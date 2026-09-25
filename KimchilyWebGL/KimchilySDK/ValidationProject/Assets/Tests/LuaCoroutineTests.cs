using System.Collections;
using System.IO;
using NUnit.Framework;
using Kimchily.Creator;
using UnityEngine;
using UnityEngine.TestTools;
using XLua;

namespace Kimchily.Validation.Tests
{
    public sealed class LuaCoroutineTests
    {
        GameObject host;
        CoroutineScheduler scheduler;
        LuaEnv environment;
        LuaTable api;

        [UnityTest]
        public IEnumerator ExistingXLuaGeneratorRunsThroughSchedulerAndInvokesExplicitCleanup()
        {
            host = new GameObject("Lua coroutine verification");
            scheduler = host.AddComponent<CoroutineScheduler>();
            environment = new LuaEnv();
            // Keep this legacy Windows/xLua interoperability fixture self-contained.
            // It verifies the existing cs_generator contract, not the Web TypeScript runtime.
            string utilPath = Path.Combine(Application.dataPath, "Fixtures/XLua/util.lua");
            Assert.IsTrue(File.Exists(utilPath), "The local xLua util fixture is missing.");
            environment.Global.Set("kimchily_util", environment.DoString(File.ReadAllText(utilPath))[0]);
            environment.Global.Set("scheduler", scheduler);
            environment.Global.Set("owner", host);
            environment.DoString(@"
                    local state = { resumed = false, cleaned = false }
                    api = state
                    state.run = function()
                        local routine = kimchily_util.cs_generator(function()
                            coroutine.yield(CS.UnityEngine.WaitForSecondsRealtime(0.01))
                            state.resumed = true
                            coroutine.yield(nil)
                        end)
                        return scheduler:StartRoutine(
                            CS.Kimchily.Creator.CoroutineRoutine.WithCleanup(routine,
                                function() state.cleaned = true end), owner)
                    end
            ");
            api = environment.Global.Get<LuaTable>("api");
            CoroutineHandle handle = environment.DoString("return api.run()")[0] as CoroutineHandle;
            Assert.IsNotNull(handle);
            yield return handle;
            Assert.AreEqual(CoroutineStatus.Completed, handle.Status);
            Assert.IsTrue(api.Get<bool>("resumed"));
            Assert.IsTrue(api.Get<bool>("cleaned"));
        }

        [UnityTearDown]
        public IEnumerator ReleaseLuaEnvironmentAfterCallbacksLeaveTheFrame()
        {
            if (scheduler != null) scheduler.CancelAll();
            api?.Dispose();
            api = null;
            if (environment != null)
            {
                environment.Global.Set<string, object>("scheduler", null);
                environment.Global.Set<string, object>("owner", null);
                environment.DoString("api = nil; kimchily_util = nil; collectgarbage('collect')");
            }
            if (host != null) Object.Destroy(host);
            host = null;
            scheduler = null;
            // Unity Mono can keep callback temporaries alive on the current
            // native/managed stack. Dispose only after that frame has unwound.
            yield return null;
            if (environment != null)
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                environment.Tick();
                environment.Dispose();
                environment = null;
            }
        }
    }
}
