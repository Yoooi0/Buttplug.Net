namespace Buttplug.Tests;

public class CancellableTaskCompletionSourceTests
{
    [Fact]
    public async Task TaskIsFaultedWhenPassedCancelledToken()
    {
        var token = new CancellationToken(true);
        var completionSource = new CancellableTaskCompletionSource<object>(token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completionSource.Task);
    }

    [Fact]
    public async Task TaskIsFaultedAfterDisposing()
    {
        var token = CancellationToken.None;
        var completionSource = new CancellableTaskCompletionSource<object>(token);
        var task = completionSource.Task;

        completionSource.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task TaskPropertyThrowsAfterDisposing()
    {
        var token = CancellationToken.None;
        var completionSource = new CancellableTaskCompletionSource<object>(token);

        completionSource.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => completionSource.Task);
    }

    [Fact]
    public async Task TaskIsFaultedAfterCallingSetException()
    {
        var token = CancellationToken.None;
        var completionSource = new CancellableTaskCompletionSource<object>(token);

        completionSource.SetException(new Exception());

        await Assert.ThrowsAnyAsync<Exception>(async () => await completionSource.Task);
    }

    [Fact]
    public async Task TaskIsCompletedAfterCallingSetResult()
    {
        var token = CancellationToken.None;
        var completionSource = new CancellableTaskCompletionSource<object>(token);
        var result = new object();
        var task = completionSource.Task;

        completionSource.SetResult(result);

        var taskResult = await task;
        Assert.True(task.IsCompleted);
        Assert.Equal(result, taskResult);
    }
}
