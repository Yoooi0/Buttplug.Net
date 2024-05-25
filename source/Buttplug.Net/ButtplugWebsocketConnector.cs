using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Buttplug;

internal class ButtplugWebsocketConnector(IButtplugMessageJsonConverter converter, IButtplugMessageTaskFactory taskFactory) : IButtplugConnector
{
    private ClientWebSocket? _client;

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (_client != null)
            throw new ButtplugException("Connector is already connected");

        _client = new ClientWebSocket();
        await _client.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<IButtplugMessage> RecieveMessagesAsync(CancellationToken cancellationToken)
    {
        return _client switch
        {
            null => throw new ButtplugException("Cannot receive messages while disconnected"),
            _ => RecieveMessagesAsync(cancellationToken)
        };

        async IAsyncEnumerable<IButtplugMessage> RecieveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (_client?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var messageJson = await _client.ReceiveStringAsync(Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(messageJson))
                    continue;

                foreach (var message in converter.Deserialize(messageJson))
                    yield return message;
            }
        }
    }

    public async Task<IButtplugMessage> SendMessageAsync(IButtplugMessage message, CancellationToken cancellationToken)
    {
        if (_client == null)
            throw new ButtplugException("Cannot send messages while disconnected");

        var task = taskFactory.CreateTask(message, cancellationToken);
        var messageJson = converter.Serialize(message);

        await _client.SendAsync(Encoding.UTF8.GetBytes(messageJson), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    public async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
    {
        var result = await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            T resultT => resultT,
            ErrorButtplugMessage error => throw new ButtplugException(error),
            _ => throw new ButtplugException($"Unexpected response message: {result.GetType().Name}({result.Id})")
        };
    }

    public async Task DisconnectAsync()
    {
        if (_client != null && (_client.State == WebSocketState.Connecting || _client.State == WebSocketState.Open))
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);

        _client?.Dispose();
        _client = null;
    }

    protected virtual async ValueTask DisposeAsync(bool disposing) => await DisconnectAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
