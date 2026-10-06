namespace Kimchily.Server.Core.Jobs
{
    // Adapted from Project Dawn's Server/Game/Job/Job.cs. See docs/reuse.md.
    public interface IJob
    {
        void Execute();
    }

    public sealed class Job : IJob
    {
        private readonly Action _action;

        public Job(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _action = action;
        }

        public void Execute()
        {
            _action();
        }
    }

    public sealed class Job<T> : IJob
    {
        private readonly Action<T> _action;
        private readonly T _argument;

        public Job(Action<T> action, T argument)
        {
            _action = action;
            _argument = argument;
        }

        public void Execute()
        {
            _action(_argument);
        }
    }
}
