using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

namespace Kimchily.Scripting
{
    /// <summary>
    /// Explicit tables/callbacks only: no CLR userdata, reflection registration, dynamic
    /// loaders, OS or IO modules. Every Lua entry point executes inside a budgeted coroutine.
    /// This limits Lua instructions, not total memory or native callback wall-clock time.
    /// </summary>
    internal sealed class LuaSandboxSession
    {
        internal const int MaxScriptCharacters = 262144;
        internal const int MaxRoutines = 32;
        private readonly Script script;
        private readonly int instructionBudget;
        private readonly Queue<DynValue> pending = new Queue<DynValue>();
        private int routineCount;
        private bool canWait;
        internal bool IsExecuting { get; private set; }
        internal bool AcceptingRoutines { get; set; } = true;
        internal int PendingCount => pending.Count;

        internal LuaSandboxSession(GameObject owner, LuaObjectReference[] references, int budget, Action<string> log)
        {
            // Serialized world data bypasses the public component setter.
            instructionBudget = Math.Max(100, Math.Min(100000, budget));
            script = new Script(CoreModules.Preset_HardSandbox);
            // These built-ins can call Lua again from inside CLR or perform an unbounded
            // allocation in a single VM instruction. Exclude them from this first API.
            Remove(script.Globals, "collectgarbage", "print");
            Remove(script.Globals.Get("string").Table, "gsub", "gmatch", "match", "find", "rep", "format", "dump");
            Remove(script.Globals.Get("table").Table, "sort");
            script.Globals.Set("self", DynValue.NewTable(ObjectFacade(owner)));
            var referenceTable = new Table(script);
            if (references != null)
            {
                foreach (LuaObjectReference reference in references)
                {
                    if (reference == null || string.IsNullOrWhiteSpace(reference.name) || reference.target == null)
                        continue;
                    if (!referenceTable.Get(reference.name).IsNil())
                        throw new ArgumentException("Duplicate Lua reference: " + reference.name);
                    referenceTable.Set(reference.name, DynValue.NewTable(ObjectFacade(reference.target)));
                }
            }
            script.Globals.Set("refs", DynValue.NewTable(referenceTable));
            SetCallback(script.Globals, "log", args =>
            {
                string message = args.Count > 0 ? args[0].ToPrintString() : string.Empty;
                log?.Invoke(message.Length > 2048 ? message.Substring(0, 2048) : message);
                return DynValue.Nil;
            });
            SetCallback(script.Globals, "start", args =>
            {
                if (!AcceptingRoutines) throw new ScriptRuntimeException("Cannot start a coroutine while disabled or shutting down.");
                DynValue function = args.AsType(0, "start", DataType.Function, false);
                if (routineCount >= MaxRoutines) throw new ScriptRuntimeException("Lua coroutine limit exceeded (32).");
                pending.Enqueue(NewCoroutine(function));
                routineCount++;
                return DynValue.Nil;
            });
            SetCallback(script.Globals, "wait_seconds", args =>
            {
                if (!canWait) throw new ScriptRuntimeException("wait_seconds is only valid inside start(function). ");
                double seconds = Number(args, 0, "wait_seconds");
                if (seconds < 0 || seconds > 86400) throw new ScriptRuntimeException("wait_seconds must be between 0 and 86400.");
                return DynValue.NewYieldReq(new[] { DynValue.NewNumber(seconds) });
            });
            SetCallback(script.Globals, "next_frame", args =>
            {
                if (!canWait) throw new ScriptRuntimeException("next_frame is only valid inside start(function).");
                return DynValue.NewYieldReq(new[] { DynValue.Nil });
            });
        }

        internal void Load(string source, string sourceName)
        {
            if (source == null || source.Length > MaxScriptCharacters)
                throw new ArgumentException("Lua script is missing or exceeds the 262144 character limit.");
            DynValue chunk = script.LoadString(source, null, sourceName);
            Resume(NewCoroutine(chunk), false);
        }

        internal void Invoke(string name, params DynValue[] arguments)
        {
            DynValue function = script.Globals.Get(name);
            if (function.IsNil()) return;
            if (function.Type != DataType.Function)
                throw new ScriptRuntimeException(name + " must be a Lua function.");
            Resume(NewCoroutine(function), false, arguments);
        }

        internal DynValue TakePending() => pending.Dequeue();
        internal void RoutineFinished() { if (routineCount > 0) routineCount--; }
        internal void CancelPending()
        {
            routineCount -= pending.Count;
            pending.Clear();
        }

        internal DynValue ResumeRoutine(DynValue coroutine) => Resume(coroutine, true);

        private DynValue NewCoroutine(DynValue function)
        {
            DynValue coroutine = script.CreateCoroutine(function);
            coroutine.Coroutine.AutoYieldCounter = instructionBudget;
            return coroutine;
        }

        private DynValue Resume(DynValue coroutine, bool allowWait, params DynValue[] arguments)
        {
            if (IsExecuting) throw new InvalidOperationException("Nested Lua execution is not supported.");
            IsExecuting = true;
            canWait = allowWait;
            try
            {
                DynValue result = coroutine.Coroutine.Resume(arguments);
                if (coroutine.Coroutine.State == CoroutineState.ForceSuspended)
                    throw new ScriptRuntimeException("Lua instruction budget exceeded (" + instructionBudget + ").");
                if (!allowWait && coroutine.Coroutine.State != CoroutineState.Dead)
                    throw new ScriptRuntimeException("Lifecycle functions cannot yield. Use start(function). ");
                return result;
            }
            finally
            {
                canWait = false;
                IsExecuting = false;
            }
        }

        private Table ObjectFacade(GameObject target)
        {
            var table = new Table(script);
            SetCallback(table, "position", args => Vector(Require(target).transform.position));
            SetCallback(table, "set_position", args =>
            {
                Require(target).transform.position = Vector(args, "set_position"); return DynValue.Nil;
            });
            SetCallback(table, "translate", args =>
            {
                Require(target).transform.Translate(Vector(args, "translate"), Space.World); return DynValue.Nil;
            });
            SetCallback(table, "rotate", args =>
            {
                Require(target).transform.Rotate(Vector(args, "rotate"), Space.Self); return DynValue.Nil;
            });
            SetCallback(table, "set_scale", args =>
            {
                Require(target).transform.localScale = Vector(args, "set_scale"); return DynValue.Nil;
            });
            SetCallback(table, "set_active", args =>
            {
                Require(target).SetActive(args.AsType(0, "set_active", DataType.Boolean, false).Boolean); return DynValue.Nil;
            });
            SetCallback(table, "is_active", args => DynValue.NewBoolean(Require(target).activeSelf));
            return table;
        }

        private DynValue Vector(Vector3 value)
        {
            var result = new Table(script);
            result.Set("x", DynValue.NewNumber(value.x));
            result.Set("y", DynValue.NewNumber(value.y));
            result.Set("z", DynValue.NewNumber(value.z));
            return DynValue.NewTable(result);
        }

        private static Vector3 Vector(CallbackArguments args, string method)
            => new Vector3((float)Number(args, 0, method), (float)Number(args, 1, method), (float)Number(args, 2, method));

        private static double Number(CallbackArguments args, int index, string method)
        {
            double value = args.AsType(index, method, DataType.Number, false).Number;
            if (double.IsNaN(value) || double.IsInfinity(value) || value > float.MaxValue || value < -float.MaxValue)
                throw new ScriptRuntimeException(method + " requires finite numbers.");
            return value;
        }

        private static GameObject Require(GameObject target)
        {
            if (target == null) throw new ScriptRuntimeException("Referenced object was destroyed.");
            return target;
        }

        private static void SetCallback(Table table, string name, Func<CallbackArguments, DynValue> callback)
            => table.Set(name, DynValue.NewCallback((context, args) => callback(args.SkipMethodCall()), name));

        private static void Remove(Table table, params string[] names)
        {
            foreach (string name in names) table.Set(name, DynValue.Nil);
        }
    }
}
