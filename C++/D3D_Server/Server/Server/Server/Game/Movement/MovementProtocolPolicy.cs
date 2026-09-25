using System;

namespace Server.Game.Movement
{
    public static class MovementProtocolPolicy
    {
        public static bool AuthoritativeMoveRequestsEnabled => true;

        // Legacy C_Move is disabled by default because it immediately mutates position.
        // Set D3D_ALLOW_LEGACY_MOVE=1 only for an explicit development compatibility run.
        public static bool LegacyMoveEnabled =>
            string.Equals(Environment.GetEnvironmentVariable("D3D_ALLOW_LEGACY_MOVE"), "1", StringComparison.Ordinal);
    }
}