#nullable enable
namespace ProjectDawn.Combat
{
    // Explicit network-validation ruleset. Do not silently use this as the 21-skill game balance.
    public static class IntegrationRules
    {
        public const string Id = "dawn-network-slice-v1";
        public static TimingPolicy Timing() => new TimingPolicy(20000, 120000, 120000, 260000, 0);
        public static CombatRules Create() => new CombatRules(new[] {
            new AttackDefinition(1, false, false, 25, 2500, cooldownTicks: 8),
            new AttackDefinition(4, false, false, 10, 2500, repeat: 3, intervalTicks: 2, cooldownTicks: 12)
        });
    }
}
