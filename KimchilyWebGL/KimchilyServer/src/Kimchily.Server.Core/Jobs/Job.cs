namespace Kimchily.Server.Core.Jobs;

// Adapted from Project Dawn's Server/Game/Job/Job.cs. See docs/reuse.md.
public interface IJob
{
    void Execute();
}

public sealed class Job(Action action) : IJob
{
    private readonly Action _action = action ?? throw new ArgumentNullException(nameof(action));
    public void Execute() => _action();
}

public sealed class Job<T>(Action<T> action, T argument) : IJob
{
    public void Execute() => action(argument);
}
