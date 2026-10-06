using System.Diagnostics;
using System.Text.Json;
using Kimchily.Server.Core;

static class ScriptHostChecks
{
    public static async Task Run(Action<bool, string> assert, Action<string> passed)
    {
        var root = PortalChecks.Root;
        var scratch = Path.Combine(root, "Artifacts", "script-checks", Guid.NewGuid().ToString("N"));
        var games = Path.Combine(scratch, "games");
        Directory.CreateDirectory(games);
        var catalog = new ApprovedScriptCatalog(games);
        long now = 0;
        var hub = new RoomHub(() => now, catalog);

        async Task Send(FakePeer peer, ClientCommand command)
        {
            var task = hub.HandleAsync(peer, command);
            hub.Flush();
            await task;
        }

        ClientCommand Join(string room, string world = "script-test")
        {
            return new ClientCommand()
            {
                ProtocolVersion = 1,
                Type = "join",
                Name = "TS 확인",
                WorldId = world,
                RevisionId = "r1",
                RoomId = room
            };
        }

        ClientCommand Command(ScriptBundle bundle, string action, string? payload = null)
        {
            return new ClientCommand()
            {
                ProtocolVersion = 1,
                Type = "game",
                ScriptId = bundle.ScriptId,
                ScriptHash = bundle.ScriptHash,
                Action = action,
                PayloadJson = payload
            };
        }

        ScriptGameState Latest(FakePeer peer)
        {
            return peer.Events.Last(item => item.Game is not null).Game!;
        }

        JsonElement Data(FakePeer peer)
        {
            return JsonDocument.Parse(Latest(peer).StateJson).RootElement;
        }

        void Tick()
        {
            hub.TryQueueGameTick();
            hub.Flush();
        }

        ScriptBundle Fixture(string id, string javascript, string world = "script-test")
        {
            var hash = ApprovedScriptCatalog.Hash(javascript);
            var bundle = new ScriptBundle(1, id, world, hash, hash, "test-fixture", javascript);
            var directory = Path.Combine(games, id);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, hash + ".json"), JsonSerializer.Serialize(bundle, Protocol.Json));
            return bundle;
        }

        // 아예 다른 게임의 state/action을 사용한다. C#에는 이 필드명이나 행동을 추가하지 않는다.
        var counter = Fixture(
            "counter",
            "exports.create=()=>({count:0}); exports.reduce=(s,i)=>{if(i.kind==='command'){if(i.action!=='add')throw new Error('bad');return {count:s.count+i.payload.amount};}return null;};");
        var peer = new FakePeer("script-a");
        await Send(peer, Join("counter"));
        await Send(peer, Command(counter, "watch"));
        assert(Data(peer).GetProperty("count").GetInt32() == 0, "Generic state did not initialize.");
        var firstVersion = Latest(peer).Version;
        await Send(peer, Command(counter, "watch"));
        assert(Latest(peer).Version == firstVersion, "Unchanged generic state acquired a new version.");
        await Send(peer, Command(counter, "add", "{\"amount\":7}"));
        assert(
            Data(peer).GetProperty("count").GetInt32() == 7
            && Latest(peer).Version == firstVersion + 1,
            "Generic command did not run in JS.");
        passed("The same C# host runs an unrelated counter script with arbitrary JSON state and commands");

        var different = Fixture("counter-v2", "exports.create=()=>({other:true});exports.reduce=()=>null;");
        await Send(peer, Command(different, "watch"));
        assert(
            peer.Events.Last().Code == "GAME_SCRIPT_MISMATCH"
            && Latest(peer).ScriptHash == counter.ScriptHash,
            "Room accepted a different script identity.");
        await Send(peer, Command(counter, "watch")with { ScriptHash = new string ('a', 64) });
        assert(peer.Events.Last().Code == "GAME_SCRIPT_MISMATCH", "Room accepted another script hash.");
        var foreign = new FakePeer("foreign");
        await Send(foreign, Join("foreign", "wrong-world"));
        await Send(foreign, Command(counter, "watch"));
        assert(foreign.Events.Last().Code == "GAME_NOT_APPROVED", "Approved script escaped its assigned world.");
        await Send(foreign, Command(counter, "watch")with { ScriptId = "../counter" });
        assert(foreign.Events.Last().Code == "INVALID_GAME", "Path traversal identifier accepted.");
        passed("Script identity and immutable hash stay bound to one world and room; traversal is rejected");

        var tampered = Fixture("tamper", "exports.create=()=>({ok:true});exports.reduce=()=>null;");
        File.WriteAllText(
            Path.Combine(games, tampered.ScriptId, tampered.ScriptHash + ".json"),
            JsonSerializer.Serialize(tampered with { Javascript = tampered.Javascript + " // changed" }, Protocol.Json));
        var corruptPeer = new FakePeer("corrupt");
        await Send(corruptPeer, Join("corrupt"));
        await Send(corruptPeer, Command(tampered, "watch"));
        assert(corruptPeer.Events.Last().Code == "GAME_NOT_APPROVED", "Content hash was not verified.");
        passed("Changing a registered JavaScript file without a matching hash is rejected before execution");

        await Send(peer, Command(counter, "add", new string ('x', 1025)));
        assert(peer.Events.Last().Code == "INVALID_GAME_PAYLOAD", "Oversized command payload accepted.");
        await Send(peer, Command(counter, "add", "{"));
        assert(peer.Events.Last().Code == "INVALID_GAME_PAYLOAD", "Malformed payload accepted.");
        await Send(
            peer,
            Command(
            counter,
            "add",
            JsonSerializer.Serialize(
            new string ('한', 400),
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
        assert(peer.Events.Last().Code == "INVALID_GAME_PAYLOAD", "UTF-8 bytes were confused with character count.");
        var beforeRejected = Latest(peer).Version;
        await Send(peer, Command(counter, "finish"));
        assert(
            peer.Events.Any(item => item.Code == "GAME_COMMAND_REJECTED")
            && Latest(peer).Version == beforeRejected,
            "Rejected action changed state.");
        await Send(peer, Command(counter, "add", "{\"amount\":1}"));
        assert(Data(peer).GetProperty("count").GetInt32() == 8, "Rejected command broke subsequent valid commands.");
        await Send(peer, Command(counter, "add", "{\"amount\":1,\"ignored\":" + new string ('[', 10) + "0" + new string (']', 10) + "}"));
        assert(Data(peer).GetProperty("count").GetInt32() == 9, "A valid nested payload failed when wrapped in the script input.");
        passed("Malformed or oversized JSON payloads are rejected and a script-rejected action cannot corrupt state");

        var hidden = Fixture(
            "capabilities",
            "exports.create=()=>({clr:typeof System,node:typeof require,process:typeof process,atomics:typeof Atomics});exports.reduce=()=>null;");
        var hiddenPeer = new FakePeer("hidden");
        await Send(hiddenPeer, Join("hidden"));
        await Send(hiddenPeer, Command(hidden, "watch"));
        assert(
            Data(hiddenPeer).EnumerateObject().All(value => value.Value.GetString() == "undefined"),
            "Native or Node capability leaked into script.");
        passed("Server scripts receive JSON capabilities without CLR, Node, require or blocking Atomics globals");

        var runaway = Fixture(
            "runaway",
            "exports.create=()=>({ok:true});exports.reduce=(s,i)=>{if(i.kind==='tick'){while(true){}}return null;};");
        var broken = new FakePeer("broken");
        await Send(broken, Join("broken"));
        await Send(broken, Command(runaway, "watch"));
        now += 100;
        Tick();
        assert(broken.Events.Last().Code == "GAME_SCRIPT_FAULT", "Runaway script did not fault.");
        await Send(peer, new ClientCommand() { ProtocolVersion = 1, Type = "chat", Text = "다른 방은 계속 동작" });
        assert(peer.Events.Last().Type == "chat", "Script exception escaped and stopped the room pump.");
        await Send(broken, Command(runaway, "watch"));
        assert(broken.Events.Last().Code == "GAME_SCRIPT_FAULT", "Faulted room restarted runaway VM.");
        passed("Execution limits stop a runaway game while another room still processes chat");

        var oversized = Fixture(
            "oversized",
            "exports.create=()=>({ok:true});exports.reduce=(s,i)=>i.kind==='tick'?{large:'x'.repeat(17000)}:null;");
        var large = new FakePeer("large");
        await Send(large, Join("large"));
        await Send(large, Command(oversized, "watch"));
        var lastGood = Latest(large);
        now += 100;
        Tick();
        assert(
            large.Events.Last().Code == "GAME_SCRIPT_FAULT"
            && Latest(large) == lastGood,
            "Oversized output replaced the last good state.");
        passed("Output JSON is bounded and a fault preserves the last valid snapshot");

        var deep = Fixture(
            "too-deep",
            "exports.create=()=>({ok:true});exports.reduce=(s,i)=>{if(i.kind!=='tick')return null;let r={};for(let n=0;n<15;n++)r={nested:r};return r;};");
        var deepPeer = new FakePeer("deep");
        await Send(deepPeer, Join("deep"));
        await Send(deepPeer, Command(deep, "watch"));
        now += 100;
        Tick();
        assert(deepPeer.Events.Last().Code == "GAME_SCRIPT_FAULT", "Excessively nested state was accepted.");
        var wide = Fixture(
            "too-wide",
            "exports.create=()=>({ok:true});exports.reduce=(s,i)=>i.kind==='tick'?{items:new Array(1100).fill(0)}:null;");
        var widePeer = new FakePeer("wide");
        await Send(widePeer, Join("wide"));
        await Send(widePeer, Command(wide, "watch"));
        now += 100;
        Tick();
        assert(widePeer.Events.Last().Code == "GAME_SCRIPT_FAULT", "Excessive JSON values were accepted.");
        passed("The host rejects excessive JSON nesting and value counts independently of output byte size");
        // 실제 TS 소스의 설정만 3→5로 변경한 별도 번들을 빌드한다. 배포 중인 identity 파일은 건드리지 않는다.
        var originalSource = File.ReadAllText(Path.Combine(root, "../KimchilyCreator/ServerScripts/chili-portal/PortalRules.ts"));
        assert(originalSource.Contains("holdSeconds: 3,"), "Five-second fixture could not locate the authored configuration.");
        var alternateSource = Path.Combine(scratch, "PortalFive.ts");
        File.WriteAllText(alternateSource, originalSource.Replace("holdSeconds: 3,", "holdSeconds: 5,", StringComparison.Ordinal));
        var compilerRoot = Path.GetFullPath(Path.Combine(root, "../KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler"));
        using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(compilerRoot, ".tools/ready.json")));
        var node = Path.Combine(compilerRoot, ".tools", marker.RootElement.GetProperty("nodeRelativePath").GetString()!);
        var start = new ProcessStartInfo(node)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in new[]
        {
            Path.Combine(root, "tools/compile-script.cjs"),
            "--source",
            alternateSource,
            "--id",
            "portal-five-seconds",
            "--world",
            "script-test",
            "--output",
            games
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        assert(process.ExitCode == 0, "Alternate TS compile failed: " + await stderr);
        using var result = JsonDocument.Parse(await stdout);
        var alternateHash = result.RootElement.GetProperty("scriptHash").GetString()!;
        var five = catalog.Load("portal-five-seconds", alternateHash, "script-test");
        var alternate = new FakePeer("five");
        await Send(alternate, Join("five"));
        await Send(alternate, Command(five, "watch"));
        await Send(alternate, Command(five, "start"));
        var began = now;

        for (var index = 0; index <= 10; index++)
        {
            now = began + index * 500;
            await Send(
                alternate,
                new ClientCommand()
            {
                ProtocolVersion = 1,
                Type = "state",
                State = new PlayerState(index + 1, -3, .15f, 2, 0, 0, true, 0)
            });
            Tick();
            var state = Data(alternate);
            assert(state.GetProperty("holdSeconds").GetInt32() == 5, "Host overrode the TS duration.");
            assert(
                state.GetProperty("phase").GetString() == (index == 10 ? "complete" : "holding"),
                "Modified TS rule did not require exactly five seconds.");
        }

        assert(alternateHash != PortalChecks.ScriptHash, "Rule change did not change script identity.");
        passed("Changing only TypeScript holdSeconds from 3 to 5 changes victory timing without editing or rebuilding C#");
    }
}
