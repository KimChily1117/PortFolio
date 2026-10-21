namespace DnfRaidDemo
{
    // Server-side contract, intentionally independent of the Lua interpreter.
    public static class DawnPatternRules
    {
        public const int Meteor=6, Charge=8, ChargeCount=3, ChargeIntervalTicks=162;
        public const float ChargeTrailSeconds=2.5f, ChargeTrailRadius=.6f, ChargeTrailDamage=3f;
        public const float ChargeSpeed=14f, ChargeRange=9f, ChargeRadius=.8f, ChargeTell=.8f, ChargeRecovery=.85f, ChargeDamage=38f;
        public const float StartHeight=6.5f, FallSpeed=2.6777f*2f, HalfWidth=1.1f*3f, HalfDepth=.4f*3f, ImpactDamage=25f;
        public const int MeteorCount=4;
        public const float MeteorInterval=1.45f;
        public const float VisualScale=3f, DiagonalOffset=4.5f;
        public static int Select(uint roll,int phase,int previous,int ordinal)
        {
            if(ordinal%4==0)return Meteor;
            if(ordinal%4==2)return Charge;
            int[] pool=phase==1?new[]{0,2,3,5}:new[]{0,1,2,4,5};
            int index=(int)(roll%(uint)pool.Length);
            return pool[index]==previous?pool[(index+1)%pool.Length]:pool[index];
        }
    }
}
