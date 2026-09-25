using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;

internal static class Program
{
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private static int Main(string[] arguments)
    {
        if (arguments.Length == 2)
        {
            Assembly original = new AssemblyLoadContext("upstream").LoadFromAssemblyPath(Path.GetFullPath(arguments[0]));
            Assembly rebuilt = new AssemblyLoadContext("source-build").LoadFromAssemblyPath(Path.GetFullPath(arguments[1]));
            Require(original.FullName == rebuilt.FullName, "assembly name/version/public key preserved");
            string[] before = PublicSurface(original);
            string[] after = PublicSurface(rebuilt);
            Require(before.SequenceEqual(after), "public API preserved (" + before.Length + " declarations)");
        }
        var script = new Script(CoreModules.Preset_HardSandbox);
        var loader = script.Options.ScriptLoader as UnityAssetsScriptLoader;
        Require(loader != null && loader.GetLoadedScripts().Length == 0,
            "first Script initialization uses the empty in-memory loader");
        Require(!loader.ScriptFileExists("../../outside.lua"), "memory loader cannot resolve filesystem paths");
        DynValue loop = script.CreateCoroutine(script.LoadString("while true do end"));
        loop.Coroutine.AutoYieldCounter = 1000;
        loop.Coroutine.Resume();
        Require(loop.Coroutine.State == CoroutineState.ForceSuspended, "infinite loop preempted");

        script.Globals.Set("wait_seconds", DynValue.NewCallback((context, args) =>
            DynValue.NewYieldReq(new[] { args[0] })));
        script.Globals.Set("next_frame", DynValue.NewCallback((context, args) =>
            DynValue.NewYieldReq(new[] { DynValue.Nil })));
        DynValue routine = script.CreateCoroutine(script.LoadString("wait_seconds(0.25); next_frame(); return 42"));
        routine.Coroutine.AutoYieldCounter = 1000;
        DynValue first = routine.Coroutine.Resume();
        Require(first.Type == DataType.Number && first.Number == 0.25 && routine.Coroutine.State == CoroutineState.Suspended,
            "explicit CLR yield returns seconds");
        DynValue second = routine.Coroutine.Resume();
        Require(second.IsNil() && routine.Coroutine.State == CoroutineState.Suspended, "next-frame yield returns nil");
        DynValue third = routine.Coroutine.Resume();
        Require(third.Number == 42 && routine.Coroutine.State == CoroutineState.Dead, "coroutine completes after resume");
        foreach (string blocked in new[] { "io", "os", "load", "require", "debug", "coroutine", "pcall", "xpcall", "CS", "clr" })
            Require(script.Globals.Get(blocked).IsNil(), "hard sandbox omits " + blocked);
        return 0;
    }

    private static string[] PublicSurface(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        return assembly.GetExportedTypes().SelectMany(type => new[] { "TYPE " + type.FullName }
            .Concat(type.GetMembers(flags).Select(member => type.FullName + " " + member.MemberType + " " + member)))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
