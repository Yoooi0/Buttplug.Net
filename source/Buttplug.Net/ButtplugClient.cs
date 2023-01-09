using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Buttplug;

public class ButtplugClient : IAsyncDisposable
{
    internal const uint MessageVersion = 3;

    private IButtplugConnector? _connector;
    private CancellationTokenSource? _cancellationSource;
    private Task? _task;

    private readonly IButtplugMessageJsonConverter _converter;
    private readonly ConcurrentDictionary<uint, ButtplugDevice> _devices;

    public string Name { get; }
    public bool IsScanning { get; private set; }
    public bool IsConnected { get; private set; }

    public ICollection<ButtplugDevice> Devices => _devices.Values;

    public event EventHandler<ButtplugDevice>? DeviceAdded;
    public event EventHandler<ButtplugDevice>? DeviceRemoved;
    public event EventHandler<Exception>? ErrorReceived;
    public event EventHandler? ScanningFinished;
    public event EventHandler? Disconnected;

    public ButtplugClient(string name, IButtplugMessageJsonConverter converter)
    {
        Name = name;
        _converter = converter;
        _devices = new ConcurrentDictionary<uint, ButtplugDevice>();
    }

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            _connector = new ButtplugWebsocketConnector(_converter);
            _connector.InvalidMessageReceived += (_, e) => ErrorReceived?.Invoke(this, e);
            await _connector.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

            var serverInfo = await SendMessageExpectTAsync<ServerInfoButtplugMessage>(new RequestServerInfoButtplugMessage(Name), cancellationToken).ConfigureAwait(false);
            if (serverInfo.MessageVersion < 3)
                throw new ButtplugException($"A newer server is required ({serverInfo.MessageVersion} < {MessageVersion})");

            var deviceList = await SendMessageExpectTAsync<DeviceListButtplugMessage>(new RequestDeviceListButtplugMessage(), cancellationToken).ConfigureAwait(false);
            foreach (var info in deviceList.Devices)
            {
                if (_devices.ContainsKey(info.DeviceIndex))
                    continue;

                var device = new ButtplugDevice(_connector, info.DeviceMessages)
                {
                    Index = info.DeviceIndex,
                    Name = info.DeviceName,
                    DisplayName = info.DeviceDisplayName,
                    MessageTimingGap = info.DeviceMessageTimingGap
                };

                _devices.TryAdd(info.DeviceIndex, device);
                DeviceAdded?.Invoke(this, device);
            }

            _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _task = Task.Factory.StartNew(() => RunAsync(serverInfo.MaxPingTime, _cancellationSource.Token),
                _cancellationSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
                    .Unwrap();

            _ = _task.ContinueWith(_ => DisconnectAsync()).Unwrap();
        }
        catch(Exception e)
        {
            await DisconnectAsync().ConfigureAwait(false);
            e.Throw();
        }
    }

    private async Task RunAsync(uint maxPingTime, CancellationToken cancellationToken)
    {
        try
        {
            IsConnected = true;
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var tasks = new List<Task>() { ReadAsync(cancellationSource.Token) };
            if (maxPingTime > 0)
                tasks.Add(WriteAsync(maxPingTime, cancellationSource.Token));

            var task = await Task.WhenAny(tasks).ConfigureAwait(false);
            cancellationSource.Cancel();

            task.ThrowIfFaulted();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            ErrorReceived?.Invoke(this, e);
            e.Throw();
        }
        finally
        {
            IsConnected = false;
        }
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await _connector!.RecieveMessageAsync(cancellationToken).ConfigureAwait(false);
                if (message is DeviceAddedButtplugMessage deviceAdded)
                {
                    var device = new ButtplugDevice(_connector, deviceAdded.DeviceMessages)
                    {
                        Index = deviceAdded.DeviceIndex,
                        Name = deviceAdded.DeviceName,
                        DisplayName = deviceAdded.DeviceDisplayName,
                        MessageTimingGap = deviceAdded.DeviceMessageTimingGap
                    };

                    if (_devices.TryAdd(device.Index, device))
                        DeviceAdded?.Invoke(this, device);
                    else
                        ErrorReceived?.Invoke(this, new ButtplugException($"Found existing device for event \"{deviceAdded}\""));
                }
                else if (message is DeviceRemovedButtplugMessage deviceRemoved)
                {
                    if (_devices.TryRemove(deviceRemoved.DeviceIndex, out var device))
                    {
                        device.Dispose();
                        DeviceRemoved?.Invoke(this, device);
                    }
                    else
                    {
                        ErrorReceived?.Invoke(this, new ButtplugException($"Unable to find matching device for event \"{deviceRemoved}\""));
                    }
                }
                else if (message is ScanningFinishedButtplugMessage)
                {
                    IsScanning = false;
                    ScanningFinished?.Invoke(this, EventArgs.Empty);
                }
                else if (message is ErrorButtplugMessage error)
                {
                    var exception = error.ErrorCode == ErrorButtplugMessageCode.ERROR_PING
                        ? new TimeoutException("Ping timeout", new ButtplugException(error))
                        : (Exception) new ButtplugException(error);

                    ErrorReceived?.Invoke(this, exception);
                    throw exception;
                }
                else
                {
                    var exception = new ButtplugException($"Unexpected message: {message.GetType().Name}({message.Id})");
                    ErrorReceived?.Invoke(this, exception);
                    throw exception;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task WriteAsync(uint maxPingTime, CancellationToken cancellationToken)
    {
        try
        {
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(maxPingTime / 2));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
                _ = await SendMessageExpectTAsync<OkButtplugMessage>(new PingButtplugMessage(), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private int _isDisconnectingFlag;
    public async Task DisconnectAsync()
    {
        if (Interlocked.CompareExchange(ref _isDisconnectingFlag, 1, 0) != 0)
            return;

        _cancellationSource?.Cancel();

        try
        {
            if (_task != null)
                await _task.ConfigureAwait(false);
        }
        catch { }

        _cancellationSource?.Dispose();
        _cancellationSource = null;

        if (_connector != null)
            await _connector.DisposeAsync().ConfigureAwait(false);

        _connector = null;

        foreach (var (_, device) in _devices)
        {
            device.Dispose();
            DeviceRemoved?.Invoke(this, device);
        }

        _devices.Clear();
        Disconnected?.Invoke(this, EventArgs.Empty);
        Interlocked.Decrement(ref _isDisconnectingFlag);
    }

    public async Task StartScanningAsync(CancellationToken cancellationToken)
    {
        if (IsScanning)
            return;

        await SendMessageExpectTAsync<OkButtplugMessage>(new StartScanningButtplugMessage(), cancellationToken).ConfigureAwait(false);
        IsScanning = true;
    }

    public async Task StopScanningAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopScanningButtplugMessage(), cancellationToken).ConfigureAwait(false);

    public async Task StopAllDevicesAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopAllDevicesButtplugMessage(), cancellationToken).ConfigureAwait(false);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _connector == null ? throw new ObjectDisposedException(nameof(_connector))
                              : await _connector.SendMessageExpectTAsync<T>(message, cancellationToken).ConfigureAwait(false);

    protected virtual async ValueTask DisposeAsync(bool disposing) => await DisconnectAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
