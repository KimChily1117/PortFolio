using System;
using System.Collections;
using MoonSharp.Interpreter;
using Kimchily.Creator;
using UnityEngine;

namespace Kimchily.Scripting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoroutineScheduler))]
    [AddComponentMenu("Kimchily/Lua Behaviour")]
    public sealed class KimchilyLuaBehaviour : MonoBehaviour
    {
        [SerializeField] private TextAsset scriptAsset;
        [SerializeField] private LuaObjectReference[] references = Array.Empty<LuaObjectReference>();
        [SerializeField, Range(100, 100000)] private int instructionBudget = 20000;
        private LuaSandboxSession session;
        private CoroutineScheduler scheduler;
        private bool started;
        private bool pendingDisable;
        private bool destroying;
        private bool handlingDeferred;

        /// <summary>Assign before enabling, or call Reload after changing the asset.</summary>
        public TextAsset ScriptAsset { get => scriptAsset; set => scriptAsset = value; }
        public LuaObjectReference[] References { get => references; set => references = value ?? Array.Empty<LuaObjectReference>(); }
        public int InstructionBudget
        {
            get => Math.Max(100, Math.Min(100000, instructionBudget));
            set => instructionBudget = Math.Max(100, Math.Min(100000, value));
        }
        public bool IsReady => session != null && !IsFaulted && !destroying;
        public bool IsFaulted { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            if (!Initialize()) return;
            session.AcceptingRoutines = true;
            InvokeLifecycle("on_enable");
        }

        private void Start()
        {
            started = true;
            if (!Initialize()) return;
            InvokeLifecycle("on_start");
            DrainPending();
        }

        private void Update()
        {
            if (!IsReady) return;
            InvokeLifecycle("on_update", DynValue.NewNumber(Time.deltaTime));
            DrainPending();
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.AcceptingRoutines = false;
            session.CancelPending();
            if (scheduler != null) scheduler.CancelOwnedBy(this);
            pendingDisable = true;
            ProcessDeferred();
        }

        private void OnDestroy()
        {
            destroying = true;
            if (session == null) return;
            session.AcceptingRoutines = false;
            session.CancelPending();
            if (scheduler != null) scheduler.CancelOwnedBy(this);
            ProcessDeferred();
        }

        /// <summary>Explicitly restarts a changed script and cancels all its previous routines.</summary>
        public void Reload()
        {
            if (session != null && session.IsExecuting)
                throw new InvalidOperationException("Cannot reload while Lua is executing.");
            if (session != null)
            {
                session.AcceptingRoutines = false;
                session.CancelPending();
                if (scheduler != null) scheduler.CancelOwnedBy(this);
                InvokeLifecycle("on_destroy");
            }
            session = null;
            pendingDisable = false;
            IsFaulted = false;
            LastError = null;
            if (!isActiveAndEnabled || !Initialize()) return;
            InvokeLifecycle("on_enable");
            if (started) InvokeLifecycle("on_start");
            DrainPending();
        }

        private bool Initialize()
        {
            if (session != null) return IsReady;
            if (scriptAsset == null || IsFaulted || destroying) return false;
            scheduler = GetComponent<CoroutineScheduler>();
            try
            {
                session = new LuaSandboxSession(gameObject, references, instructionBudget,
                    message => Debug.Log("[Kimchily Lua] " + message, this));
                session.Load(scriptAsset.text, scriptAsset.name);
                ProcessDeferred();
                return IsReady;
            }
            catch (Exception exception) { Fault(exception); return false; }
        }

        private void InvokeLifecycle(string name, params DynValue[] arguments)
        {
            if (session == null || IsFaulted) return;
            try { session.Invoke(name, arguments); }
            catch (Exception exception) { Fault(exception); }
            ProcessDeferred();
        }

        private void ProcessDeferred()
        {
            if (session == null || session.IsExecuting || handlingDeferred) return;
            handlingDeferred = true;
            try
            {
                if (pendingDisable)
                {
                    pendingDisable = false;
                    InvokeLifecycle("on_disable");
                }
                if (destroying)
                {
                    InvokeLifecycle("on_destroy");
                    session = null;
                }
            }
            finally { handlingDeferred = false; }
        }

        private void DrainPending()
        {
            if (!IsReady || !isActiveAndEnabled) return;
            // Snapshot: routines started by another routine wait until the next frame.
            // This avoids unbounded CLR re-entry through a Lua start() callback.
            int count = session.PendingCount;
            for (int i = 0; i < count && IsReady && isActiveAndEnabled && session.PendingCount > 0; i++)
            {
                LuaSandboxSession currentSession = session;
                DynValue routine = currentSession.TakePending();
                try { scheduler.StartRoutine(Run(currentSession, routine), this); }
                catch (Exception exception) { currentSession.RoutineFinished(); Fault(exception); }
            }
        }

        private IEnumerator Run(LuaSandboxSession owner, DynValue coroutine)
        {
            try
            {
                while (IsReady && session == owner && isActiveAndEnabled)
                {
                    DynValue result;
                    try { result = owner.ResumeRoutine(coroutine); }
                    catch (Exception exception) { Fault(exception); yield break; }
                    ProcessDeferred();
                    if (!IsReady || !isActiveAndEnabled || coroutine.Coroutine.State == CoroutineState.Dead)
                        yield break;
                    if (result.Type == DataType.Number)
                        yield return new WaitForSeconds((float)result.Number);
                    else
                        yield return null;
                }
            }
            finally { owner.RoutineFinished(); }
        }

        private void Fault(Exception exception)
        {
            if (IsFaulted) return;
            IsFaulted = true;
            LastError = exception is InterpreterException interpreterException
                ? interpreterException.DecoratedMessage ?? interpreterException.Message
                : exception.Message;
            if (session != null)
            {
                session.AcceptingRoutines = false;
                session.CancelPending();
            }
            if (scheduler != null) scheduler.CancelOwnedBy(this);
            Debug.LogError("[Kimchily Lua] " + (scriptAsset == null ? name : scriptAsset.name) + ": " + LastError, this);
        }
    }
}
