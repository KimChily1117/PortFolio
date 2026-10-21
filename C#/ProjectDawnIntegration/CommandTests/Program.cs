using System;
using System.Collections.Generic;
using ProjectDawn.Network;
using K=ProjectDawn.Network.SkillCommandKeys;
using T=ProjectDawn.Network.SkillCommandToken;
static class Program
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static readonly SkillCommandBinding[] defaults={new SkillCommandBinding(7,T.Forward,T.Forward,T.Space),new SkillCommandBinding(8,T.Down,T.Down,T.Space)};
    static List<SkillCommandMatch> Press(SkillCommandRecognizer r,K key,double time,IReadOnlyList<SkillCommandBinding> b=null,int facing=1)
    {r.Sample(K.None,facing,time-.01,b??defaults);return r.Sample(key,facing,time,b??defaults);}
    static void Main()
    {
        var r=new SkillCommandRecognizer();
        Check(Press(r,K.Right,0).Count==0,"first forward does not cast");
        Check(Press(r,K.Right,.15).Count==0,"second forward waits for terminal");
        var match=Press(r,K.Space,.25);Check(match.Count==1&&match[0].Skill==7&&match[0].Terminal==K.Space,"forward forward Space requests correct skill and consumed terminal");
        Check(r.Sample(K.Space,1,.3,defaults).Count==0,"held terminal does not repeat");
        Check(Press(r,K.Space,.4).Count==0,"consumed prefix cannot repeat");
        r.Reset();Press(r,K.Down,0);Press(r,K.Down,.15);Check(Press(r,K.Space,.3)[0].Skill==8,"down down Space independent of facing");
        r.Reset();Press(r,K.Left,0,null,-1);Press(r,K.Left,.15,null,-1);Check(Press(r,K.Space,.3,null,-1)[0].Skill==7,"left-facing forward forward maps to left left");
        r.Reset();Press(r,K.Left,0,null,1);Press(r,K.Left,.15,null,1);Check(Press(r,K.Space,.3,null,1).Count==0,"backward is not forward");
        r.Reset();Press(r,K.Right,0,null,1);Press(r,K.Right,.15,null,-1);Check(Press(r,K.Space,.3,null,-1).Count==1,"facing captured on first press despite later turn");
        r.Reset();Press(r,K.Right,0);r.Sample(K.Right,1,.15,defaults);Check(Press(r,K.Space,.3).Count==0,"holding direction cannot supply repeated direction");
        r.Reset();Press(r,K.Right,0);Press(r,K.Right,.5);Check(Press(r,K.Space,.6).Count==0,"expired gap rejects old prefix");
        var longBinding=new[]{new SkillCommandBinding(9,T.Down,T.Down,T.Down,T.Down,T.Space)};
        r.Reset();Press(r,K.Down,0,longBinding);Press(r,K.Down,.4,longBinding);Press(r,K.Down,.8,longBinding);Press(r,K.Down,1.2,longBinding);Check(Press(r,K.Space,1.6,longBinding).Count==0,"total sequence deadline bounds individually valid gaps");
        r.Reset();Press(r,K.Right,0);Press(r,K.Right,.1);r.Sample(K.None,1,.2,defaults,false);Check(Press(r,K.Space,.3).Count==0,"chat or modal blocking clears prefix");
        r.Reset();r.Sample(K.Down,1,0,defaults,false);r.Sample(K.Down,1,.1,defaults);Press(r,K.Down,.2);Check(Press(r,K.Space,.3).Count==0,"typing held direction cannot leak on closing chat");
        r.Reset();Press(r,K.Right,0);Press(r,K.Right,.1);r.Reset(K.Space);Check(r.Sample(K.Space,1,.2,defaults).Count==0,"focus reset ignores key already held");
        var custom=new[]{new SkillCommandBinding(10,T.Back,T.Z),new SkillCommandBinding(11,T.Up,T.X)};
        r.Reset();Press(r,K.Left,0,custom);Check(Press(r,K.Z,.1,custom)[0].Terminal==K.Z,"custom backward Z terminal");
        Press(r,K.Up,.2,custom);Check(Press(r,K.X,.3,custom)[0].Skill==11,"custom up X terminal");
        var longest=new[]{new SkillCommandBinding(7,T.Forward,T.Space),new SkillCommandBinding(8,T.Down,T.Forward,T.Space)};
        r.Reset();Press(r,K.Down,0,longest);Press(r,K.Right,.1,longest);Check(Press(r,K.Space,.2,longest)[0].Skill==8,"longest matching suffix wins");
        r.Reset();Press(r,K.Right,0);r.Sample(K.Left|K.Right,1,.1,defaults);Press(r,K.Right,.2);Check(Press(r,K.Space,.3).Count==0,"opposing directions invalidate buffered command");
        r.Reset();Press(r,K.Right,10);Press(r,K.Right,10.1);Check(Press(r,K.Space,1).Count==0,"backwards clock resets prefix");
        Check(SkillCommandRecognizer.Validate(defaults)==null,"default configuration valid");
        Check(SkillCommandRecognizer.Validate(Array.Empty<SkillCommandBinding>())==null,"all commands may be disabled");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(1,T.Space)})!=null,"single key commands rejected to preserve ordinary bindings");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(1,T.Down,T.Up)})!=null,"direction terminal rejected");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(1,T.Down,(T)77,T.Z)})!=null,"unknown persisted token rejected");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(1,T.Down,T.Z),new SkillCommandBinding(2,T.Down,T.Z)})!=null,"duplicate command collision rejected");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(1,T.Down,T.Z),new SkillCommandBinding(2,T.Down,T.Z,T.X)})!=null,"early-firing prefix collision rejected");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(1,T.Down,T.Z),new SkillCommandBinding(1,T.Up,T.X)})!=null,"duplicate skill binding rejected");
        Check(SkillCommandRecognizer.Validate(longest)==null,"suffix overlap allowed with longest match policy");
        Check(SkillCommandRecognizer.Validate(new[]{new SkillCommandBinding(0,T.Down,T.Z)})!=null,"invalid basic skill binding rejected");
        Console.WriteLine("Skill commands: "+checks+" checks passed.");
    }
}
