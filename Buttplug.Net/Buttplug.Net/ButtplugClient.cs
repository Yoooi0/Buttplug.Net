using System.Collections.Concurrent;

namespace Buttplug;

public class ButtplugClient : IAsyncDisposable
{
    internal const uint MessageVersion = 3;

    private IButtplugConnector? _connector;
    private CancellationTokenSource? _cancellationSource;
    private Task? _task;

    private readonly IButtplugJsonMessageConverter _converter;
    private readonly ConcurrentDictionary<uint, ButtplugDevice> _devices;

    public string Name { get; }

    public event EventHandler<ButtplugDevice>? DeviceAdded;
    public event EventHandler<ButtplugDevice>? DeviceRemoved;
    public event EventHandler<Exception>? ErrorReceived;
    public event EventHandler? ScanningFinished;
    public event EventHandler? Disconnected;

    public ButtplugClient(string name, IButtplugJsonMessageConverter converter)
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
            await _connector.ConnectAsync(uri, cancellationToken);

            var serverInfo = await SendMessageExpectTAsync<ServerInfoButtplugMessage>(new RequestServerInfoButtplugMessage(Name), cancellationToken);
            if (serverInfo.MessageVersion < 3)
                throw new ButtplugException($"A newer server is required ({serverInfo.MessageVersion} < {MessageVersion})");

            var deviceList = await SendMessageExpectTAsync<DeviceListButtplugMessage>(new RequestDeviceListButtplugMessage(), cancellationToken);
            foreach (var info in deviceList.Devices)
            {
                if (_devices.ContainsKey(info.DeviceIndex))
                    continue;

                var device = new ButtplugDevice(_connector)
                {
                    Index = info.DeviceIndex,
                    Name = info.DeviceName,
                    DisplayName = info.DeviceDisplayName,
                    MessageTimingGap = info.DeviceMessageTimingGap,
                    MessageAttributes = info.DeviceMessages
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
            await DisconnectAsync();
            e.Throw();
        }
    }

    private async Task RunAsync(uint maxPingTime, CancellationToken cancellationToken)
    {
        try
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var tasks = new List<Task>() { ReadAsync(cancellationSource.Token) };
            if (maxPingTime > 0)
                tasks.Add(WriteAsync(maxPingTime, cancellationSource.Token));

            var task = await Task.WhenAny(tasks);
            cancellationSource.Cancel();

            task.ThrowIfFaulted();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            ErrorReceived?.Invoke(this, e);
            e.Throw();
        }
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await _connector!.RecieveMessageAsync(cancellationToken);
                if (message is DeviceAddedButtplugMessage deviceAdded)
                {
                    var device = new ButtplugDevice(_connector)
                    {
                        Index = deviceAdded.DeviceIndex,
                        Name = deviceAdded.DeviceName,
                        DisplayName = deviceAdded.DeviceDisplayName,
                        MessageTimingGap = deviceAdded.DeviceMessageTimingGap,
                        MessageAttributes = deviceAdded.DeviceMessages
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
            while (await timer.WaitForNextTickAsync(cancellationToken) && !cancellationToken.IsCancellationRequested)
                _ = await SendMessageExpectTAsync<OkButtplugMessage>(new PingButtplugMessage(), cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

    private int _isDisconnectingFlag;
    public async Task DisconnectAsync()
    {
        if (Interlocked.CompareExchange(ref _isDisconnectingFlag, 1, 0) != 0)
            return;

        _cancellationSource?.Cancel();

        if (_task != null)
            await _task;

        _cancellationSource?.Dispose();
        _cancellationSource = null;

        if (_connector != null)
            await _connector.DisposeAsync();

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
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StartScanningButtplugMessage(), cancellationToken);

    public async Task StopScanningAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopScanningButtplugMessage(), cancellationToken);

    public async Task StopAllDevicesAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopAllDevicesButtplugMessage(), cancellationToken);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _connector == null ? throw new ObjectDisposedException(nameof(_connector))
                              : await _connector.SendMessageExpectTAsync<T>(message, cancellationToken);

    protected virtual async ValueTask DisposeAsync(bool disposing) => await DisconnectAsync();

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }
}
