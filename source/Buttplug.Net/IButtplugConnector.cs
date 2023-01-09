namespace Buttplug;

internal interface IButtplugSender
{
    Task<IButtplugMessage> SendMessageAsync(IButtplugMessage message, CancellationToken cancellationToken);
    Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage;
}

internal interface IButtplugReceiver
{
    IAsyncEnumerable<IButtplugMessage> RecieveMessagesAsync(CancellationToken cancellationToken);
}

internal interface IButtplugConnector : IButtplugSender, IButtplugReceiver, IAsyncDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task DisconnectAsync();
}
