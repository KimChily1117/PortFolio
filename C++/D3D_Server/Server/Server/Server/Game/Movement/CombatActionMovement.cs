using Google.Protobuf.Protocol;
using Server.Game.Objects;
using Server.Game.Room;

namespace Server.Game.Movement
{
    // Durations match Resources/Models/{Annie,Garen}/*.clip: frameCount / 24 fps.
    // VFX/projectile lifetimes do not extend the movement lock.
    public static class CombatActionMovement
    {
        public static bool TryBegin(GameRoom room, GameObject caster, int skillId)
        {
            if (!(caster is Player player) || room == null || caster.Info == null ||
                skillId < 0 || skillId > 4) return false;
            double duration;
            switch (caster.Info.ChampType)
            {
                case PLAYER_CHAMPION_TYPE.PlayerTypeAnnie:
                    duration = (skillId == 0 ? 37 : skillId == 3 ? 14 : 25) / 24.0;
                    break;
                case PLAYER_CHAMPION_TYPE.PlayerTypeGaren:
                    // Garen W currently uses Idle.clip as its placeholder animation.
                    duration = (skillId == 0 ? 48 : skillId == 1 ? 24 :
                        skillId == 2 ? 52 : skillId == 3 ? 72 : 37) / 24.0;
                    break;
                default: return false;
            }
            if (!player.TryBeginCombatAction(skillId, duration)) return false;
            if (player.IsMovementLockedByCombat) room.CancelPlayerMovement(player);
            return true;
        }

        public static void PlayPlaceholder(GameRoom room, GameObject caster, int skillId)
        {
            // Preserve the existing animation-only skills; do not invent damage/buffs.
            if (!TryBegin(room, caster, skillId)) return;
            room.Broadcast(new S_SkillResult { CasterId = caster.Info.ObjectId, SkillId = skillId });
        }
    }
}
