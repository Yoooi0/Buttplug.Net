namespace Buttplug;

internal interface IButtplugSender
{
    Task<IButtplugMessage> SendMessageAsync(IButtplugMessage message, CancellationToken cancellationToken);
    Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage;
}

internal interface IButtplugReceiver
{
    Task<IButtplugMessage> RecieveMessageAsync(CancellationToken cancellationToken);
}

internal interface IButtplugConnector : IButtplugSender, IButtplugReceiver, IAsyncDisposable
{
    event EventHandler<Exception>? InvalidMessageReceived;

    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task DisconnectAsync();
}
