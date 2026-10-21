using Google.Protobuf.Protocol;
using System.Linq;

namespace Server.Game.Match
{
    public static class MatchProfileProvider
    {
        private const int DefaultLevel = 1;
        private const int DefaultLevelBucket = 0;
        private const int DefaultMmr = 1000;
        private const int DefaultMmrBucket = 1000;

        public static bool TryBuild(ClientSession session, MatchMode mode, RoomType targetRoomType, out MatchProfile profile, out string rejectReason)
        {
            profile = null;
            rejectReason = null;

            if (session == null || session.MyPlayer == null || session.MyPlayer.Info == null)
            {
                rejectReason = "InvalidSession";
                return false;
            }

            bool hasEquippedWeapon = session.MyPlayer.Inven.Items.Values.Any(item =>
                item != null &&
                item.Equipped &&
                item.ItemType == ItemType.Weapon);

            bool hasEquippedArmor = session.MyPlayer.Inven.Items.Values.Any(item =>
                item != null &&
                item.Equipped &&
                item.ItemType == ItemType.Armor);

            profile = new MatchProfile()
            {
                PlayerId = session.MyPlayer.Id,
                PlayerName = session.MyPlayer.Info.Name,
                Mode = mode,
                TargetRoomType = targetRoomType,
                Level = DefaultLevel,
                LevelBucket = DefaultLevelBucket,
                Mmr = DefaultMmr,
                MmrBucket = DefaultMmrBucket,
                HasEquippedWeapon = hasEquippedWeapon,
                HasEquippedArmor = hasEquippedArmor
            };
            profile.QueueKey = MatchQueueKey.Create(profile.Mode, profile.TargetRoomType, profile.LevelBucket, profile.MmrBucket);

            if (hasEquippedWeapon == false || hasEquippedArmor == false)
            {
                rejectReason = "MissingRequiredEquipment";
                return false;
            }

            return true;
        }
    }
}
