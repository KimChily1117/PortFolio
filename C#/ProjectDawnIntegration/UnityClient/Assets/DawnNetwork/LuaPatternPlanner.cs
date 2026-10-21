using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using MoonSharp.Interpreter;
using DnfRaidDemo;

namespace ProjectDawn.Network
{
    // No CLR userdata, filesystem, require, OS, random, or Unity APIs are exposed.
    // Coroutine instruction budget covers initialization and each callback; failure never falls back to C# host AI.
    public sealed class LuaPatternPlanner
    {
        private readonly Script vm = new Script(CoreModules.None);
        private bool canEmit;
        private int emitted, pattern;
        private long tick;
        private readonly List<string> actions = new List<string>();
        public int Requests {get;private set;}
        public int Confirmations {get;private set;}
        public LuaPatternPlanner(string source = null)
        {
            if(source==null)using(var stream=typeof(LuaPatternPlanner).Assembly.GetManifestResourceStream("ProjectDawn.bakal_patterns.lua"))
            { if(stream==null)throw new InvalidOperationException("Missing host Lua script");using(var reader=new StreamReader(stream))source=reader.ReadToEnd(); }
            if(source.Length>65536)throw new InvalidDataException("Lua script size");
            vm.Globals.Set("emit_pattern",DynValue.NewCallback((context,args)=>{
                if(!canEmit || ++emitted!=1)throw new InvalidOperationException("Unexpected pattern callback");
                double p=args[0].Number,t=args[1].Number;
                if(args[0].Type!=DataType.Number || args[1].Type!=DataType.Number || p<0 || p>8 || p!=Math.Floor(p) || t<1 || t>int.MaxValue || t!=Math.Floor(t))
                    throw new InvalidDataException("Invalid Lua proposal");
                pattern=(int)p;tick=(long)t;return DynValue.Nil;
            }));
            vm.Globals.Set("emit_action",DynValue.NewCallback((context,args)=>{
                if(!canEmit || emitted!=1 || actions.Count>=7)throw new InvalidOperationException("Unexpected action callback");
                int[] values=new int[3];
                for(int i=0;i<3;i++) {
                    double value=args[i].Number;
                    if(args[i].Type!=DataType.Number || double.IsNaN(value) || double.IsInfinity(value) || value<0 || value>int.MaxValue || value!=Math.Floor(value))
                        throw new InvalidDataException("Invalid Lua action");
                    values[i]=(int)value;
                }
                if(values[0]>4 || values[1]>600 || values[2]>3)throw new InvalidDataException("Lua action outside wire bounds");
                actions.Add(values[0].ToString(CultureInfo.InvariantCulture)+","+values[1].ToString(CultureInfo.InvariantCulture)+","+values[2].ToString(CultureInfo.InvariantCulture));
                return DynValue.Nil;
            }));
            Run(vm.LoadString(source));
        }
        private DynValue Run(DynValue function,params DynValue[] args)
        {
            if(function.Type!=DataType.Function)throw new InvalidDataException("Missing Lua callback");
            var coroutine=vm.CreateCoroutine(function).Coroutine;coroutine.AutoYieldCounter=20000;
            var result=coroutine.Resume(args);
            if(coroutine.State!=CoroutineState.Dead)throw new InvalidOperationException("Lua callback instruction budget exceeded");
            return result;
        }
        public (int Pattern,long Tick,string Program) Request(RaidSimulation simulation)
        {
            var context=new Table(vm);
            context.Set("player_count",DynValue.NewNumber(simulation.Players.Count));
            context.Set("roll",DynValue.NewNumber(simulation.SelectionRoll));context.Set("phase",DynValue.NewNumber(simulation.SelectionPhase));
            context.Set("previous",DynValue.NewNumber(simulation.SelectionPrevious));context.Set("ordinal",DynValue.NewNumber(simulation.SelectionOrdinal));
            context.Set("start_tick",DynValue.NewNumber(simulation.PendingPatternTick));
            context.Set("alive_mask",DynValue.NewNumber(simulation.SelectionAliveMask));
            context.Set("previous_mark_mask",DynValue.NewNumber(simulation.SelectionPreviousMarkMask));
            return RequestCallback("on_pattern_requested",context,2);
        }
        public (int Pattern,long Tick,string Program) RequestRoom(RaidSimulation simulation)
        {
            var context=new Table(vm);
            context.Set("roll",DynValue.NewNumber(simulation.RoomSelectionRoll));
            context.Set("alive_mask",DynValue.NewNumber(simulation.RoomSelectionAliveMask));
            context.Set("previous_mark_mask",DynValue.NewNumber(simulation.RoomSelectionPreviousMarkMask));
            context.Set("start_tick",DynValue.NewNumber(simulation.PendingRoomMarkTick));
            return RequestCallback("on_room_mark_requested",context,1);
        }
        private (int Pattern,long Tick,string Program) RequestCallback(string callback,Table context,int minimumActions)
        {
            emitted=0;actions.Clear();canEmit=true;
            try {Run(vm.Globals.Get(callback),DynValue.NewTable(context));}
            finally {canEmit=false;}
            if(emitted!=1)throw new InvalidOperationException("Lua must emit exactly one proposal");
            if(actions.Count<minimumActions)throw new InvalidOperationException("Lua must emit an action program");
            Requests++;return (pattern,tick,string.Join(";",actions));
        }
        public void Confirm(int selected,long at)
        {
            var context=new Table(vm);context.Set("pattern",DynValue.NewNumber(selected));context.Set("tick",DynValue.NewNumber(at));
            Run(vm.Globals.Get("on_pattern_confirmed"),DynValue.NewTable(context));Confirmations++;
        }
    }
}
