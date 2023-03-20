namespace Buttplug.Tests;

public class ButtplugMessageTaskManagerTests
{
    [Fact]
    public async Task CreateTaskThrowsWhenPassedCancelledToken()
    {
        var manager = new ButtplugMessageTaskManager();
        var message = new PingButtplugMessage();
        var token = new CancellationToken(true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.CreateTask(message, token));
    }

    [Fact]
    public async Task CreateTaskThrowsWhenDuplicateMessageIdFound()
    {
        var manager = new ButtplugMessageTaskManager();
        var message = new PingButtplugMessage();
        var token = CancellationToken.None;

        _ = manager.CreateTask(message, token);
        await Assert.ThrowsAsync<ButtplugException>(() => manager.CreateTask(message, token));
    }

    [Fact]
    public void FinishTaskThrowsWhenPassedUnknownMessage()
    {
        var manager = new ButtplugMessageTaskManager();
        var message = new PingButtplugMessage();

        Assert.Throws<ButtplugException>(() => manager.FinishTask(message));
    }

    [Fact]
    public async Task MessageTaskGetsCancelledByToken()
    {
        var manager = new ButtplugMessageTaskManager();
        var message = new PingButtplugMessage();
        var cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;

        var task = manager.CreateTask(message, token);
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }

    [Fact]
    public async Task MessageTaskGetsCancelledWhenClearingTasks()
    {
        var manager = new ButtplugMessageTaskManager();
        var message = new PingButtplugMessage();
        var token = CancellationToken.None;

        var task = manager.CreateTask(message, token);
        manager.CancelPendingTasks();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }

    [Fact]
    public async Task MessageTaskIsFaultedWhenFinishedWithErrorMessage()
    {
        var manager = new ButtplugMessageTaskManager();
        var sentMessage = new PingButtplugMessage();
        var receivedMessage = new ErrorButtplugMessage(sentMessage.Id, "", ErrorButtplugMessageCode.Message);
        var token = CancellationToken.None;

        var task = manager.CreateTask(sentMessage, token);
        manager.FinishTask(receivedMessage);

        await Assert.ThrowsAsync<ButtplugException>(async () => await task);
    }

    [Fact]
    public async Task MessageTaskIsCompletedWhenFinishedWithValidMessage()
    {
        var manager = new ButtplugMessageTaskManager();
        var sentMessage = new PingButtplugMessage();
        var receivedMessage = new OkButtplugMessage(sentMessage.Id);
        var token = CancellationToken.None;

        var task = manager.CreateTask(sentMessage, token);
        manager.FinishTask(receivedMessage);
        await task;

        Assert.True(task.IsCompleted);
        Assert.False(task.IsFaulted);
    }
}
