using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Server.DB
{
    // 쿼리에 대해서 알아봅시다. 생각해보면 U+DIVE할떄도 LINQ를 자주썻었죠? 근데 쓰는방법을 몰랐어요
    // 게임 제작에 필요한 Data들을 모아놓은 class

    [Table("Account")]
    public class AccountDb
    {
        public int AccountDbId { get; set; }
        public string AccountName { get; set; }
        public ICollection<PlayerDb> Players { get; set; }

    }

    [Table("Player")]
    public class PlayerDb
    {
        public int PlayerDbId { get; set; }
        public string PlayerName { get; set; }

        [ForeignKey("Account")]
        public int AccountDbId { get; set; }
        public AccountDb Account { get; set; }

        public int Level { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Attack { get; set; }
        public float Speed { get; set; }
        public int TotalExp { get; set; }

    }



    [Table("Item")]
    public class ItemDb
    {
        public int ItemDbId { get; set; }
        public int TemplateId { get; set; }
        public int Count { get; set; }
        public int Slot { get; set; }
        public bool Equipped { get; set; } = false;

        [ForeignKey("Owner")]
        public int? OwnerDbId { get; set; }
        public PlayerDb Owner { get; set; }
    }

    [Table("MatchHistory")]
    public class MatchHistoryDb
    {
        public int Id { get; set; }
        public int PartyId { get; set; }
        public string QueueKey { get; set; }
        public string TargetRoomType { get; set; }
        public int? TargetRoomId { get; set; }
        public int? TransferId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? TransferStartedAtUtc { get; set; }
        public DateTime? DungeonEnteredAtUtc { get; set; }
        public string ResultStatus { get; set; }
        public string FailureReason { get; set; }
        public ICollection<MatchHistoryMemberDb> Members { get; set; }
    }

    [Table("MatchHistoryMember")]
    public class MatchHistoryMemberDb
    {
        public int Id { get; set; }

        [ForeignKey("MatchHistory")]
        public int MatchHistoryId { get; set; }
        public MatchHistoryDb MatchHistory { get; set; }

        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
    }

}
