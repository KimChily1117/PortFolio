#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DnfRaidDemo;
namespace ProjectDawn.Combat
{
    public sealed class RaidContentState
    {
        public RaidSimulation Simulation;
        public readonly int[] PlayerIds;
        public readonly int BossId;
        public Dictionary<int,PlayerInput> Held = new Dictionary<int,PlayerInput>();
        public Dictionary<int,long> Lease = new Dictionary<int,long>();
        public RaidContentState(int[] playerIds,int bossId,bool playIntro=false)
        {
            if(playerIds==null||playerIds.Length<1||playerIds.Length>4||playerIds.Distinct().Count()!=playerIds.Length||playerIds.Contains(bossId))
                throw new ArgumentException("Invalid raid roster",nameof(playerIds));
            PlayerIds=(int[])playerIds.Clone();BossId=bossId;
            Simulation=new RaidSimulation(playerIds.Length,42) {AutoBuffs=true,IntroEnabled=playIntro,NetworkPatternGate=true,RoomsEnabled=true};Simulation.Start();
            // Entry size changes the roster, not the established four-player boss tuning.
            Simulation.Boss.Hp=Simulation.Boss.MaxHp=32500f;
        }
        public RaidContentState Clone() => new RaidContentState(PlayerIds,BossId) { Simulation=Simulation.Clone(),
            Held=new Dictionary<int,PlayerInput>(Held),Lease=new Dictionary<int,long>(Lease) };
        public int Slot(int id)=>Array.IndexOf(PlayerIds,id);
        public static PlayerCommand Command(PlayerInput i,int slot,bool edges=true)=>new PlayerCommand {
            PlayerId=slot, Move=new RaidVector(i.MoveX/1000f,i.MoveY/1000f),Skills=edges?i.Value:0,
            Basic=(i.Buttons&1)!=0,Run=(i.Buttons&2)!=0,Jump=edges&&(i.Buttons&4)!=0,Dash=edges&&(i.Buttons&8)!=0,Revive=edges&&(i.Buttons&16)!=0,Potion=edges&&(i.Buttons&32)!=0 };
    }
    public static class RaidContentRules
    {
        public const string Id="dawn-raid-countdown-v21";
        public const int ControlsDefinition=100;
        // Input -> server (160ms budget), then server -> host -> server (300ms,
        // including jitter and dispatch/replay stalls observed with four rendered clients).
        // Finality remains strictly after both ingress deadlines; personal visuals
        // are predicted immediately and never publish HP or damage to the server.
        public static TimingPolicy Timing()=>new TimingPolicy(16667,160000,300000,480000,0,maxRecords:262144);
        public static CombatRules Create()=>new CombatRules(new[]{new AttackDefinition(100,false,false,0,0)}
            .Concat(Enumerable.Range(0,9).Select(p=>new AttackDefinition(201+p,true,false,0,0))));
    }
    internal static class RaidContentStep
    {
        public static List<CombatEvent> Run(CombatState state,long tick,IReadOnlyList<InputReceipt> inputs,IReadOnlyList<HostProposal> proposals)
        {
            var c=state.Content!;var sim=c.Simulation;var result=new List<CombatEvent>();
            var commands=new Dictionary<int,PlayerCommand>();var claimed=new HashSet<InputKey>();
            if(sim.CinematicActive){c.Held.Clear();c.Lease.Clear();}
            foreach(var pair in c.Held)
                if(c.Lease[pair.Key]>=tick)commands[pair.Key]=RaidContentState.Command(pair.Value,c.Slot(pair.Key),false);
            int approved=-1;string program="",roomProgram="";bool roomApproved=false;
            foreach(var p in proposals.OrderBy(p=>p.Sequence))
            {
                bool valid=false;
                if(p.Actor==c.BossId && !p.Origin.HasValue)
                {
                    if(p.Definition==208) {
                        valid=sim.RoomsEnabled&&sim.PendingRoomMarkTick==tick&&!roomApproved&&BossPatternProgram.ValidRoomMarks(p.Program,sim.RoomSelectionRoll,sim.RoomSelectionAliveMask,sim.RoomSelectionPreviousMarkMask);
                        if(valid){roomApproved=true;roomProgram=p.Program;}
                    } else {
                    valid=sim.PendingPattern>=0 && sim.PendingPatternTick==tick && p.Definition==201+sim.PendingPattern && approved<0 && BossPatternProgram.Valid(p.Program,sim.PendingPattern,sim.SelectionOrdinal,sim.SelectionRoll,sim.SelectionAliveMask,sim.SelectionPreviousMarkMask,c.PlayerIds.Length);
                    if(valid){approved=p.Definition-201;program=p.Program;}
                    }
                }
                else if(p.Origin.HasValue && p.Definition==100 && c.Slot(p.Actor)>=0 && claimed.Add(p.Origin.Value))
                {
                    var r=inputs.FirstOrDefault(x=>x.Input.Key.Equals(p.Origin.Value)&&x.Input.Kind==InputKind.Controls);
                    valid=r!=null && !sim.Players[c.Slot(p.Actor)].Departed && !sim.Players[c.Slot(p.Actor)].Disconnected;
                    if(valid && r!=null && !sim.CinematicActive){c.Held[p.Actor]=r.Input;c.Lease[p.Actor]=tick+6;commands[p.Actor]=RaidContentState.Command(r.Input,c.Slot(p.Actor));}
                }
                result.Add(new CombatEvent(0,tick,valid?"ActionCommitted":"ActionRejected",p.Id,p.Origin));
            }
            foreach(var input in inputs)
                if(!claimed.Contains(input.Input.Key))result.Add(new CombatEvent(0,tick,"InputRejected",input.Input.Key.ToString(),input.Input.Key));
            sim.NetworkTick(commands.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray(),approved,program,roomProgram);
            if(sim.CinematicActive){c.Held.Clear();c.Lease.Clear();}
            int eventIndex=0;
            foreach(var e in sim.Events.Where(e=>e.Age==0))
            {
                int target=e.TargetId<0?c.BossId:c.PlayerIds[e.TargetId];
                int hp=(int)Math.Ceiling(e.TargetId<0?sim.Boss.Hp:sim.Players[e.TargetId].Hp);
                result.Add(new CombatEvent(0,tick,"Content"+e.Kind,$"content/{tick}/{eventIndex++}",null,target,
                    (int)Math.Ceiling(e.Value),hp));
            }
            for(int slot=0;slot<c.PlayerIds.Length;slot++)
            {
                var f=state.Fighters[c.PlayerIds[slot]];var p=sim.Players[slot];
                f.X=(int)(p.Position.x*1000);f.Hp=(int)Math.Ceiling(p.Hp);f.MaxHp=(int)Math.Ceiling(p.MaxHp);f.JumpHeight=(int)(p.JumpHeight*1000);
            }
            var boss=state.Fighters[c.BossId];boss.Hp=(int)Math.Ceiling(sim.Boss.Hp);boss.MaxHp=(int)Math.Ceiling(sim.Boss.MaxHp);boss.X=(int)(sim.Boss.Position.x*1000);
            return result;
        }
    }
}
