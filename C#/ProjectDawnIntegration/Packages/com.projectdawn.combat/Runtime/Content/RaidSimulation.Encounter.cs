namespace DnfRaidDemo
{
    public partial class RaidSimulation
    {
        // Authored from the supplied recording (source 3.0s onwards). This is
        // shared server/host time, never a client-local loading/cutscene timer.
        public const int EncounterIntroTicks = 840;
        public const int EncounterLandingTick = 708;
        public bool IntroEnabled;
        public bool IntroActive => IntroEnabled && Mode == EncounterMode.Fighting && TickIndex < EncounterIntroTicks;

        public const int PhaseTransitionTicks = 720;
        public const int PhaseBattlefieldTick = 480;
        public const int PhaseLandingTick = 636;
        public int PhaseTransitionStartTick = -1;
        public bool PhaseTransitionActive => Mode == EncounterMode.Fighting && PhaseTransitionStartTick >= 0 && TickIndex < PhaseTransitionStartTick + PhaseTransitionTicks;
        public bool CinematicActive => IntroActive || PhaseTransitionActive;
        public bool PhaseTwoBattlefieldActive => Boss.Phase >= 2 && (PhaseTransitionStartTick < 0 || TickIndex >= PhaseTransitionStartTick + PhaseBattlefieldTick);

        private void StartPhaseTransition()
        {
            PhaseTransitionStartTick = TickIndex;
            Boss.TransitionTime = PhaseTransitionTicks * FixedStep;
            // Cancel combat actions; retain persistent room hazards at their frozen age.
            foreach (var zone in Zones.ToArray())
                if (zone.FromPlayer || !IsRoomHazard(zone)) RemoveZone(zone);
            foreach (var player in Players)
            {
                CancelPlayerAction(player);
                player.AttackTime = player.DashTime = player.LungeTime = player.LungeDelay = 0f;
                player.JumpHeight = player.JumpVelocity = player.LandingTime = 0f;
                player.SkillLeapAge = -1f;
                player.Running = false;
                System.Array.Clear(_inputBuffers[player.Id], 0, _inputBuffers[player.Id].Length);
            }
            // Reissue an old pending room proposal after the pause, with enough lead time.
            PendingRoomMarkTick = 0;
            NextRoomMarkTick = System.Math.Max(NextRoomMarkTick, TickIndex + 31);
            Emit("PhaseTransition", Boss.Position, PhaseTransitionTicks, -1);
        }

        private bool StepPhaseTransition()
        {
            if (PhaseTransitionStartTick < 0) return false;
            int age = TickIndex - PhaseTransitionStartTick;
            if (age <= 0 || age > PhaseTransitionTicks) return false;
            NextRoomMarkTick++;
            if (MarkTargets.Length > 0) MarkDeadline++;
            if (WipeDeadlineTick > 0) WipeDeadlineTick++;
            foreach (var fire in RoomFires) { fire.StartTick++; fire.EndTick++; fire.NextPulse++; }
            foreach (var player in Players)
            {
                player.RoomChangedTick++;
                if (player.NextPotionTick > 0) player.NextPotionTick++;
                if (player.NextChargeBurnTick > 0) player.NextChargeBurnTick++;
            }
            Boss.TransitionTime = (PhaseTransitionTicks - age) * FixedStep;
            if (age == PhaseBattlefieldTick) Emit("PhaseBattlefield", Boss.Position, 2f, -1);
            if (age == PhaseLandingTick) Emit("PhaseLanding", Boss.Position, 2f, -1);
            if (age == PhaseTransitionTicks)
            {
                Boss.PatternName = "폭룡왕 · 2페이즈";
                Emit("PhaseReady", Boss.Position, 2f, -1);
            }
            return true;
        }

        private bool StepEncounterIntro()
        {
            if (!IntroEnabled || TickIndex > EncounterIntroTicks) return false;
            // Discard combat commands during entry. No timers, movement, MP,
            // cooldowns, hazards or combat time advance behind the camera.
            if (TickIndex == EncounterLandingTick) Emit("EncounterLanding", Boss.Position, 0, -1);
            if (TickIndex == EncounterIntroTicks)
            {
                Boss.PatternName = "전투 시작";
                Emit("EncounterReady", Boss.Position, 0, -1);
            }
            return true;
        }
    }
}
