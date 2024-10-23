using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace Buttplug;

public class ButtplugClient(string name, IButtplugMessageJsonConverter converter) : IAsyncDisposable
{
    internal const uint MessageVersion = 3;

    private readonly ButtplugMessageTaskManager _taskManager = new();
    private readonly ConcurrentDictionary<uint, ButtplugDevice> _devices = new();

    private ButtplugWebsocketConnector? _connector;
    private CancellationTokenSource? _cancellationSource;
    private Task? _task;
    private int _isDisconnectingFlag;

    public string Name { get; } = name;
    public bool IsConnected { get; private set; }

    public ICollection<ButtplugDevice> Devices => _devices.Values;

    public event EventHandler<ButtplugDevice>? DeviceAdded;
    public event EventHandler<ButtplugDevice>? DeviceRemoved;
    public event EventHandler<Exception>? UnhandledException;
    public event EventHandler? ScanningFinished;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (_cancellationSource != null)
            throw new ButtplugException("The client is already connected");

        try
        {
            _connector = new ButtplugWebsocketConnector(converter, _taskManager);
            await _connector.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

            using var connectionSemaphore = new SemaphoreSlim(0, 1);
            _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _task = Task.Run(() => RunAsync(connectionSemaphore, _cancellationSource.Token), _cancellationSource.Token);
            _ = _task.ContinueWith(_ => DisconnectAsync());

            await connectionSemaphore.WaitAsync(_cancellationSource.Token).ConfigureAwait(false);
        }
        catch(Exception e)
        {
            await DisconnectAsync().ConfigureAwait(false);
            e.Throw();
        }
    }

    private async Task RunAsync(SemaphoreSlim connectionSemaphore, CancellationToken cancellationToken)
    {
        try
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var tasks = new List<Task>() { ReadAsync(cancellationSource.Token) };

            var serverInfo = await SendMessageExpectTAsync<ServerInfoButtplugMessage>(new RequestServerInfoButtplugMessage(Name), cancellationToken).ConfigureAwait(false);
            if (serverInfo.MessageVersion < 3)
                throw new ButtplugException($"A newer server is required ({serverInfo.MessageVersion} < {MessageVersion})");

            var deviceList = await SendMessageExpectTAsync<DeviceListButtplugMessage>(new RequestDeviceListButtplugMessage(), cancellationToken).ConfigureAwait(false);
            foreach (var info in deviceList.Devices)
            {
                if (_devices.ContainsKey(info.DeviceIndex))
                    continue;

                var device = new ButtplugDevice(_connector!, info);
                _devices.TryAdd(info.DeviceIndex, device);
                DeviceAdded?.Invoke(this, device);
            }

            if (serverInfo.MaxPingTime > 0)
                tasks.Add(WriteAsync(serverInfo.MaxPingTime, cancellationSource.Token));

            IsConnected = true;
            connectionSemaphore.Release();

            var task = await Task.WhenAny(tasks).ConfigureAwait(false);
            cancellationSource.Cancel();

            task.ThrowIfFaulted();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            UnhandledException?.Invoke(this, e);
            e.Throw();
        }
        finally
        {
            IsConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach(var message in _connector!.RecieveMessagesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (message.Id == 0)
                    HandleSystemMessage(message);
                else
                    _taskManager.FinishTask(message);
            }
        }
        catch (OperationCanceledException) { }

        void HandleSystemMessage(IButtplugMessage message)
        {
            if (message is DeviceAddedButtplugMessage deviceAdded)
            {
                var device = new ButtplugDevice(_connector, deviceAdded);
                if (_devices.TryAdd(device.Index, device))
                    DeviceAdded?.Invoke(this, device);
                else
                    UnhandledException?.Invoke(this, new ButtplugException($"Found existing device for event \"{deviceAdded}\""));
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
                    UnhandledException?.Invoke(this, new ButtplugException($"Unable to find matching device for event \"{deviceRemoved}\""));
                }
            }
            else if (message is ScanningFinishedButtplugMessage)
            {
                ScanningFinished?.Invoke(this, EventArgs.Empty);
            }
            else if (message is SensorReadingButtplugMessage sensorReading)
            {
                if (!_devices.TryGetValue(sensorReading.DeviceIndex, out var device))
                    throw new ButtplugException("Received sensor reading for missing device");

                device.HandleSubscribeSensorReading(sensorReading.SensorIndex, sensorReading.SensorType, sensorReading.Data);
            }
            else if (message is EndpointReadingButtplugMessage endpointReading)
            {
                if (!_devices.TryGetValue(endpointReading.DeviceIndex, out var device))
                    throw new ButtplugException("Received endpoint reading for missing device");

                device.HandleSubscribeEndpointReading(endpointReading.Endpoint, endpointReading.Data);
            }
            else if (message is ErrorButtplugMessage error)
            {
                if (error.ErrorCode == ErrorButtplugMessageCode.Ping)
                    throw new TimeoutException("Ping timeout", new ButtplugException(error));
                throw new ButtplugException(error);
            }
            else
            {
                throw new ButtplugException($"Unexpected message: {message.GetType().Name}({message.Id})");
            }
        }
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

        _taskManager.CancelPendingTasks();

        foreach (var (_, device) in _devices)
        {
            device.Dispose();
            DeviceRemoved?.Invoke(this, device);
        }

        _devices.Clear();
        Interlocked.Decrement(ref _isDisconnectingFlag);
    }

    public async Task StartScanningAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StartScanningButtplugMessage(), cancellationToken).ConfigureAwait(false);

    public async Task StopScanningAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopScanningButtplugMessage(), cancellationToken).ConfigureAwait(false);

    public async Task StopAllDevicesAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopAllDevicesButtplugMessage(), cancellationToken).ConfigureAwait(false);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _connector == null ? throw new ObjectDisposedException(nameof(_connector))
                              : await _connector.SendMessageExpectTAsync<T>(message, cancellationToken).ConfigureAwait(false);

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(nameof(ButtplugClient));
        stringBuilder.Append(" { ");
        if (PrintMembers(stringBuilder))
            stringBuilder.Append(' ');

        stringBuilder.Append('}');
        return stringBuilder.ToString();
    }

    protected virtual bool PrintMembers(StringBuilder builder)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        builder.Append($"{nameof(Name)} = ");
        builder.Append(Name);
        builder.Append($", {nameof(Devices)} = ");
        builder.Append(_devices.Count);
        return true;
    }

    protected virtual async ValueTask DisposeAsync(bool disposing) => await DisconnectAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
