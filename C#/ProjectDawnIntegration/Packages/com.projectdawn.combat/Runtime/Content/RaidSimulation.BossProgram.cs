using System;
using System.Linq;
using Vector2 = ProjectDawn.Combat.RaidVector;
using Mathf = ProjectDawn.Combat.RaidMath;
namespace DnfRaidDemo
{
    public partial class RaidSimulation
    {
        public int ProgramPattern=-1, ProgramStart, ProgramIndex;
        public BossPatternStep[] ProgramSteps=Array.Empty<BossPatternStep>();
        public Vector2 BossMoveTarget;
        public int BossMoveUntil;
        private string _approvedProgram;
        void BeginBossProgram(int pattern,string program)
        {
            if(!BossPatternProgram.Valid(program,pattern,SelectionOrdinal,SelectionRoll,SelectionAliveMask,SelectionPreviousMarkMask,Players.Count)) {Emit("PatternMissing",Boss.Position,pattern,-1);return;}
            ProgramPattern=pattern;ProgramSteps=BossPatternProgram.Parse(program);ProgramIndex=0;ProgramStart=TickIndex;
            _lastPattern=pattern;_patternRemaining=12f;Boss.PatternName="접근 · 패턴 준비";
            Emit("Pattern",Boss.Position,pattern,-1);
        }
        void CancelBossProgram(){ProgramPattern=-1;ProgramSteps=Array.Empty<BossPatternStep>();ProgramIndex=0;BossMoveUntil=0;}
        void BeginCharge(PlayerState target)
        {
            Boss.PatternName="돌진 · 방향 고정 후 측면 회피";
            if(target==null){_patternRemaining=.5f;return;}
            Boss.PatternName="연속 돌진 "+(ProgramIndex-1)+"/3 · "+(target.Id+1)+"P 지목";
            var direction=(target.Position-Boss.Position).normalized;
            if(direction.sqrMagnitude<.001f)direction=Vector2.left;
            // Clip the ray against the arena. Never steer after the warning becomes visible.
            float distance=DawnPatternRules.ChargeRange;
            if(Mathf.Abs(direction.x)>.001f)distance=Mathf.Min(distance,((direction.x>0?7.5f:-7.5f)-Boss.Position.x)/direction.x);
            if(Mathf.Abs(direction.y)>.001f)distance=Mathf.Min(distance,((direction.y>0?2.3f:-2.3f)-Boss.Position.y)/direction.y);
            distance=Mathf.Max(0,distance);
            float duration=Mathf.Max(FixedStep,(float)System.Math.Floor(distance/DawnPatternRules.ChargeSpeed/FixedStep)*FixedStep);
            var charge=Circle(Boss.Position,DawnPatternRules.ChargeRadius,DawnPatternRules.ChargeTell,duration,DawnPatternRules.ChargeRecovery,DawnPatternRules.ChargeDamage,"BossCharge");
            charge.TargetId=target.Id;charge.HitIndex=ProgramIndex-1;
            charge.Offset=Boss.Position;charge.Velocity=direction*Mathf.Min(DawnPatternRules.ChargeSpeed,distance/duration);
            float end=0;AddEnemy(charge,0,ref end);_patternRemaining=end+.35f;
            Emit("ChargeLocked",target.Position,target.Id,-1);
        }
        void UpdateBossProgram()
        {
            if(ProgramPattern<0||Boss.StaggerTime>0||Boss.TransitionTime>0||Boss.Hp<=0)return;
            while(ProgramIndex<ProgramSteps.Length&&TickIndex-ProgramStart>=ProgramSteps[ProgramIndex].Tick) {
                var step=ProgramSteps[ProgramIndex++];var target=Players.FirstOrDefault(p=>p.Id==step.Target&&p.Hp>0&&p.RoomId==BossRoom)??ClosestAlivePlayer();
                if(step.Kind==0) {
                    BossMoveUntil=ProgramStart+ProgramSteps.First(x=>x.Kind==1||x.Kind==2||x.Kind==4).Tick;
                    if(target!=null) {
                        float side=Boss.Position.x>=target.Position.x?1:-1;
                        BossMoveTarget=new Vector2(Mathf.Clamp(target.Position.x+side*1.7f,-6.5f,6.5f),Mathf.Clamp(target.Position.y,-2f,2f));
                    } else BossMoveTarget=Boss.Position;
                } else if(step.Kind==1) {BossMoveUntil=0;BeginPattern(ProgramPattern,false);}
                else if(step.Kind==4) { BossMoveUntil=0;BeginCharge(target); }
                else {
                    BossMoveUntil=0;Boss.PatternName="Dawn 메테오 · 직격 이동 / 충격파 점프";
                    if(target!=null) {
                        float end=0,fall=DawnPatternRules.StartHeight/DawnPatternRules.FallSpeed;
                        AddEnemy(Ellipse(target.Position,new Vector2(DawnPatternRules.HalfWidth,DawnPatternRules.HalfDepth),fall,.12f,1.5f,DawnPatternRules.ImpactDamage,"DawnMeteorImpact"),0,ref end);
                        var shock=Box(Vector2.zero,new Vector2(9,3),fall,.14f,.5f,16,"DawnMeteorShockwave");shock.GroundOnly=true;AddEnemy(shock,0,ref end);
                        _patternRemaining=end+.6f;
                    }
                }
                Emit("PatternAction",Boss.Position,step.Kind,-1);
            }
            if(TickIndex<BossMoveUntil) {
                // Track a living actor in this room during approach; direction locks at the attack callback.
                var tracked=Players.FirstOrDefault(p=>p.Id==ProgramSteps[0].Target&&p.Hp>0&&p.RoomId==BossRoom)??ClosestAlivePlayer();
                if(tracked!=null) {
                    float side=Boss.Position.x>=tracked.Position.x?1:-1;
                    BossMoveTarget=new Vector2(Mathf.Clamp(tracked.Position.x+side*1.7f,-6.5f,6.5f),Mathf.Clamp(tracked.Position.y,-2f,2f));
                }
                var delta=BossMoveTarget-Boss.Position;
                if(delta.sqrMagnitude>.0001f)Boss.Position+=delta.normalized*Mathf.Min(delta.magnitude,4.2f*FixedStep);
            }
            if(ProgramIndex>=ProgramSteps.Length) {ProgramPattern=-1;ProgramSteps=Array.Empty<BossPatternStep>();}
        }
    }
}
