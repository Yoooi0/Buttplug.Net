using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Buttplug;

internal interface IButtplugMessageTaskFactory
{
    Task<IButtplugMessage> CreateTask(IButtplugMessage message, CancellationToken cancellationToken);
}

internal class ButtplugMessageTaskManager : IButtplugMessageTaskFactory
{
    private readonly ConcurrentDictionary<uint, MessageTaskRegistration> _pendingTasks;

    public ButtplugMessageTaskManager() => _pendingTasks = new();

    public Task<IButtplugMessage> CreateTask(IButtplugMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var messageTaskCompletionSource = new CancellableTaskCompletionSource<IButtplugMessage>(cancellationToken);
        var cancellationTokenRegistration = cancellationToken.Register(() => TryCleanupTask(message.Id, out var _));

        return !_pendingTasks.TryAdd(message.Id, new(messageTaskCompletionSource, cancellationTokenRegistration))
            ? throw new ButtplugException("Found pending task with duplicate id: \"{message.Id}\"")
            : messageTaskCompletionSource.Task;
    }

    private bool TryCleanupTask(uint messageId, [NotNullWhen(true)] out CancellableTaskCompletionSource<IButtplugMessage>? messageTaskCompletionSource)
    {
        messageTaskCompletionSource = null;
        if (!_pendingTasks.TryRemove(messageId, out var messageTaskRegistration))
            return false;

        (messageTaskCompletionSource, var messageTaskCancellationTokenRegistration) = messageTaskRegistration;
        messageTaskCancellationTokenRegistration.Dispose();
        return true;
    }

    public void FinishTask(IButtplugMessage message)
    {
        if (!TryCleanupTask(message.Id, out var messageTaskCompletionSource))
            throw new ButtplugException($"Could not find pending task with id: \"{message.Id}\"");

        if (message is ErrorButtplugMessage error)
            messageTaskCompletionSource.SetException(new ButtplugException(error));
        else
            messageTaskCompletionSource.SetResult(message);
    }

    public void CancelPendingTasks()
    {
        foreach (var (messageId, _) in _pendingTasks)
        {
            if (!_pendingTasks.TryRemove(messageId, out var messageTaskRegistration))
                continue;

            messageTaskRegistration.Dispose();
        }
    }

    private readonly record struct MessageTaskRegistration(CancellableTaskCompletionSource<IButtplugMessage> MessageTaskCompletionSource, CancellationTokenRegistration CancellationTokenRegistration) : IDisposable
    {
        public void Dispose()
        {
            MessageTaskCompletionSource.Dispose();
            CancellationTokenRegistration.Dispose();
        }
    }
}

internal class CancellableTaskCompletionSource<T> : IDisposable
{
    private CancellationTokenSource? _cancellationSource;
    private TaskCompletionSource<T>? _completionSource;
    private CancellationTokenRegistration? _tokenRegistration;

    public Task<T> Task => _completionSource?.Task ?? throw new ObjectDisposedException(nameof(_completionSource));

    public CancellableTaskCompletionSource(CancellationToken cancellationToken)
    {
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _completionSource = new TaskCompletionSource<T>();

        var linkedToken = _cancellationSource.Token;
        _tokenRegistration = linkedToken.Register(() => _completionSource?.TrySetCanceled(linkedToken));
    }

    public void SetException(Exception exception) => _completionSource?.SetException(exception);
    public void SetResult(T result) => _completionSource?.SetResult(result);

    protected virtual void Dispose(bool disposing)
    {
        _cancellationSource?.Cancel();
        _tokenRegistration?.Dispose();
        _cancellationSource?.Dispose();

        _tokenRegistration = null;
        _cancellationSource = null;
        _completionSource = null;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}