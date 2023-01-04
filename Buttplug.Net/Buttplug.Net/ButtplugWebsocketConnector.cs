using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace Buttplug;

internal class ButtplugWebsocketConnector : IButtplugConnector
{
    private readonly Channel<IButtplugMessage> _sendMessageChannel;
    private readonly Channel<IButtplugMessage> _receiveMessageChannel;
    private readonly ButtplugMessageTaskManager _taskManager;
    private readonly IButtplugJsonMessageConverter _converter;

    private CancellationTokenSource? _cancellationSource;
    private Task? _task;

    public event EventHandler<Exception>? InvalidMessageReceived;

    public ButtplugWebsocketConnector(IButtplugJsonMessageConverter converter)
    {
        _converter = converter;

        _taskManager = new ButtplugMessageTaskManager();
        _sendMessageChannel = Channel.CreateUnbounded<IButtplugMessage>(new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = true,
        });
        _receiveMessageChannel = Channel.CreateUnbounded<IButtplugMessage>(new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = true,
        });
    }

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cancellationToken);

        _task = Task.Factory.StartNew(() => RunAsync(client, _cancellationSource.Token),
            _cancellationSource.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)
            .Unwrap();

        _ = _task.ContinueWith(_ => DisconnectAsync()).Unwrap();
    }

    private async Task RunAsync(ClientWebSocket client, CancellationToken cancellationToken)
    {
        try
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var task = await Task.WhenAny(ReadAsync(client, cancellationSource.Token), WriteAsync(client, cancellationSource.Token));
            cancellationSource.Cancel();

            task.ThrowIfFaulted();
        }
        catch (OperationCanceledException) { }
        finally
        {
            client.Dispose();
        }
    }

    private async Task ReadAsync(ClientWebSocket client, CancellationToken cancellationToken)
    {
        try
        {
            while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var messageJson = await client.ReceiveStringAsync(Encoding.UTF8, cancellationToken);

                try
                {
                    foreach (var message in _converter.Deserialize(messageJson))
                    {
                        if (message.Id == 0)
                            await _receiveMessageChannel.Writer.WriteAsync(message, cancellationToken);
                        else
                            _taskManager.FinishTask(message);
                    }
                }
                catch (Exception e)
                {
                    InvalidMessageReceived?.Invoke(this, e);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task WriteAsync(ClientWebSocket client, CancellationToken cancellationToken)
    {
        try
        {
            while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var message = await _sendMessageChannel.Reader.ReadAsync(cancellationToken);
                var messageJson = _converter.Serialize(message);

                await client.SendAsync(Encoding.UTF8.GetBytes(messageJson), WebSocketMessageType.Text, true, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task<IButtplugMessage> RecieveMessageAsync(CancellationToken cancellationToken) => await _receiveMessageChannel.Reader.ReadAsync(cancellationToken);
    public async Task<IButtplugMessage> SendMessageAsync(IButtplugMessage message, CancellationToken cancellationToken)
    {
        if (_task?.IsCompleted == true)
            throw new ButtplugException("Cannot send messages while disconnected");

        var task = _taskManager.CreateTask(message, cancellationToken);
        await _sendMessageChannel.Writer.WriteAsync(message, cancellationToken);
        return await task;
    }

    public async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
    {
        var result = await SendMessageAsync(message, cancellationToken);
        return result switch
        {
            T resultT => resultT,
            ErrorButtplugMessage error => throw new ButtplugException(error),
            _ => throw new ButtplugException($"Unexpected response message: {result.GetType().Name}({result.Id})")
        };
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

        _taskManager.CancelPendingTasks();

        Interlocked.Decrement(ref _isDisconnectingFlag);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing) => await DisconnectAsync();

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }
}
