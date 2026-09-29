namespace Kimchily.Server.Core.Jobs;

/// <summary>
/// Project Dawn's Push / PushAfter / Flush model, detached from TCP and game types.
/// One execution gate protects the whole drain, not just individual queue pops.
/// </summary>
public class JobSerializer(int capacity = 1024, Func<long>? clock = null)
{
    private readonly object _queueGate = new();
    private readonly object _executionGate = new();
    private readonly Queue<IJob> _ready = new();
    private readonly PriorityQueue<IJob, (long Due, long Order)> _timers = new();
    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private long _order;

    public bool TryPush(Action action) => TryPush(new Job(action));

    public bool TryPush(IJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_queueGate)
        {
            if (_ready.Count + _timers.Count >= _capacity) return false;
            _ready.Enqueue(job);
            return true;
        }
    }

    public bool TryPushAfter(int milliseconds, Action action)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        lock (_queueGate)
        {
            if (_ready.Count + _timers.Count >= _capacity) return false;
            // The old maximum-heap / 32-bit timer is replaced by a stable minimum-heap.
            _timers.Enqueue(new Job(action), (checked(_clock() + milliseconds), _order++));
            return true;
        }
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!TryPush(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception exception) { completion.SetException(exception); }
        })) completion.SetException(new InvalidOperationException("The room work queue is full."));
        return completion.Task;
    }

    public void Flush(int budget = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);
        lock (_executionGate)
        {
            for (var index = 0; index < budget; index++)
            {
                IJob job;
                lock (_queueGate)
                {
                    if (_timers.TryPeek(out _, out var deadline) && deadline.Due <= _clock())
                        job = _timers.Dequeue();
                    else if (_ready.Count != 0) job = _ready.Dequeue();
                    else return;
                }
                job.Execute();
            }
        }
    }
}
