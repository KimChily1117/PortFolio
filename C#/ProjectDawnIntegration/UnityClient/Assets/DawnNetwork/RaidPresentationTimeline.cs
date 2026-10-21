using System;
using System.Collections.Generic;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
using SimEvent=DnfRaidDemo.CombatEvent;

namespace ProjectDawn.Network
{
    // Display-only projection of server-confirmed schedules. Never publishes combat state.
    public sealed class RaidPresentationTimeline
    {
        private sealed class LocalInput {public long Sequence,Tick;public PlayerCommand Command;public bool Rejected;}
        private readonly int slot;
        private readonly Dictionary<long,LocalInput> inputs=new Dictionary<long,LocalInput>();
        private readonly HashSet<int> activated=new HashSet<int>();
        private readonly HashSet<string> feedback=new HashSet<string>();
        public const int MaxProjectionTicks=60;
        public event Action<SimEvent> Feedback;
        public event Action<AttackZone,long,long,PlayerState> Activated;
        public int DisplayTick {get;private set;}
        public bool Clamped {get;private set;}
        public RaidPresentationTimeline(int localSlot){slot=localSlot;}
        public void Track(long sequence,long tick,PlayerCommand command)
        {
            inputs[sequence]=new LocalInput {Sequence=sequence,Tick=tick,Command=Copy(command,true)};
            if(inputs.Count>512) throw new InvalidOperationException("Prediction input history exceeded bound");
        }
        public void Acknowledge(long sequence,long tick,bool accepted)
        {if(inputs.TryGetValue(sequence,out var input)){input.Rejected=!accepted;if(accepted)input.Tick=tick;}}
        public void Reject(long sequence){if(inputs.TryGetValue(sequence,out var input))input.Rejected=true;}
        public RaidSimulation Project(RaidSimulation final,long serverTick)
        {
            if(final.Mode!=EncounterMode.Fighting)return final.Clone();
            long wanted=Math.Max(final.TickIndex,serverTick);
            Clamped=wanted>final.TickIndex+MaxProjectionTicks;
            int target=(int)Math.Min(wanted,final.TickIndex+MaxProjectionTicks);
            // Preserve the last held input before the snapshot; all older input decisions are in the snapshot.
            var held=inputs.Values.Where(i=>!i.Rejected && i.Tick<=final.TickIndex).OrderBy(i=>i.Tick).ThenBy(i=>i.Sequence).LastOrDefault();
            foreach(var key in inputs.Where(p=>p.Value.Tick<=final.TickIndex && p.Value!=held).Select(p=>p.Key).ToArray())inputs.Remove(key);
            var pending=inputs.Values.Where(i=>!i.Rejected && i.Tick>final.TickIndex).GroupBy(i=>i.Tick)
                .ToDictionary(g=>g.Key,g=>g.OrderBy(i=>i.Sequence).Last());
            var prediction=final.Clone();
            // The first rendered frame may be a few ticks after impact. Audit the
            // pose at impact, before a short dodge immunity expires in that gap.
            var activationStates=new Dictionary<int,PlayerState>();
            if(final.TickIndex>=target-4)activationStates[final.TickIndex]=prediction.Players[slot].Clone();
            int personalTarget=target;
            // Keep local jump height on the same tick as enemy activation, never one tick ahead.
            for(int tick=final.TickIndex+1;tick<=personalTarget;tick++)
            {
                bool edge=pending.TryGetValue(tick,out var next);
                if(edge)held=next;
                PlayerCommand command=held!=null && tick<=held.Tick+6?Copy(held.Command,edge):new PlayerCommand{PlayerId=slot};
                prediction.NetworkTick(new[]{command},-1);
                if(tick>=target-4)activationStates[tick]=prediction.Players[slot].Clone();
                foreach(var e in prediction.Events.Where(e=>e.Age==0 && (e.Kind=="Skill" && e.TargetId==slot || e.Kind=="Damage" && e.SourceId==slot)))
                    if(!string.IsNullOrEmpty(e.FeedbackKey) && feedback.Add(e.FeedbackKey))Feedback?.Invoke(e.Clone());
            }
            var display=final.Clone();
            display.Players[slot]=prediction.Players[slot].Clone();display.Players[slot].Hp=final.Players[slot].Hp;
            display.Players[slot].PotionCharges=final.Players[slot].PotionCharges;
            display.Players[slot].NextPotionTick=final.Players[slot].NextPotionTick;
            // A confirmed remote cast has a known start time. Advance its cosmetic clock,
            // not its HP, movement, cooldowns, inputs or combat decisions.
            float lead=(target-final.TickIndex)*RaidSimulation.FixedStep;
            int paused=final.PhaseTransitionStartTick>=0 ? Math.Max(0,Math.Min(target,final.PhaseTransitionStartTick+RaidSimulation.PhaseTransitionTicks)-Math.Max(final.TickIndex,final.PhaseTransitionStartTick)):0;
            lead=Math.Max(0,lead-paused*RaidSimulation.FixedStep);
            if(paused>0) {
                display.NextRoomMarkTick=prediction.NextRoomMarkTick;display.MarkDeadline=prediction.MarkDeadline;
                display.RoomFires=prediction.RoomFires.Select(f=>f.Clone()).ToList();
                display.Boss.TransitionTime=prediction.Boss.TransitionTime;
            }
            foreach(var actor in display.Players.Where(p=>p.Id!=slot)) {
                actor.ActionPresentationLead=0;
                if(actor.Hp<=0 || actor.ActionDuration<=0)continue;
                float elapsed=actor.ActionElapsed;
                actor.ActionElapsed=Math.Min(actor.ActionDuration,elapsed+lead*actor.ActionRate);
                actor.ActionPresentationLead=Math.Max(0,actor.ActionElapsed-elapsed);
            }
            display.TickIndex=target;display.Elapsed=prediction.Elapsed;
            display.Boss.Position=prediction.Boss.Position;display.BossMoveUntil=prediction.BossMoveUntil;
            display.Boss.ActionElapsed=Math.Min(display.Boss.ActionDuration,display.Boss.ActionElapsed+lead);
            display.Events.RemoveAll(e=>e.Kind=="MeteorImpact" || e.Kind=="StompImpact");
            // Player skill areas are cosmetic predictions; enemy areas are exclusively from confirmed schedules.
            display.Zones.RemoveAll(z=>z.FromPlayer && z.OwnerId==slot);
            display.Zones.AddRange(prediction.Zones.Where(z=>z.FromPlayer && z.OwnerId==slot).Select(z=>z.Clone()));
            foreach(var zone in display.Zones.Where(z=>z.FromPlayer && z.OwnerId!=slot).ToArray()) {
                zone.Age+=lead*zone.PlaybackRate;
                if(zone.Age>=zone.Telegraph+zone.ActiveTime+zone.Recovery)display.Zones.Remove(zone);
            }
            foreach(var zone in display.Zones.Where(z=>!z.FromPlayer).ToArray())
            {
                int activationTick=final.TickIndex;
                bool crossing=zone.Age>=zone.Telegraph;
                if(crossing)activationTick=final.TickIndex-(int)Math.Floor((zone.Age-zone.Telegraph)/RaidSimulation.FixedStep);
                for(int tick=final.TickIndex+1;tick<=target;tick++)
                {
                    if(final.PhaseTransitionStartTick>=0 && tick>final.PhaseTransitionStartTick && tick<=final.PhaseTransitionStartTick+RaidSimulation.PhaseTransitionTicks)continue;
                    float previous=zone.Age;zone.Age+=RaidSimulation.FixedStep;
                    if(previous<zone.Telegraph && zone.Age>=zone.Telegraph){activationTick=tick;crossing=true;}
                    if(zone.Age>=zone.Telegraph && zone.Age<zone.Telegraph+zone.ActiveTime && zone.Velocity.sqrMagnitude>0)
                        zone.Center+=zone.Velocity*RaidSimulation.FixedStep;
                }
                if(crossing && activated.Add(zone.Id) && !display.CinematicActive && !Clamped && target-activationTick<=4)
                {
                    if(activationStates.TryGetValue(activationTick,out var atImpact))
                        Activated?.Invoke(zone.Clone(),activationTick,target,atImpact);
                    if(RaidSimulation.IsStompZone(zone))Feedback?.Invoke(new SimEvent {RoomId=zone.RoomId,Kind="StompImpact",Position=display.Boss.Position,Value=zone.Id,TargetId=-1,FeedbackKey="stomp/"+zone.Id});
                    if(zone.Tag=="DawnMeteorImpact" || zone.Tag=="RoomMeteorImpact")Feedback?.Invoke(new SimEvent {RoomId=zone.RoomId,Kind="MeteorImpact",Position=zone.Center,Value=zone.Id,TargetId=-1,FeedbackKey="meteor/"+zone.Id});
                }
                if(zone.Age>=zone.Telegraph+zone.ActiveTime+zone.Recovery)display.Zones.Remove(zone);
            }
            // These trail patches are derived only from a charge already committed in this snapshot.
            // They predict its visible path; authoritative HP remains the final snapshot's HP.
            foreach(var trail in prediction.Zones.Where(z=>z.Tag=="ChargeTrail"))
                if(final.Zones.Any(z=>z.Tag=="BossCharge"&&z.Id==trail.SourceAction)&&!display.Zones.Any(z=>z.Id==trail.Id))display.Zones.Add(trail.Clone());
            DisplayTick=target;
            return display;
        }
        private static PlayerCommand Copy(PlayerCommand c,bool edges)=>new PlayerCommand {PlayerId=c.PlayerId,Move=c.Move,
            Basic=c.Basic,Run=c.Run,Skills=edges?c.Skills:0,Jump=edges&&c.Jump,Dash=edges&&c.Dash};
    }
}
