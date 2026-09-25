using System;
using System.Collections;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Kimchily.Creator;
using Kimchily.Creator.Content;
using UnityEngine;

namespace Kimchily.TypeScript
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoroutineScheduler))]
    [AddComponentMenu("Kimchily/TypeScript Behaviour")]
    public sealed class KimchilyTypeScriptBehaviour : MonoBehaviour, IWorldContentValidatable, IWorldScriptStatus
    {
        [SerializeField] private TypeScriptAsset scriptAsset;
        [SerializeField] private TypeScriptFieldBinding[] fields = Array.Empty<TypeScriptFieldBinding>();
        [SerializeField, Range(100, 100000)] private int instructionBudget = 20000;
        private TypeScriptVm vm;
        private CoroutineScheduler scheduler;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly Dictionary<int, Routine> routines = new Dictionary<int, Routine>();
        private readonly Queue<Routine> pending = new Queue<Routine>();
        private readonly Queue<Routine> cleanup = new Queue<Routine>();
        private int nextRoutine;
        private bool started;
        private bool pendingDisable;
        private bool pendingEnable;
        private bool destroying;
        private bool processing;
        private bool acceptingRoutines;
        private const int MaximumRoutines = 32;

        private sealed class Routine
        {
            internal int Id;
            internal JsValue Iterator;
            internal CoroutineHandle Handle;
            internal bool Cancelled;
            internal bool Finished;
            internal bool CleanupQueued;
        }

        public TypeScriptAsset ScriptAsset { get => scriptAsset; set => scriptAsset = value; }
        public TypeScriptFieldBinding[] Fields { get => fields; set => fields = value ?? Array.Empty<TypeScriptFieldBinding>(); }
        public int InstructionBudget { get => TypeScriptVm.ClampBudget(instructionBudget); set => instructionBudget = TypeScriptVm.ClampBudget(value); }
        public bool IsReady => vm != null && !IsFaulted && !destroying;
        public bool IsFaulted { get; private set; }
        public bool HasStarted { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            acceptingRoutines = true;
            if (vm != null && vm.IsExecuting) { pendingEnable = true; return; }
            if (Initialize() && isActiveAndEnabled) Invoke("OnEnable");
        }

        private void Start()
        {
            started = true;
            try
            {
                if (scriptAsset == null) Fault(new InvalidOperationException("A compiled TypeScript asset is required."));
                else if (Initialize() && isActiveAndEnabled) Invoke("Start");
            }
            finally { HasStarted = true; }
            DrainPending();
        }

        private void Update()
        {
            if (!IsReady) return;
            Invoke("Update", Time.deltaTime);
            DrainPending();
        }

        private void OnDisable()
        {
            acceptingRoutines = false;
            CancelAllRoutines();
            pendingDisable = true;
            ProcessDeferred();
        }

        private void OnDestroy()
        {
            destroying = true;
            acceptingRoutines = false;
            CancelAllRoutines();
            ProcessDeferred();
        }

        public void Reload()
        {
            if (vm != null && vm.IsExecuting) throw new InvalidOperationException("Cannot reload while TypeScript is executing.");
            acceptingRoutines = false;
            CancelAllRoutines();
            ProcessDeferred();
            if (vm != null)
            {
                Invoke("OnDestroy");
                vm.Dispose();
            }
            vm = null;
            objects.Clear();
            pendingDisable = false;
            pendingEnable = false;
            IsFaulted = false;
            LastError = null;
            HasStarted = false;
            acceptingRoutines = isActiveAndEnabled;
            if (!isActiveAndEnabled || !Initialize()) return;
            Invoke("OnEnable");
            if (started)
            {
                try { Invoke("Start"); }
                finally { HasStarted = true; }
            }
            DrainPending();
        }

        private bool Initialize()
        {
            if (vm != null) return IsReady;
            if (scriptAsset == null || IsFaulted || destroying) return false;
            scheduler = GetComponent<CoroutineScheduler>();
            try
            {
                foreach (string error in ValidateContent()) throw new InvalidOperationException(error);
                var modules = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (TypeScriptModule module in scriptAsset.modules) modules.Add(module.id, module.source);
                TextAsset bootstrap = Resources.Load<TextAsset>("Kimchily/TypeScript/Bootstrap.js");
                if (bootstrap == null) throw new InvalidOperationException("Kimchily TypeScript bootstrap resource is missing.");
                objects.Clear();
                objects.Add(gameObject);
                vm = new TypeScriptVm(modules, bootstrap.text, CreateHost, instructionBudget);
                vm.Load(scriptAsset.entryModule, CreateFields);
                Invoke("Awake");
                ProcessDeferred();
                return IsReady;
            }
            catch (Exception exception) { Fault(exception); return false; }
        }

        private ObjectInstance CreateHost(Engine engine)
        {
            var host = new JsObject(engine);
            host.Set("call", new ClrFunction(engine, "call", (_, args) => HostCall(engine, args)));
            return host;
        }

        private ObjectInstance CreateFields(Engine engine)
        {
            var result = new JsObject(engine);
            foreach (TypeScriptFieldBinding field in fields ?? Array.Empty<TypeScriptFieldBinding>())
            {
                if (!field.useOverride) continue;
                JsValue value;
                switch (field.kind)
                {
                    case "number": value = field.numberValue; break;
                    case "string": value = field.stringValue ?? ""; break;
                    case "boolean": value = field.boolValue; break;
                    case "Vector3": value = Vector(engine, field.vectorValue); break;
                    case "GameObject": value = ObjectId(field.gameObjectValue); break;
                    case "Transform": value = ObjectId(field.transformValue == null ? null : field.transformValue.gameObject); break;
                    default: throw new InvalidOperationException("Unsupported field type: " + field.kind);
                }
                var binding = new JsObject(engine);
                binding.Set("type", field.kind);
                binding.Set("value", value);
                result.Set(field.name, binding);
            }
            return result;
        }

        private JsValue ObjectId(GameObject value)
        {
            if (value == null) return JsValue.Null;
            int id = objects.IndexOf(value);
            if (id < 0) { id = objects.Count; objects.Add(value); }
            return id;
        }

        private JsValue HostCall(Engine engine, JsValue[] call)
        {
            if (call.Length != 3 || !call[0].IsString() || !call[1].IsNumber() || !call[2].IsArray())
                throw new InvalidOperationException("Invalid Kimchily host call.");
            string op = call[0].AsString();
            double rawId = call[1].AsNumber();
            if (!Finite(rawId) || rawId != Math.Truncate(rawId) || rawId < 0 || rawId >= objects.Count)
                throw new InvalidOperationException("Object reference is outside this behaviour's capabilities.");
            int id = (int)rawId;
            ObjectInstance args = call[2].AsObject();
            if (op == "time.deltaTime") return Time.deltaTime;
            if (op == "debug.log" || op == "debug.logWarning" || op == "debug.logError")
            {
                JsValue text = args.Get("0");
                if (!text.IsString()) throw new InvalidOperationException("Debug text must be a string.");
                string message = text.AsString();
                if (message.Length > 4096) message = message.Substring(0, 4096);
                string formatted = "[Kimchily TypeScript] " + message;
                if (op == "debug.logWarning") Debug.LogWarning(formatted, this);
                else if (op == "debug.logError") Debug.LogError(formatted, this);
                else Debug.Log(formatted, this);
                return JsValue.Undefined;
            }
            if (op == "coroutine.start") return QueueRoutine(args.Get("0"));
            if (op == "coroutine.stop")
            {
                double handle = Number(args, "0");
                if (handle == Math.Truncate(handle) && handle > 0 && handle <= int.MaxValue && routines.TryGetValue((int)handle, out Routine routine)) CancelRoutine(routine);
                return JsValue.Undefined;
            }
            if (op == "coroutine.stopAll") { CancelAllRoutines(); return JsValue.Undefined; }
            GameObject target = objects[id];
            if (target == null) throw new InvalidOperationException("Object reference was destroyed.");
            Transform transform = target.transform;
            switch (op)
            {
                case "gameObject.getName": return target.name;
                case "gameObject.setName":
                    JsValue nameValue = args.Get("0");
                    if (!nameValue.IsString() || nameValue.AsString().Length > 256) throw new InvalidOperationException("Object name must be at most 256 characters.");
                    target.name = nameValue.AsString(); break;
                case "gameObject.getActiveSelf": return target.activeSelf;
                case "gameObject.setActive":
                    JsValue active = args.Get("0");
                    if (!active.IsBoolean()) throw new InvalidOperationException("SetActive expects a boolean.");
                    target.SetActive(active.AsBoolean()); break;
                case "transform.getPosition": return Vector(engine, transform.position);
                case "transform.setPosition": transform.position = ReadVector(args); break;
                case "transform.getLocalPosition": return Vector(engine, transform.localPosition);
                case "transform.setLocalPosition": transform.localPosition = ReadVector(args); break;
                case "transform.getLocalScale": return Vector(engine, transform.localScale);
                case "transform.setLocalScale": transform.localScale = ReadVector(args); break;
                case "transform.getEulerAngles": return Vector(engine, transform.eulerAngles);
                case "transform.setEulerAngles": transform.eulerAngles = ReadVector(args); break;
                case "transform.translate": transform.Translate(ReadVector(args), ReadSpace(args)); break;
                case "transform.rotate": transform.Rotate(ReadVector(args), ReadSpace(args)); break;
                default: throw new InvalidOperationException("Host operation is not available: " + op);
            }
            return JsValue.Undefined;
        }

        private static JsValue Vector(Engine engine, Vector3 value) => new JsArray(engine, new JsValue[] { value.x, value.y, value.z });
        private static Vector3 ReadVector(ObjectInstance args) => new Vector3((float)Number(args, "0"), (float)Number(args, "1"), (float)Number(args, "2"));
        private static Space ReadSpace(ObjectInstance args)
        {
            double value = Number(args, "3");
            if (value != 0 && value != 1) throw new InvalidOperationException("Invalid transform space.");
            return value == 0 ? Space.World : Space.Self;
        }
        private static double Number(ObjectInstance args, string key)
        {
            JsValue value = args.Get(key);
            if (!value.IsNumber() || !Finite(value.AsNumber()) || Math.Abs(value.AsNumber()) > 10000000)
                throw new InvalidOperationException("A finite number within the SDK range is required.");
            return value.AsNumber();
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private void Invoke(string callback, params JsValue[] arguments)
        {
            if (vm == null || IsFaulted) return;
            try { vm.Invoke(callback, arguments); }
            catch (Exception exception) { Fault(exception); }
            ProcessDeferred();
        }

        private int QueueRoutine(JsValue iterator)
        {
            if (!acceptingRoutines || destroying || IsFaulted) throw new InvalidOperationException("This behaviour is not accepting coroutines.");
            if (!iterator.IsObject()) throw new InvalidOperationException("StartCoroutine expects a generator object.");
            if (routines.Count >= MaximumRoutines) throw new InvalidOperationException("Coroutine limit exceeded.");
            foreach (Routine existing in routines.Values)
                if (ReferenceEquals(existing.Iterator, iterator)) throw new InvalidOperationException("Generator is already running.");
            if (nextRoutine == int.MaxValue) throw new InvalidOperationException("Coroutine handle limit exceeded.");
            var routine = new Routine { Id = ++nextRoutine, Iterator = iterator };
            routines.Add(routine.Id, routine);
            pending.Enqueue(routine);
            return routine.Id;
        }

        private void DrainPending()
        {
            if (!IsReady || !isActiveAndEnabled) return;
            int count = pending.Count;
            for (int i = 0; i < count && IsReady && isActiveAndEnabled && pending.Count > 0; i++)
            {
                Routine routine = pending.Dequeue();
                if (routine.Finished || routine.Cancelled) continue;
                try { routine.Handle = scheduler.StartRoutine(RunRoutine(vm, routine), this); }
                catch (Exception exception) { FinishRoutine(routine, true); Fault(exception); }
            }
            ProcessDeferred();
        }

        private IEnumerator RunRoutine(TypeScriptVm owner, Routine routine)
        {
            bool completed = false;
            try
            {
                while (IsReady && vm == owner && isActiveAndEnabled && !routine.Cancelled)
                {
                    float seconds;
                    bool hasNext;
                    try { hasNext = owner.Resume(routine.Iterator, out seconds); }
                    catch (Exception exception) { Fault(exception); yield break; }
                    ProcessDeferred();
                    if (!hasNext) { completed = true; yield break; }
                    if (!IsReady || !isActiveAndEnabled || routine.Cancelled) yield break;
                    if (seconds < 0) yield return null;
                    else yield return new WaitForSeconds(seconds);
                }
            }
            finally
            {
                FinishRoutine(routine, !completed);
                ProcessDeferred();
            }
        }

        private void CancelRoutine(Routine routine)
        {
            if (routine.Finished || routine.Cancelled) return;
            routine.Cancelled = true;
            if (routine.Handle != null && scheduler != null) scheduler.Stop(routine.Handle);
            else FinishRoutine(routine, true);
        }

        private void CancelAllRoutines()
        {
            var snapshot = new List<Routine>(routines.Values);
            foreach (Routine routine in snapshot) CancelRoutine(routine);
            pending.Clear();
        }

        private void FinishRoutine(Routine routine, bool close)
        {
            if (routine.Finished) return;
            routine.Finished = true;
            routines.Remove(routine.Id);
            if (close && !routine.CleanupQueued)
            {
                routine.CleanupQueued = true;
                cleanup.Enqueue(routine);
            }
        }

        private void ProcessDeferred()
        {
            if (vm == null || vm.IsExecuting || processing) return;
            processing = true;
            try
            {
                bool wasAccepting = acceptingRoutines;
                acceptingRoutines = false;
                while (cleanup.Count > 0)
                {
                    Routine routine = cleanup.Dequeue();
                    try { vm.Close(routine.Iterator); }
                    catch (Exception exception) { Fault(exception); }
                }
                acceptingRoutines = wasAccepting && !IsFaulted && !destroying && isActiveAndEnabled;
                if (pendingDisable)
                {
                    pendingDisable = false;
                    Invoke("OnDisable");
                }
                if (pendingEnable && !destroying)
                {
                    pendingEnable = false;
                    if (isActiveAndEnabled) Invoke("OnEnable");
                }
                if (destroying)
                {
                    Invoke("OnDestroy");
                    vm.Dispose();
                    vm = null;
                    objects.Clear();
                }
            }
            finally { processing = false; }
        }

        private void Fault(Exception exception)
        {
            if (IsFaulted) return;
            IsFaulted = true;
            LastError = exception.Message;
            acceptingRoutines = false;
            CancelAllRoutines();
            Debug.LogError("[Kimchily TypeScript] " + (scriptAsset == null ? name : scriptAsset.name) + ": " + LastError, this);
        }

        public IEnumerable<string> ValidateContent()
        {
            var errors = new List<string>();
            if (scriptAsset == null) { errors.Add("A compiled TypeScript asset is required."); return errors; }
            if (scriptAsset.apiVersion != 1) errors.Add("Unsupported TypeScript SDK API version: " + scriptAsset.apiVersion);
            if (!scriptAsset.compiledSuccessfully) errors.Add("TypeScript asset has compiler errors.");
            if (string.IsNullOrEmpty(scriptAsset.className)) errors.Add("TypeScript entry must default-export a KimchilyScriptBehaviour class.");
            var modules = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TypeScriptModule module in scriptAsset.modules ?? Array.Empty<TypeScriptModule>())
            {
                if (module == null || string.IsNullOrEmpty(module.id)) { errors.Add("TypeScript module ID is missing."); continue; }
                if (modules.ContainsKey(module.id)) { errors.Add("Duplicate TypeScript module: " + module.id); continue; }
                modules.Add(module.id, module.source);
            }
            try { TypeScriptVm.ValidateModules(modules); }
            catch (Exception exception) { errors.Add(exception.Message); }
            if (string.IsNullOrEmpty(scriptAsset.entryModule) || !modules.ContainsKey(scriptAsset.entryModule)) errors.Add("TypeScript entry module is missing from the bundle.");
            var declared = new Dictionary<string, string>(StringComparer.Ordinal);
            TypeScriptField[] declarations = scriptAsset.fields ?? Array.Empty<TypeScriptField>();
            if (declarations.Length > TypeScriptVm.MaximumFields) errors.Add("Too many TypeScript fields.");
            foreach (TypeScriptField field in declarations)
            {
                if (field == null || !ValidFieldName(field.name) || !ValidKind(field.kind)) { errors.Add("Invalid TypeScript field declaration."); continue; }
                if (declared.ContainsKey(field.name)) errors.Add("Duplicate TypeScript field: " + field.name);
                else declared.Add(field.name, field.kind);
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            if ((fields?.Length ?? 0) > TypeScriptVm.MaximumFields) errors.Add("Too many TypeScript field bindings.");
            foreach (TypeScriptFieldBinding field in fields ?? Array.Empty<TypeScriptFieldBinding>())
            {
                if (field == null || !ValidFieldName(field.name) || !ValidKind(field.kind)) { errors.Add("Invalid TypeScript field binding."); continue; }
                if (!names.Add(field.name)) errors.Add("Duplicate TypeScript field binding: " + field.name);
                if (!declared.TryGetValue(field.name, out string kind) || kind != field.kind) errors.Add("TypeScript field binding does not match its declaration: " + field.name);
                if (!field.useOverride) continue;
                if (field.kind == "number" && !Finite(field.numberValue)) errors.Add("TypeScript number field must be finite: " + field.name);
                if (field.kind == "Vector3" && (!Finite(field.vectorValue.x) || !Finite(field.vectorValue.y) || !Finite(field.vectorValue.z))) errors.Add("TypeScript vector field must be finite: " + field.name);
                if (field.kind == "string" && (field.stringValue?.Length ?? 0) > 65536) errors.Add("TypeScript string field exceeds the length limit: " + field.name);
            }
            return errors;
        }

        private static bool ValidKind(string kind) => kind == "number" || kind == "string" || kind == "boolean" || kind == "GameObject" || kind == "Transform" || kind == "Vector3";
        private static bool ValidFieldName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 80) return false;
            switch (name)
            {
                case "__proto__": case "constructor": case "prototype": case "gameObject": case "transform":
                case "StartCoroutine": case "StopCoroutine": case "StopAllCoroutines": case "Awake":
                case "OnEnable": case "Start": case "Update": case "OnDisable": case "OnDestroy": return false;
            }
            if (!AsciiLetter(name[0]) && name[0] != '_' && name[0] != '$') return false;
            for (int i = 1; i < name.Length; i++) if (!AsciiLetter(name[i]) && (name[i] < '0' || name[i] > '9') && name[i] != '_' && name[i] != '$') return false;
            return true;
        }
        private static bool AsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
