using System.Linq;
using Microsoft.EntityFrameworkCore;
namespace Server.DB
{
    public static class AppearanceStore
    {
        // UPDATE and read-back share a transaction. A stale first-login import
        // cannot replace a look already saved by another client.
        public static string Save(AppDbContext db, int accountId, int playerId, string look, bool initializeOnly)
        {
            using (var tx = db.Database.BeginTransaction())
            {
                if(DatabaseSettings.IsMySql) {
                    if(initializeOnly) db.Database.ExecuteSqlInterpolated($@"UPDATE player SET Appearance={look} WHERE PlayerDbId={playerId} AND AccountDbId={accountId} AND Appearance IS NULL");
                    else db.Database.ExecuteSqlInterpolated($@"UPDATE player SET Appearance={look} WHERE PlayerDbId={playerId} AND AccountDbId={accountId}");
                } else {
                if (initializeOnly)
                    db.Database.ExecuteSqlInterpolated($@"UPDATE dbo.Player SET Appearance={look}
                        WHERE PlayerDbId={playerId} AND AccountDbId={accountId} AND Appearance IS NULL");
                else
                    db.Database.ExecuteSqlInterpolated($@"UPDATE dbo.Player SET Appearance={look}
                        WHERE PlayerDbId={playerId} AND AccountDbId={accountId}");
                }
                var saved = db.Players.AsNoTracking().Where(p => p.PlayerDbId == playerId && p.AccountDbId == accountId)
                    .Select(p => p.Appearance).SingleOrDefault();
                if (saved == null) throw new System.InvalidOperationException("Appearance owner was not found.");
                tx.Commit();
                return saved;
            }
        }
    }
}
