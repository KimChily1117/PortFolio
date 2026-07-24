using Microsoft.EntityFrameworkCore;
using Server.DB;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DbTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: apply <sqlPath> | recent [take] | cleanup");
                return 1;
            }

            using (AppDbContext db = new AppDbContext())
            {
                if (args[0] == "apply")
                {
                    string sql = File.ReadAllText(args[1]);
                    await db.Database.ExecuteSqlRawAsync(sql);
                    Console.WriteLine("APPLIED");
                    return 0;
                }

                if (args[0] == "cleanup")
                {
                    db.MatchHistoryMembers.RemoveRange(db.MatchHistoryMembers);
                    db.MatchHistories.RemoveRange(db.MatchHistories);
                    await db.SaveChangesAsync();
                    Console.WriteLine("CLEANED");
                    return 0;
                }

                if (args[0] == "recent")
                {
                    int take = args.Length > 1 ? int.Parse(args[1]) : 10;
                    var histories = await db.MatchHistories
                        .OrderByDescending(h => h.Id)
                        .Take(take)
                        .ToListAsync();
                    foreach (var h in histories.OrderBy(h => h.Id))
                    {
                        var members = await db.MatchHistoryMembers
                            .Where(m => m.MatchHistoryId == h.Id)
                            .OrderBy(m => m.Id)
                            .ToListAsync();
                        Console.WriteLine($"H Id={h.Id} PartyId={h.PartyId} QueueKey={h.QueueKey} TargetRoomType={h.TargetRoomType} TargetRoomId={h.TargetRoomId} TransferId={h.TransferId} Status={h.ResultStatus} Created={h.CreatedAtUtc:O} TransferStarted={h.TransferStartedAtUtc:O} Entered={h.DungeonEnteredAtUtc:O} Failure={h.FailureReason}");
                        foreach (var m in members)
                            Console.WriteLine($"M HistoryId={m.MatchHistoryId} PlayerId={m.PlayerId} PlayerName={m.PlayerName}");
                    }
                    return 0;
                }
            }

            return 1;
        }
    }
}