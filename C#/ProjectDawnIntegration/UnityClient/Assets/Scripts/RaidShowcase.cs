using UnityEngine;

namespace DnfRaidDemo
{
    // Optional recording driver: reads the same telegraphs a player sees and submits normal commands.
    // It neither edits HP nor supplies invulnerability. The host selects a disclosed demonstration schedule.
    public static class RaidShowcase
    {
        public static readonly float[] PatternTimes = { 4f, 8f, 13f, 19f, 25f, 31f, 37f };
        public static readonly int[] PatternIds = { 0, 1, 2, 5, 4, 3, 5 };

        public static PlayerCommand Command(RaidSimulation sim, int id)
        {
            var command = new PlayerCommand { PlayerId = id };
            if (sim.Mode != EncounterMode.Fighting) return command;
            if (sim.Elapsed < 1.8f) return command;
            PlayerState player = sim.Players[id];
            if (player.Hp <= 0) return command;
            Vector2 combat = sim.Boss.Position + new Vector2(id == 0 ? -1.5f : -2.25f, id == 0 ? -.72f : .72f);
            Vector2 goal = combat;
            bool evade = Unsafe(sim, player.Position, .30f);
            float best = Score(sim, player, goal, combat);
            // Local candidate search is deterministic from the received state. It does not inspect future RNG.
            for (float y = -2.6f; y <= 2.61f; y += .65f)
                for (float x = -8f; x <= 8.01f; x += .65f)
                {
                    Vector2 candidate = new Vector2(x, y);
                    float score = Score(sim, player, candidate, combat);
                    if (score < best) { best = score; goal = candidate; }
                }
            Vector2 difference = goal - player.Position;
            command.Move = difference.magnitude > .16f ? difference.normalized : Vector2.zero;
            bool groundSoon = false;
            foreach (AttackZone zone in sim.Zones)
            {
                if (zone.FromPlayer || zone.Age < 0 || zone.Age >= zone.Telegraph + zone.ActiveTime) continue;
                float remaining = zone.Telegraph - zone.Age;
                if (zone.GroundOnly && remaining < .29f && remaining > -.02f)
                { groundSoon = true; command.Jump |= player.JumpHeight <= .001f; }
                if (!zone.GroundOnly && remaining < .23f && evade && player.Cooldowns[(int)SkillId.Dash] <= 0)
                    command.Dash = true;
            }
            // Face the boss before initiating a skill, using normal movement input.
            bool inRange = Mathf.Abs(player.Position.y - sim.Boss.Position.y) < .86f &&
                Mathf.Abs(player.Position.x - sim.Boss.Position.x) < 2.5f;
            bool ready = sim.Elapsed < 30 && inRange && !evade && !groundSoon && player.JumpHeight <= .001f;
            if (ready) command.Move = Vector2.zero;
            if (ready && player.Facing != (player.Position.x < sim.Boss.Position.x ? 1 : -1))
                command.Move = new Vector2(player.Position.x < sim.Boss.Position.x ? 1 : -1, 0);
            else if (ready)
            {
                command.Basic = true;
                // Different skill cadence makes both local inputs visible in the two-client film.
                if (sim.Elapsed > 2 + id && player.Cooldowns[1] <= 0) command.Skills |= PlayerCommand.Bit(SkillId.BloodSword);
                if (sim.Elapsed > 3 + id && player.Cooldowns[2] <= 0) command.Skills |= PlayerCommand.Bit(SkillId.GoreCross);
                if (sim.Elapsed > 16 + id && player.Cooldowns[4] <= 0 && !Unsafe(sim, player.Position, 1.5f)) command.Skills |= PlayerCommand.Bit(SkillId.ExtremeOverkill);
            }
            return command;
        }

        private static bool Unsafe(RaidSimulation sim, Vector2 point, float margin)
        {
            foreach (AttackZone zone in sim.Zones)
                if (!zone.FromPlayer && !zone.GroundOnly && zone.Age >= 0 && zone.Age < zone.Telegraph + zone.ActiveTime
                    && RaidSimulation.Intersects(zone, point, RaidSimulation.PlayerHurtboxHalfSize + Vector2.one * margin)) return true;
            return false;
        }

        private static float Score(RaidSimulation sim, PlayerState player, Vector2 point, Vector2 combat)
        {
            float distance = (point - player.Position).magnitude;
            float score = distance * .6f + (point - combat).magnitude * .85f;
            foreach (PlayerState other in sim.Players)
                if (other.Id != player.Id) score += Mathf.Max(0, 1.25f - (point - other.Position).magnitude) * 3f;
            foreach (AttackZone zone in sim.Zones)
            {
                if (zone.FromPlayer || zone.GroundOnly || zone.Age < 0 || zone.Age >= zone.Telegraph + zone.ActiveTime) continue;
                if (RaidSimulation.Intersects(zone, point, RaidSimulation.PlayerHurtboxHalfSize + Vector2.one * .3f)) score += 1000;
                // Prefer routes that can clear a currently unsafe footprint before activation.
                if (RaidSimulation.Intersects(zone, player.Position, RaidSimulation.PlayerHurtboxHalfSize))
                    score += Mathf.Max(0, distance / 4.8f - Mathf.Max(.1f, zone.Telegraph - zone.Age)) * 8f;
            }
            return score;
        }
    }
}
