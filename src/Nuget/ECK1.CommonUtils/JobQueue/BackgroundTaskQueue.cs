using System.Collections.Concurrent;

namespace ECK1.CommonUtils.JobQueue;

public interface IBackgroundTaskQueue
{
    void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem);
    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly ConcurrentQueue<Func<IServiceProvider, CancellationToken, Task>> _workItems = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        _workItems.Enqueue(workItem);
        _signal.Release();
    }

    public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        _workItems.TryDequeue(out var workItem);
        return workItem!;
    }
}
