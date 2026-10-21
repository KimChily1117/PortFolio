namespace DnfRaidDemo
{
    // Shared by host calculation, server replay and client prediction.
    // Jump physics, boss warnings and awakening cutscenes keep their authored clocks.
    public static class RaidActionTempo
    {
        public const float WalkSpeed = 5.5f;
        public const float RunSpeed = 8f;
        public const float AttackMoveSpeed = 2.1f;
        public const float NormalActionRate = 1.12f;
        public static float Rate(SkillId skill, float equipmentRate) =>
            SkillBook.Get(skill).Tier > 0 ? 1f : equipmentRate * NormalActionRate;
    }
}
