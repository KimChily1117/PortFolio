using System;
using System.Globalization;
using System.Linq;
namespace DnfRaidDemo
{
    [Serializable] public sealed class BossPatternStep { public int Kind, Tick, Target; }
    // Bounded wire language: 0 approach, 1 legacy attack, 2 meteor+ground shockwave, 3 carry meteor mark, 4 locked-direction charge.
    // Lua owns composition. The server owns damage, geometry, movement speed and safety limits.
    public static class BossPatternProgram
    {
        public static BossPatternStep[] Parse(string program)
        {
            if(string.IsNullOrEmpty(program)||program.Length>512)return Array.Empty<BossPatternStep>();
            var parts=program.Split(';');if(parts.Length<1||parts.Length>7)return Array.Empty<BossPatternStep>();
            var steps=new BossPatternStep[parts.Length];
            for(int i=0;i<parts.Length;i++) {
                var p=parts[i].Split(',');int k,t,a;
                if(p.Length!=3||!int.TryParse(p[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out k)||
                   !int.TryParse(p[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out t)||
                   !int.TryParse(p[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out a))return Array.Empty<BossPatternStep>();
                steps[i]=new BossPatternStep{Kind=k,Tick=t,Target=a};
            } return steps;
        }
        // Deterministic cyclic draw; prefer everyone not marked in the immediately previous round.
        // Fewer than four living actors cannot always supply two new targets: fill from previous
        // survivors only after exhausting new survivors, never duplicate an actor within a round.
        public static int[] MarkTargets(uint roll,int aliveMask,int previousMask)
        {
            var selected=new System.Collections.Generic.List<int>();
            for(int pass=0;pass<2;pass++)for(int i=0;i<4&&selected.Count<2;i++) {
                int slot=((int)(roll%4)+i)%4,bit=1<<slot;
                if((aliveMask&bit)!=0 && (((previousMask&bit)!=0)==(pass==1)))selected.Add(slot);
            }
            return selected.ToArray();
        }
        public static bool Valid(string program,int pattern,int ordinal,uint roll=0,int aliveMask=15,int previousMarkMask=0,int playerCount=4)
        {
            if(playerCount<1||playerCount>4)return false;
            var s=Parse(program);if(s.Length<2||pattern<0||(pattern>6&&pattern!=DawnPatternRules.Charge)||ordinal<0)return false;
            int target=ordinal%playerCount;
            if(s[0].Kind!=0||s[0].Tick!=0||s[0].Target!=target)return false;
            int firstAttack=1;
            if(s[firstAttack].Tick<24||s[firstAttack].Tick>60)return false;
            if(pattern==DawnPatternRules.Charge)return s.Length==4&&s.Skip(1).Select((x,i)=>x.Kind==4&&x.Tick==s[1].Tick+i*DawnPatternRules.ChargeIntervalTicks&&x.Target==(target+i)%playerCount).All(x=>x);
            if(pattern!=6)return s.Length==firstAttack+1&&s[firstAttack].Kind==(pattern==DawnPatternRules.Charge?4:1)&&s[firstAttack].Target==target;
            return s.Length==firstAttack+4&&s.Skip(firstAttack).Select((x,i)=>x.Kind==2&&x.Tick==s[firstAttack].Tick+i*87&&x.Target==(target+i)%playerCount).All(x=>x);
        }
        public static bool ValidRoomMarks(string program,uint roll,int aliveMask,int previousMask)
        {
            var expected=MarkTargets(roll,aliveMask,previousMask);var steps=Parse(program);
            return expected.Length>0&&steps.Length==expected.Length&&steps.Select((x,i)=>x.Kind==3&&x.Tick==0&&x.Target==expected[i]).All(x=>x);
        }
        public static string RoomMarks(uint roll,int aliveMask,int previousMask)=>string.Join(";",MarkTargets(roll,aliveMask,previousMask).Select(x=>"3,0,"+x));
        public static string Default(int pattern,int ordinal,int preparation=36,uint roll=0,int aliveMask=15,int previousMarkMask=0,int playerCount=4)
        {
            if(playerCount<1||playerCount>4)throw new ArgumentOutOfRangeException(nameof(playerCount));
            int target=ordinal%playerCount;string p="0,0,"+target;
            for(int i=0;i<(pattern==6?4:pattern==DawnPatternRules.Charge?3:1);i++)p+=";"+(pattern==6?2:pattern==DawnPatternRules.Charge?4:1)+","+(preparation+i*(pattern==DawnPatternRules.Charge?DawnPatternRules.ChargeIntervalTicks:87))+","+((target+i)%playerCount);
            return p;
        }
    }
}
