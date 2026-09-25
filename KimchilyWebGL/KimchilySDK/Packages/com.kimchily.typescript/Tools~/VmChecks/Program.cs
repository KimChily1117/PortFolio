using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Kimchily.TypeScript;

internal static class Program
{
    private static readonly string Bootstrap = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Bootstrap.js.txt"));
    private const string Imports = "const {KimchilyScriptBehaviour,Event}=require('Kimchily.Script'); const {Debug,Vector3,Time,WaitForSeconds}=require('UnityEngine');\n";
    private static int passed;
    private sealed class Host
    {
        internal readonly List<string> Logs = new List<string>();
        internal readonly List<JsValue> Generators = new List<JsValue>();
        internal double[] Position = new double[3];
        internal string SlowOperation;
        internal int DelayMilliseconds;
        internal ObjectInstance Create(Engine engine)
        {
            var host = new JsObject(engine);
            host.Set("call", new ClrFunction(engine, "call", (_, args) =>
            {
                string op = args[0].AsString();
                if (op == SlowOperation) Thread.Sleep(DelayMilliseconds);
                var values = args[2].AsObject();
                switch (op)
                {
                    case "debug.log": Logs.Add(values.Get("0").AsString()); return JsValue.Undefined;
                    case "coroutine.start": Generators.Add(values.Get("0")); return Generators.Count;
                    case "time.deltaTime": return 0.25;
                    case "gameObject.getName": return "owner";
                    case "transform.getPosition": return new JsArray(engine, new JsValue[] { Position[0], Position[1], Position[2] });
                    case "transform.setPosition": for (int i=0;i<3;i++) Position[i]=values.Get(i.ToString()).AsNumber(); return JsValue.Undefined;
                    default: throw new InvalidOperationException("Unexpected test host operation: " + op);
                }
            }));
            return host;
        }
    }

    private static TypeScriptVm Create(string body, Host host, int budget = 20000, Dictionary<string,string> extra = null, Func<Engine,ObjectInstance> fields = null)
    {
        var modules = extra ?? new Dictionary<string,string>(StringComparer.Ordinal);
        modules.Add("Assets/Scripts/Main", Imports + body);
        var vm = new TypeScriptVm(modules, Bootstrap, host.Create, budget);
        try { vm.Load("Assets/Scripts/Main", fields ?? (engine => new JsObject(engine))); return vm; }
        catch { vm.Dispose(); throw; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static Exception Reject(Action action)
    {
        try { action(); } catch (Exception exception) { return exception; }
        throw new Exception("Expected a bounded rejection.");
    }
    private static void Case(string name, Action test)
    {
        test(); passed++; Console.WriteLine("PASS " + name);
    }
    private static void Main()
    {
        Case("allocation constraint follows target support while execution stays bounded", () => {
            using var vm = Create("exports.default=class extends KimchilyScriptBehaviour { Update(){while(true){}} };", new Host(), 1000);
            var allocationConstraint = vm.Engine.Constraints.Find<Jint.Constraints.MemoryLimitConstraint>();
#if UNITY_WEBGL && !UNITY_EDITOR
            Check(allocationConstraint == null, "Web players must not call the unsupported thread allocation counter");
#else
            Check(allocationConstraint != null, "Supported targets retain the allocation constraint");
#endif
            Exception failure = Reject(() => vm.Invoke("Update"));
            Check(failure.Message.Contains("budget exceeded") || failure.Message.Contains("maximum number of statements"), failure.Message);
        });
        Case("ES2015 class, Map, virtual modules and this lifecycle", () => {
            var host=new Host(); using var vm=Create("exports.default=class Main extends KimchilyScriptBehaviour { constructor(){super();this.values=new Map([['a',3]]);} Start(){Debug.Log(this.values.get('a')+':'+this.gameObject.name);} Update(dt){Debug.Log(dt+Time.deltaTime);} };",host);
            vm.Invoke("Start"); vm.Invoke("Update",0.5); Check(host.Logs[0]=="3:owner" && host.Logs[1]=="0.75","lifecycle values");
        });
        Case("bundled relative imports and default export", () => {
            var host=new Host(); using var vm=Create("const helper=require('./Helper'); exports.default=class extends KimchilyScriptBehaviour {Start(){Debug.Log(helper.value);}};",host,extra:new Dictionary<string,string>{{"Assets/Scripts/Helper","exports.value='helper';"}}); vm.Invoke("Start"); Check(host.Logs[0]=="helper","relative import");
        });
        Case("Inspector override leaves other initializers", () => {
            var host=new Host(); using var vm=Create("exports.default=class extends KimchilyScriptBehaviour {constructor(){super();this.speed=2;this.label='initial';}Start(){Debug.Log(this.speed+':'+this.label);}};",host,fields:engine=>{var all=new JsObject(engine);var b=new JsObject(engine);b.Set("type","number");b.Set("value",7);all.Set("speed",b);return all;}); vm.Invoke("Start"); Check(host.Logs[0]=="7:initial","field initializer");
        });
        Case("constructor can use explicit owner transform", () => {
            var host=new Host(); using var vm=Create("exports.default=class extends KimchilyScriptBehaviour {constructor(){super();this.transform.position=new Vector3(1,2,3);}};",host); Check(host.Position[2]==3,"constructor facade");
        });
        Case("generator null then WaitForSeconds then completion", () => {
            var host=new Host(); using var vm=Create("exports.default=class extends KimchilyScriptBehaviour {Start(){this.StartCoroutine(this.work());}*work(){yield null;yield new WaitForSeconds(.2);Debug.Log('done');}};",host); vm.Invoke("Start"); var g=host.Generators[0]; Check(vm.Resume(g,out float a)&&a==-1,"frame"); Check(vm.Resume(g,out float b)&&Math.Abs(b-.2)<.001,"seconds"); Check(!vm.Resume(g,out _)&&host.Logs[0]=="done","finish");
        });
        Case("cancel runs generator finally", () => {
            var host=new Host(); using var vm=Create("exports.default=class extends KimchilyScriptBehaviour {Start(){this.StartCoroutine(this.work());}*work(){try{yield null;}finally{Debug.Log('closed');}}};",host); vm.Invoke("Start"); vm.Resume(host.Generators[0],out _); vm.Close(host.Generators[0]); Check(host.Logs[0]=="closed","finally");
        });
        Case("generator delegation repeated", () => {
            var host=new Host(); using var vm=Create("exports.default=class extends KimchilyScriptBehaviour {Start(){this.StartCoroutine(this.work());}*part(){yield null;}*work(){for(let i=0;i<3;i++)yield* this.part();Debug.Log('delegated');}};",host);vm.Invoke("Start");for(int i=0;i<3;i++)Check(vm.Resume(host.Generators[0],out _),"delegate step");Check(!vm.Resume(host.Generators[0],out _)&&host.Logs[0]=="delegated","delegate complete");
        });
        Case("module instruction budget",()=>Reject(()=>Create("while(true){}",new Host(),1000)));
        Case("constructor instruction budget",()=>Reject(()=>Create("exports.default=class extends KimchilyScriptBehaviour{constructor(){super();while(true){}}};",new Host(),1000)));
        Case("lifecycle instruction budget",()=>{using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Update(){while(true){}}};",new Host());Reject(()=>vm.Invoke("Update"));});
        Case("lifecycle getter instruction budget",()=>{using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{get Update(){while(true){}}};",new Host());Reject(()=>vm.Invoke("Update"));});
        Case("generator step instruction budget",()=>{var host=new Host();using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Start(){this.StartCoroutine(this.work());}*work(){while(true){}}};",host);vm.Invoke("Start");Reject(()=>vm.Resume(host.Generators[0],out _));});
        Case("generator cleanup instruction budget",()=>{var host=new Host();using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Start(){this.StartCoroutine(this.work());}*work(){try{yield null;}finally{while(true){}}}};",host);vm.Invoke("Start");vm.Resume(host.Generators[0],out _);Reject(()=>vm.Close(host.Generators[0]));});
        Case("CLR, Node, blocking Atomics absent",()=>{var host=new Host();using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Start(){Debug.Log([typeof System,typeof importNamespace,typeof process,typeof Atomics,typeof SharedArrayBuffer].join(','));}};",host);vm.Invoke("Start");Check(host.Logs[0]=="undefined,undefined,undefined,undefined,undefined","global capability");});
        Case("eval compilation forbidden",()=>Reject(()=>Create("eval('1');exports.default=class extends KimchilyScriptBehaviour{};",new Host())));
        Case("Function compilation forbidden",()=>Reject(()=>Create("new Function('return 1')();exports.default=class extends KimchilyScriptBehaviour{};",new Host())));
        Case("external CommonJS module forbidden",()=>Reject(()=>Create("require('fs');exports.default=class extends KimchilyScriptBehaviour{};",new Host())));
        Case("path escape forbidden",()=>Reject(()=>TypeScriptVm.ResolveModule("../../../bad","Assets/Main")));
        Case("oversized source rejected before parse",()=>Reject(()=>new TypeScriptVm(new Dictionary<string,string>{{"Main",new string(' ',TypeScriptVm.MaximumModuleCharacters+1)}},Bootstrap,new Host().Create,20000)));
        Case("maximum budget cannot be bypassed",()=>{using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Update(){while(true){}}};",new Host(),int.MaxValue);Check(vm.InstructionBudget==100000,"budget upper clamp");Reject(()=>vm.Invoke("Update"));});
        Case("default export must extend SDK class",()=>Reject(()=>Create("exports.default=class {};",new Host())));
        Case("bundle error isolated from next engine",()=>{Reject(()=>Create("throw new Error('bad');",new Host()));var host=new Host();using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Start(){Debug.Log('fresh');}};",host);vm.Invoke("Start");Check(host.Logs[0]=="fresh","fresh engine");});
        Case("cold host bootstrap longer than frame limit succeeds",()=>{
            var host=new Host();
            using var vm=new TypeScriptVm(new Dictionary<string,string>{{"Main",Imports+"exports.default=class extends KimchilyScriptBehaviour{};"}},Bootstrap,engine=>{
                Thread.Sleep(TypeScriptVm.ExecutionTimeoutMilliseconds+100);
                return host.Create(engine);
            },20000);
            vm.Load("Main",engine=>new JsObject(engine));
            vm.Invoke("Start");
        });
        Case("cold constructor and Inspector host initialization succeed",()=>{
            var host=new Host{SlowOperation="transform.setPosition",DelayMilliseconds=TypeScriptVm.ExecutionTimeoutMilliseconds+100};
            using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{constructor(){super();this.transform.position=new Vector3(3,0,0);}};",host,
                fields:engine=>{Thread.Sleep(TypeScriptVm.ExecutionTimeoutMilliseconds+100);return new JsObject(engine);});
            Check(host.Position[0]==3,"slow constructor must complete");
        });
        Case("final slow frame host callback still exceeds frame time",()=>{
            var host=new Host{SlowOperation="debug.log",DelayMilliseconds=TypeScriptVm.ExecutionTimeoutMilliseconds+100};
            using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Update(){Debug.Log('slow final call');}};",host);
            Exception failure=Reject(()=>vm.Invoke("Update"));
            Check(failure.Message.Contains("stage=lifecycle:Update")&&failure.Message.Contains("time budget exceeded")&&failure.Message.Contains("timeLimitMs=100"),failure.Message);
        });
        Case("initialization keeps finite wall deadline and reports host stage",()=>{
            Exception failure=Reject(()=>new TypeScriptVm(new Dictionary<string,string>{{"Main",Imports+"exports.default=class extends KimchilyScriptBehaviour{};"}},Bootstrap,engine=>{
                Thread.Sleep(TypeScriptVm.InitializationTimeoutMilliseconds+100);
                return new Host().Create(engine);
            },20000));
            Check(failure.Message.Contains("stage=bootstrap:host")&&failure.Message.Contains("time budget exceeded")&&failure.Message.Contains("timeLimitMs=5000"),failure.Message);
        });
        Case("initialization infinite loop retains statement cap and module stage",()=>{
            Exception failure=Reject(()=>Create("while(true){}",new Host(),1000));
            Check(failure.Message.Contains("stage=module:Assets/Scripts/Main:execute")&&failure.Message.Contains("statements=")&&failure.Message.Contains("/1000")&&!failure.Message.Contains("time budget exceeded"),failure.Message);
        });
        Case("constructor error identifies constructor phase",()=>{
            Exception failure=Reject(()=>Create("exports.default=class extends KimchilyScriptBehaviour{constructor(){super();throw new Error('constructor failed');}};",new Host()));
            Check(failure.Message.Contains("stage=load:constructor")&&failure.Message.Contains("constructor failed"),failure.Message);
        });
        Case("cleanup error identifies cleanup phase",()=>{
            var host=new Host();using var vm=Create("exports.default=class extends KimchilyScriptBehaviour{Start(){this.StartCoroutine(this.work());}*work(){try{yield null;}finally{while(true){}}}};",host);
            vm.Invoke("Start");vm.Resume(host.Generators[0],out _);
            Exception failure=Reject(()=>vm.Close(host.Generators[0]));
            Check(failure.Message.Contains("stage=coroutine:cleanup")&&failure.Message.Contains("timeLimitMs=100"),failure.Message);
        });
        Console.WriteLine("Jint VM checks passed: "+passed);
    }
}
