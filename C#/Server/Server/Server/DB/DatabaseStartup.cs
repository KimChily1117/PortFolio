using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Server.DB
{
    public static class DatabaseStartup
    {
        public static void Verify(bool initializeDatabase)
        {
            using (AppDbContext db = new AppDbContext())
            {
                // Existing databases are never dropped or recreated.
                if (initializeDatabase)
                {
                    bool created = db.Database.EnsureCreated();
                    Console.WriteLine($"[DB] Initialize complete. Created={created}, Provider=SqlServer");
                }

                db.Database.OpenConnection();
                db.Accounts.AsNoTracking().Take(1).ToList();
                db.Players.AsNoTracking().Take(1).ToList();
                db.Items.AsNoTracking().Take(1).ToList();
                db.MatchHistories.AsNoTracking().Take(1).ToList();
                db.MatchHistoryMembers.AsNoTracking().Take(1).ToList();
            }
            Console.WriteLine("[DB] Ready. Provider=SqlServer");
        }
    }
}
