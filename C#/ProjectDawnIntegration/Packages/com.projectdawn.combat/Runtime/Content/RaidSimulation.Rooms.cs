using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = ProjectDawn.Combat.RaidVector;
using Mathf = ProjectDawn.Combat.RaidMath;
namespace DnfRaidDemo
{
    [Serializable] public sealed class RoomFire
    {
        public int RoomId, StartTick, EndTick, NextPulse;
        public Vector2 Impact;
        public RoomFire Clone() => (RoomFire)MemberwiseClone();
    }
    public partial class RaidSimulation
    {
        public const int BossRoom = 4, RoomCount = 9, MarkTicks = 420, FireTicks = 960, RoomMarkInterval = 600;
        public const float RoomMeteorHpFraction=.95f,RoomBurnHpFraction=.20f;
        public bool RoomsEnabled;
        public int NextRoomMarkTick=300,PendingRoomMarkTick,RoomSelectionAliveMask=15,RoomSelectionPreviousMarkMask;
        public uint RoomSelectionRoll;
        private string _approvedRoomProgram;
        void UpdateRoomScheduler()
        {
            if(!RoomsEnabled)return;
            if(PendingRoomMarkTick==0&&TickIndex>=NextRoomMarkTick-30) {
                // During the revival window there may be no living target. Do not
                // ask Lua for an impossible empty mark batch, or blame the host.
                if(!Players.Any(p=>p.Hp>0)){NextRoomMarkTick=TickIndex+31;return;}
                PendingRoomMarkTick=NextRoomMarkTick;RoomSelectionRoll=NextRandom();
                RoomSelectionAliveMask=Players.Where(p=>p.Hp>0).Aggregate(0,(mask,p)=>mask|(1<<p.Id));
                RoomSelectionPreviousMarkMask=PreviousMarkMask;
            }
            if(PendingRoomMarkTick>0&&TickIndex==PendingRoomMarkTick) {
                if(BossPatternProgram.ValidRoomMarks(_approvedRoomProgram,RoomSelectionRoll,RoomSelectionAliveMask,RoomSelectionPreviousMarkMask)) {
                    foreach(var step in BossPatternProgram.Parse(_approvedRoomProgram))BeginRoomMeteor(step.Target);
                    if(MarkTargets.Length>0)PreviousMarkMask=MarkTargets.Aggregate(0,(mask,id)=>mask|(1<<id));
                    Emit("RoomPattern",Boss.Position,7,-1);
                } else Emit("RoomPatternMissing",Boss.Position,7,-1);
                NextRoomMarkTick=PendingRoomMarkTick+RoomMarkInterval;PendingRoomMarkTick=0;
            }
        }
        public int MarkTarget = -1, MarkDeadline, PreviousMarkMask;
        public int[] MarkTargets = Array.Empty<int>();
        public List<RoomFire> RoomFires = new List<RoomFire>();
        static bool IsRoomHazard(AttackZone zone) => zone.Tag=="RoomBurn" || zone.Tag=="RoomMeteorImpact" || zone.Tag=="ChargeTrail";
        void ResetRooms() { NextRoomMarkTick=300;PendingRoomMarkTick=0;RoomSelectionRoll=0;RoomSelectionAliveMask=(1<<Players.Count)-1;RoomSelectionPreviousMarkMask=0; MarkTarget=-1;MarkTargets=Array.Empty<int>();PreviousMarkMask=0;MarkDeadline=0;RoomFires.Clear(); }
        // Row-major map: 0 1 2 / 3 4 5 / 6 7 8. Up decreases the row.
        public static int NeighborRoom(int room,int dx,int dy)
        {
            if(room<0||room>=9||Math.Abs(dx)+Math.Abs(dy)!=1)return -1;
            int x=room%3+dx,y=room/3+dy;
            return x<0||x>=3||y<0||y>=3 ? -1:y*3+x;
        }
        void UpdateRoomTravel(PlayerState p,PlayerCommand input)
        {
            if(!RoomsEnabled||input==null||p.Hp<=0||p.StunTime>0||p.AttackTime>0||p.DashTime>0||p.JumpHeight>0||TickIndex-p.RoomChangedTick<45)return;
            int dx=0,dy=0;
            if(Mathf.Abs(p.Position.y)<.75f) {
                if(p.Position.x>=8.65f&&input.Move.x>.5f)dx=1;
                if(p.Position.x<=-8.65f&&input.Move.x<-.5f)dx=-1;
            }
            if(Mathf.Abs(p.Position.x)<.85f) {
                if(p.Position.y>=2.75f&&input.Move.y>.5f)dy=-1;
                if(p.Position.y<=-2.75f&&input.Move.y<-.5f)dy=1;
            }
            int room=NeighborRoom(p.RoomId,dx,dy);if(room<0)return;
            p.RoomId=room;p.RoomChangedTick=TickIndex;
            p.Position=dx!=0?new Vector2(-dx*7.8f,0):new Vector2(0,dy*2f);
            // Travel is a normal validated move, with no healing or immunity.
            Emit("RoomEnter",p.Position,room,p.Id);
        }
        void BeginRoomMeteor(int candidate)
        {
            if(!RoomsEnabled||MarkTargets.Length>=2 || MarkTargets.Length>0&&MarkDeadline!=TickIndex+MarkTicks)return;
            // The admitted Lua candidates are unique. A death after selection gets a bounded
            // living replacement; never duplicate the other mark in this same batch.
            var ordered=Players.OrderBy(p=>(p.Id-candidate+4)%4).Where(p=>p.Hp>0&&!MarkTargets.Contains(p.Id)).ToArray();
            var target=ordered.FirstOrDefault(p=>p.Id==candidate)??ordered.FirstOrDefault(p=>(PreviousMarkMask&(1<<p.Id))==0)??ordered.FirstOrDefault();
            if(target==null)return;
            MarkTargets=MarkTargets.Concat(new[]{target.Id}).ToArray();MarkTarget=MarkTargets[0];MarkDeadline=TickIndex+MarkTicks;
            Emit("RoomMarked",target.Position,MarkDeadline,target.Id);
        }

        void UpdateRoomHazards()
        {
            if(!RoomsEnabled)return;
            if(MarkTargets.Length>0&&TickIndex>=MarkDeadline) {
              foreach(int marked in MarkTargets) {
                var p=Players.First(x=>x.Id==marked);
                // A dead marked actor still drops in its last room; dying cannot erase the mechanic.
                float fall=DawnPatternRules.StartHeight/DawnPatternRules.FallSpeed;
                int start=TickIndex+(int)Math.Ceiling(fall/FixedStep)-1;
                var zone=Ellipse(p.Position,new Vector2(DawnPatternRules.HalfWidth,DawnPatternRules.HalfDepth),fall,.12f,1.5f,95,"RoomMeteorImpact");
                zone.RoomId=p.RoomId;zone.OwnerId=-1;AddZone(zone);
                RoomFires.Add(new RoomFire {RoomId=p.RoomId,Impact=p.Position,StartTick=start,EndTick=start+FireTicks,NextPulse=start+60});
                Emit("RoomMeteorLocked",p.Position,p.RoomId,p.Id);
              }
              MarkTarget=-1;MarkTargets=Array.Empty<int>();
            }
            foreach(var fire in RoomFires.ToArray()) {
                if(TickIndex>=fire.EndTick){RoomFires.Remove(fire);continue;}
                if(TickIndex>=fire.NextPulse) {
                    var burn=Box(Vector2.zero,new Vector2(9,3),0,.12f,0,20,"RoomBurn");
                    burn.RoomId=fire.RoomId;burn.OwnerId=-1;AddZone(burn);fire.NextPulse+=60;
                }
            }
        }
    }
}
