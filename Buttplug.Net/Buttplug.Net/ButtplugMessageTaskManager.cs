using System.Collections.Concurrent;

namespace Buttplug;

internal class ButtplugMessageTaskManager
{
    private readonly ConcurrentDictionary<uint, (CancellableTaskCompletionSource<IButtplugMessage> CompletionSource, CancellationTokenRegistration Registration)> _pendingTasks;

    public ButtplugMessageTaskManager() => _pendingTasks = new();

    public Task<IButtplugMessage> CreateTask(IButtplugMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var completionSource = new CancellableTaskCompletionSource<IButtplugMessage>(cancellationToken);
        var registration = cancellationToken.Register(() => TryCleanupTask(message.Id, out var _));

        return !_pendingTasks.TryAdd(message.Id, (completionSource, registration))
            ? throw new ButtplugException("Found pending task with duplicate id: \"{message.Id}\"")
            : completionSource.Task;
    }

    private bool TryCleanupTask(uint messageId, out CancellableTaskCompletionSource<IButtplugMessage>? completionSource)
    {
        completionSource = null;
        if (!_pendingTasks.TryRemove(messageId, out var item))
            return false;

        (completionSource, var registration) = item;
        registration.Dispose();
        return true;
    }

    public void FinishTask(IButtplugMessage message)
    {
        if (!TryCleanupTask(message.Id, out var completionSource))
            throw new ButtplugException($"Could not find pending task with id: \"{message.Id}\"");

        if (message is ErrorButtplugMessage error)
            completionSource!.SetException(new ButtplugException(error));
        else
            completionSource!.SetResult(message);
    }

    public void CancelPendingTasks()
    {
        foreach (var (messageId, _) in _pendingTasks)
        {
            if (!_pendingTasks.TryRemove(messageId, out var item))
                continue;

            var (completionSource, registration) = item;
            completionSource.Dispose();
            registration.Dispose();
        }
    }

    private class CancellableTaskCompletionSource<T> : IDisposable
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
}
