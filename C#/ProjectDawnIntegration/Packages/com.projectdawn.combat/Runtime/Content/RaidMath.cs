#nullable disable
using System;
namespace ProjectDawn.Combat
{
    [Serializable] public struct RaidVector
    {
        public float x, y;
        public RaidVector(float x, float y) { this.x = x; this.y = y; }
        public static RaidVector zero => new RaidVector();
        public static RaidVector left => new RaidVector(-1, 0);
        public float sqrMagnitude => x*x+y*y;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public void Normalize() { this = normalized; }
        public RaidVector normalized => magnitude > .00001f ? this / magnitude : zero;
        public static RaidVector operator +(RaidVector a, RaidVector b) => new RaidVector(a.x+b.x,a.y+b.y);
        public static RaidVector operator -(RaidVector a, RaidVector b) => new RaidVector(a.x-b.x,a.y-b.y);
        public static RaidVector operator *(RaidVector a, float b) => new RaidVector(a.x*b,a.y*b);
        public static RaidVector operator /(RaidVector a, float b) => new RaidVector(a.x/b,a.y/b);
        public static bool operator ==(RaidVector a,RaidVector b) => (a-b).sqrMagnitude < .0000000001f;
        public static bool operator !=(RaidVector a,RaidVector b) => !(a==b);
        public override bool Equals(object value)=>value is RaidVector v && x.Equals(v.x) && y.Equals(v.y);
        public override int GetHashCode()=>x.GetHashCode()^(y.GetHashCode()<<2);
        public override string ToString() => $"({x:0.000},{y:0.000})";
#if UNITY_5_3_OR_NEWER
        public static bool operator ==(RaidVector a,UnityEngine.Vector2 b)=>(UnityEngine.Vector2)a==b;
        public static bool operator !=(RaidVector a,UnityEngine.Vector2 b)=>!(a==b);
        public static bool operator ==(UnityEngine.Vector2 a,RaidVector b)=>a==(UnityEngine.Vector2)b;
        public static bool operator !=(UnityEngine.Vector2 a,RaidVector b)=>!(a==b);
        public static implicit operator UnityEngine.Vector2(RaidVector v) => new UnityEngine.Vector2(v.x,v.y);
        public static implicit operator RaidVector(UnityEngine.Vector2 v) => new RaidVector(v.x,v.y);
        public static UnityEngine.Vector2 operator +(RaidVector a, UnityEngine.Vector2 b) => (UnityEngine.Vector2)a+b;
        public static UnityEngine.Vector2 operator +(UnityEngine.Vector2 a, RaidVector b) => a+(UnityEngine.Vector2)b;
        public static UnityEngine.Vector2 operator -(RaidVector a, UnityEngine.Vector2 b) => (UnityEngine.Vector2)a-b;
        public static UnityEngine.Vector2 operator -(UnityEngine.Vector2 a, RaidVector b) => a-(UnityEngine.Vector2)b;
#endif
    }
    public static class RaidMath
    {
        public static float Abs(float a) => Math.Abs(a);
        public static float Min(float a,float b) => Math.Min(a,b);
        public static int Min(int a,int b) => Math.Min(a,b);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static int Max(int a,int b) => Math.Max(a,b);
        public static float Clamp(float v,float a,float b) => Max(a,Min(v,b));
        public static int Clamp(int v,int a,int b) => Max(a,Min(v,b));
    }
}
