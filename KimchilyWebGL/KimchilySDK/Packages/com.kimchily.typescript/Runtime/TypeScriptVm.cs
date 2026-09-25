using System;
using System.Collections.Generic;
using System.Diagnostics;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Kimchily.TypeScript
{
    /// <summary>Interprets a closed CommonJS graph. No CLR objects, assemblies or module IO are exposed.</summary>
    internal sealed class TypeScriptVm : IDisposable
    {
        internal const int MaximumModules = 128;
        internal const int MaximumModuleCharacters = 262144;
        internal const int MaximumTotalCharacters = 1048576;
        internal const int MaximumFields = 128;
        internal const int MinimumBudget = 100;
        internal const int MaximumBudget = 100000;
        internal const int InitializationTimeoutMilliseconds = 5000;
        internal const int ExecutionTimeoutMilliseconds = 100;
        private readonly Dictionary<string, string> sources;
        private readonly Dictionary<string, ObjectInstance> loaded = new Dictionary<string, ObjectInstance>(StringComparer.Ordinal);
        private readonly OperationBudget budget;
        private readonly ObjectInstance helpers;
        private readonly ObjectInstance bridge;
        private ObjectInstance instance;
        private bool disposed;
        internal Engine Engine { get; }
        internal bool IsExecuting { get; private set; }
        internal int InstructionBudget { get; }

        // Reset is deliberately a no-op: a require callback or a getter must not replenish
        // the enclosing host-operation budget. Begin is called only at our outer boundary.
        private sealed class OperationBudget : Constraint
        {
            private int remaining;
            private int initialStatements;
            private long startedAt;
            private long deadline;
            private int timeoutMilliseconds;
            private bool active;
            internal string Stage { get; set; }
            internal void Begin(int statements, int milliseconds, string stage)
            {
                remaining = initialStatements = statements;
                startedAt = Stopwatch.GetTimestamp();
                timeoutMilliseconds = milliseconds;
                deadline = startedAt + Stopwatch.Frequency * milliseconds / 1000;
                Stage = stage;
                active = true;
            }
            internal void End() { active = false; }
            public override void Reset() { }
            public override void Check()
            {
                if (!active) return;
                if (--remaining < 0)
                    throw new InvalidOperationException("TypeScript statement budget exceeded.");
                CheckTime();
            }
            // Also called after host operations: a final slow host callback may
            // return without another interpreted statement that would run Check.
            internal void CheckTime()
            {
                if (active && Stopwatch.GetTimestamp() > deadline)
                    throw new InvalidOperationException("TypeScript time budget exceeded.");
            }
            internal string Describe() => "stage=" + Stage + ", elapsedMs=" +
                ((Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency) +
                ", timeLimitMs=" + timeoutMilliseconds + ", statements=" +
                (initialStatements - remaining) + "/" + initialStatements;
        }

        internal TypeScriptVm(Dictionary<string, string> modules, string bootstrap,
            Func<Engine, ObjectInstance> createHost, int instructionBudget)
        {
            sources = modules ?? throw new ArgumentNullException(nameof(modules));
            ValidateModules(sources);
            InstructionBudget = ClampBudget(instructionBudget);
            budget = new OperationBudget();
            var options = new Options();
            options.Interop.Enabled = false;
            options.Interop.AllowGetType = false;
            options.Interop.AllowSystemReflection = false;
            options.Interop.AllowWrite = false;
            options.DisableStringCompilation();
            options.LimitRecursion(64);
            options.MaxStatements(InstructionBudget);
            // One host-owned deadline covers nested require/call entries. A fixed
            // Jint TimeoutInterval would reintroduce 100ms during cold bootstrap,
            // and reset its deadline independently at each top-level engine entry.
#if !UNITY_WEBGL || UNITY_EDITOR
            options.LimitMemory(16 * 1024 * 1024);
#endif
            // Unity WebGL IL2CPP does not implement the thread allocation counter
            // used by Jint's MemoryLimitConstraint. Do not register that constraint
            // on Web players: even its Reset calls the unsupported icall. Source,
            // array, recursion, statement and host-owned time bounds still apply;
            // none is a substitute for a per-VM heap quota or process isolation.
            options.Constraint(budget);
            options.Constraints.StackOverflowGuard = true;
            options.Constraints.MaxArraySize = 65536;
            options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(50);
            Engine = new Engine(options);
            // Blocking shared-memory waits do not belong to a frame-driven world.
            // No browser, Node, CLR namespace or external module loader is installed.
            ObjectInstance[] initialized;
            try
            {
                initialized = Run("bootstrap:capabilities", InitializationTimeoutMilliseconds, () =>
                {
                    Engine.Execute("delete globalThis.Atomics; delete globalThis.SharedArrayBuffer;");
                    // Helpers execute property reads, generator methods and error-prone accessors
                    // within an engine call; host-side ObjectInstance.Get alone is not a boundary.
                    ObjectInstance localHelpers = AtStage("bootstrap:helpers", () => Engine.Evaluate(@"({
                invoke(o,n,a) { const f=o[n]; if(f==null) return; if(typeof f!=='function') throw new TypeError(n+' must be a function'); return f.apply(o,a); },
                next(g) { return g.next(); },
                close(g) { const f=g.return; if(typeof f!=='function') return; let r=f.call(g); let i=0; while(r && !r.done && i++<32) r=g.next(); if(r && !r.done) throw new Error('Generator cleanup yield limit exceeded'); },
                step(r) { if(!r || typeof r!=='object') throw new TypeError('Invalid generator result'); if(r.done) return [true,0]; const v=r.value; if(v==null) return [false,-1]; if(v.__kimchilyWait==='seconds' && typeof v.seconds==='number' && Number.isFinite(v.seconds) && v.seconds>=0 && v.seconds<=3600) return [false,v.seconds]; throw new TypeError('Yield null or WaitForSeconds only'); }
            })").AsObject());
                    JsValue factory = AtStage("bootstrap:parse", () => Engine.Evaluate(bootstrap));
                    ObjectInstance host = AtStage("bootstrap:host", () => createHost(Engine));
                    ObjectInstance localBridge = AtStage("bootstrap:execute", () => Engine.Call(factory,
                        JsValue.Undefined, new JsValue[] { host }).AsObject());
                    return new[] { localHelpers, localBridge };
                });
            }
            catch { Engine.Dispose(); throw; }
            helpers = initialized[0];
            bridge = initialized[1];
        }

        internal void Load(string entry, Func<Engine, ObjectInstance> createFields)
        {
            Run("load:" + entry, InitializationTimeoutMilliseconds, () =>
            {
                ObjectInstance exports = Require(entry, null).AsObject();
                JsValue constructor = AtStage("load:default-export", () => exports.Get("default"));
                instance = AtStage("load:constructor", () => Engine.Call(bridge.Get("create"), bridge,
                    new JsValue[] { constructor, 0 }).AsObject());
                ObjectInstance fields = AtStage("load:host-fields", () => createFields(Engine));
                AtStage("load:apply-fields", () => Engine.Call(bridge.Get("applyFields"), bridge,
                    new JsValue[] { instance, fields }));
                return JsValue.Undefined;
            });
        }

        internal void Invoke(string name, params JsValue[] arguments)
        {
            if (instance == null) return;
            Run("lifecycle:" + name, ExecutionTimeoutMilliseconds, () => Engine.Call(helpers.Get("invoke"), helpers,
                new JsValue[] { instance, name, new JsArray(Engine, arguments) }));
        }

        internal bool Resume(JsValue generator, out float seconds)
        {
            JsValue[] result = Run("coroutine:resume", ExecutionTimeoutMilliseconds, () =>
            {
                JsValue raw = Engine.Call(helpers.Get("next"), helpers, new[] { generator });
                var step = Engine.Call(helpers.Get("step"), helpers, new[] { raw }).AsObject();
                return new[] { step.Get("0"), step.Get("1") };
            });
            seconds = (float)result[1].AsNumber();
            return !result[0].AsBoolean();
        }

        internal void Close(JsValue generator)
        {
            Run("coroutine:cleanup", ExecutionTimeoutMilliseconds, () => Engine.Call(helpers.Get("close"), helpers, new[] { generator }));
        }

        private T Run<T>(string stage, int timeoutMilliseconds, Func<T> action)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TypeScriptVm));
            if (IsExecuting) throw new InvalidOperationException("Reentrant TypeScript host execution is not supported.");
            IsExecuting = true;
            budget.Begin(InstructionBudget, timeoutMilliseconds, stage);
            try
            {
                T result = action();
                budget.CheckTime();
                return result;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("TypeScript [" + budget.Describe() + "]: " + exception.Message, exception);
            }
            finally { budget.End(); IsExecuting = false; }
        }

        private T AtStage<T>(string stage, Func<T> action)
        {
            string previous = budget.Stage;
            budget.Stage = stage;
            T result = action();
            budget.CheckTime();
            // On failure retain the deepest stage for Run's diagnostic. Successful
            // sub-operations restore only the label, never the time/statement budget.
            budget.Stage = previous;
            return result;
        }

        private JsValue Require(string requested, string parent)
        {
            if (requested == "Kimchily.Script" || requested == "UnityEngine")
                return bridge.Get("modules").AsObject().Get(requested);
            string id = ResolveModule(requested, parent);
            if (!sources.TryGetValue(id, out string source))
                throw new InvalidOperationException("Module is not bundled: " + requested);
            if (loaded.TryGetValue(id, out ObjectInstance existing)) return existing.Get("exports");
            var module = new JsObject(Engine);
            module.Set("exports", new JsObject(Engine));
            loaded.Add(id, module); // CommonJS cycles see the partially initialized exports object.
            var require = new ClrFunction(Engine, "require", (_, args) =>
            {
                if (args.Length != 1 || !args[0].IsString()) throw new InvalidOperationException("require expects a bundled module name.");
                return Require(args[0].AsString(), id);
            });
            JsValue wrapper = AtStage("module:" + id + ":parse", () =>
                Engine.Evaluate("(function(module,exports,require){\n'use strict';\n" + source + "\n})", id + ".js"));
            AtStage("module:" + id + ":execute", () =>
                Engine.Call(wrapper, JsValue.Undefined, new JsValue[] { module, module.Get("exports"), require }));
            return module.Get("exports");
        }

        internal static string ResolveModule(string requested, string parent)
        {
            if (string.IsNullOrEmpty(requested) || requested.Length > 512 || requested.IndexOf('\\') >= 0 || requested.IndexOf(':') >= 0 || requested.StartsWith("/", StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid bundled module name.");
            string value = requested;
            if (value.StartsWith(".", StringComparison.Ordinal))
            {
                if (parent == null) throw new InvalidOperationException("Entry module must use its absolute bundle ID.");
                int slash = parent.LastIndexOf('/');
                value = (slash < 0 ? "" : parent.Substring(0, slash + 1)) + value;
            }
            var parts = new List<string>();
            foreach (string part in value.Split('/'))
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (parts.Count == 0) throw new InvalidOperationException("Module leaves bundle root.");
                    parts.RemoveAt(parts.Count - 1);
                }
                else if (part.Length == 0) throw new InvalidOperationException("Invalid bundled module name.");
                else parts.Add(part);
            }
            value = string.Join("/", parts);
            if (value.EndsWith(".js", StringComparison.Ordinal) || value.EndsWith(".ts", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 3);
            return value;
        }

        internal static void ValidateModules(Dictionary<string, string> modules)
        {
            if (modules.Count == 0 || modules.Count > MaximumModules) throw new InvalidOperationException("TypeScript bundle module count is out of range.");
            long total = 0;
            foreach (var pair in modules)
            {
                if (pair.Key == "UnityEngine" || pair.Key == "Kimchily.Script" || ResolveModule(pair.Key, null) != pair.Key)
                    throw new InvalidOperationException("Invalid or reserved TypeScript module ID: " + pair.Key);
                if (pair.Value == null || pair.Value.Length > MaximumModuleCharacters) throw new InvalidOperationException("TypeScript module exceeds the source limit.");
                total += pair.Value.Length;
            }
            if (total > MaximumTotalCharacters) throw new InvalidOperationException("TypeScript bundle exceeds the source limit.");
        }

        internal static int ClampBudget(int value) => Math.Max(MinimumBudget, Math.Min(MaximumBudget, value));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            instance = null;
            loaded.Clear();
            Engine.Dispose();
        }
    }
}
