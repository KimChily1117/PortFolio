using System;
using System.Collections.Generic;

namespace ProjectDawn.Network
{
    public enum SkillCommandToken { Forward, Back, Up, Down, Z, X, Space }
    // Physical keys are kept until a match is evaluated. Facing is captured on the first
    // press of each candidate, so turning during a sequence cannot reinterpret its past.
    [Flags] public enum SkillCommandKeys { None=0, Left=1, Right=2, Up=4, Down=8, Z=16, X=32, Space=64 }
    public sealed class SkillCommandBinding
    {
        public int Skill;
        public SkillCommandToken[] Tokens;
        public SkillCommandBinding(int skill, params SkillCommandToken[] tokens) { Skill=skill; Tokens=tokens; }
    }
    public struct SkillCommandMatch
    {
        public int Skill;
        public SkillCommandKeys Terminal;
        public SkillCommandMatch(int skill, SkillCommandKeys terminal) { Skill=skill; Terminal=terminal; }
    }

    /// <summary>Input-only recognizer. Returns skill requests; never changes combat state.</summary>
    public sealed class SkillCommandRecognizer
    {
        public const double MaximumGap=.45, MaximumDuration=1.4;
        public const int MaximumLength=8;
        struct Press { public SkillCommandKeys Key; public int Facing; public double Time; }
        readonly List<Press> presses=new List<Press>();
        SkillCommandKeys held;
        double lastSample=-1;
        public void Reset(SkillCommandKeys currentlyHeld=SkillCommandKeys.None)
        { presses.Clear(); held=currentlyHeld; lastSample=-1; }

        // Call every frame, including blocked frames. Blocked input is discarded and its
        // held keys are remembered, so closing chat cannot create synthetic key presses.
        public List<SkillCommandMatch> Sample(SkillCommandKeys keys, int facing, double now,
            IReadOnlyList<SkillCommandBinding> bindings, bool enabled=true)
        {
            var matches=new List<SkillCommandMatch>();
            if(!enabled || double.IsNaN(now) || double.IsInfinity(now)) { Reset(keys); return matches; }
            if(lastSample>=0 && now<lastSample) presses.Clear();
            lastSample=now;
            SkillCommandKeys down=keys & ~held; held=keys;
            if(presses.Count>0 && (now-presses[presses.Count-1].Time>MaximumGap || now-presses[0].Time>MaximumDuration)) presses.Clear();
            // Opposing directions are ambiguous. Do not preserve a command through them.
            if((keys&(SkillCommandKeys.Left|SkillCommandKeys.Right))==(SkillCommandKeys.Left|SkillCommandKeys.Right)
                || (keys&(SkillCommandKeys.Up|SkillCommandKeys.Down))==(SkillCommandKeys.Up|SkillCommandKeys.Down))
            { presses.Clear(); return matches; }
            foreach(SkillCommandKeys key in new[]{SkillCommandKeys.Left,SkillCommandKeys.Right,SkillCommandKeys.Up,SkillCommandKeys.Down,SkillCommandKeys.Z,SkillCommandKeys.X,SkillCommandKeys.Space})
            {
                if((down&key)==0) continue;
                presses.Add(new Press{Key=key,Facing=facing<0?-1:1,Time=now});
                while(presses.Count>MaximumLength) presses.RemoveAt(0);
                SkillCommandBinding best=null;
                foreach(var binding in bindings)
                {
                    if(binding==null || binding.Tokens==null || binding.Tokens.Length>presses.Count || binding.Tokens.Length<2) continue;
                    int start=presses.Count-binding.Tokens.Length;
                    if(now-presses[start].Time>MaximumDuration) continue;
                    bool match=true;
                    for(int i=0;i<binding.Tokens.Length;i++)
                        if(ToToken(presses[start+i].Key,presses[start].Facing)!=binding.Tokens[i]) { match=false; break; }
                    if(match && (best==null || binding.Tokens.Length>best.Tokens.Length
                        || (binding.Tokens.Length==best.Tokens.Length && binding.Skill<best.Skill))) best=binding;
                }
                if(best!=null) { matches.Add(new SkillCommandMatch(best.Skill,key)); presses.Clear(); }
            }
            return matches;
        }
        static SkillCommandToken ToToken(SkillCommandKeys key,int facing)
        {
            switch(key) {
                case SkillCommandKeys.Left: return facing<0?SkillCommandToken.Forward:SkillCommandToken.Back;
                case SkillCommandKeys.Right:return facing<0?SkillCommandToken.Back:SkillCommandToken.Forward;
                case SkillCommandKeys.Up:return SkillCommandToken.Up;
                case SkillCommandKeys.Down:return SkillCommandToken.Down;
                case SkillCommandKeys.Z:return SkillCommandToken.Z;
                case SkillCommandKeys.X:return SkillCommandToken.X;
                default:return SkillCommandToken.Space;
            }
        }
        public static string Validate(IReadOnlyList<SkillCommandBinding> bindings)
        {
            if(bindings==null)return "설정이 없습니다.";
            for(int i=0;i<bindings.Count;i++) {
                var a=bindings[i];
                if(a==null || a.Tokens==null || a.Tokens.Length<2 || a.Tokens.Length>MaximumLength)return "커맨드는 2~8개 키로 지정하세요.";
                if(a.Skill<1 || a.Skill>30)return "지원하지 않는 스킬입니다.";
                foreach(var token in a.Tokens) if(!Enum.IsDefined(typeof(SkillCommandToken),token))return "지원하지 않는 키입니다.";
                var end=a.Tokens[a.Tokens.Length-1];
                if(end!=SkillCommandToken.Z && end!=SkillCommandToken.X && end!=SkillCommandToken.Space)return "마지막 키는 Z, X, Space 중 하나여야 합니다.";
                for(int j=0;j<i;j++) {
                    var b=bindings[j];
                    if(a.Skill==b.Skill)return "한 스킬에는 하나의 커맨드만 지정할 수 있습니다.";
                    int common=Math.Min(a.Tokens.Length,b.Tokens.Length);bool prefix=true;
                    for(int k=0;k<common;k++)if(a.Tokens[k]!=b.Tokens[k]){prefix=false;break;}
                    if(prefix)return "같거나 다른 커맨드의 시작 부분과 겹치는 조합은 사용할 수 없습니다.";
                }
            }
            return null;
        }
    }
}
