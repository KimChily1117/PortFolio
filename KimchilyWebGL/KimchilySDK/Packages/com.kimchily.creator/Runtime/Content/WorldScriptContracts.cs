using System.Collections.Generic;

namespace Kimchily.Creator.Content
{
    /// <summary>Content-only checks available to the Editor without coupling it to an optional scripting package.</summary>
    public interface IWorldContentValidatable
    {
        IEnumerable<string> ValidateContent();
    }

    /// <summary>Reports the initial script lifecycle result before a loaded world is declared ready.</summary>
    public interface IWorldScriptStatus
    {
        bool HasStarted { get; }
        bool IsFaulted { get; }
        string LastError { get; }
    }
}
