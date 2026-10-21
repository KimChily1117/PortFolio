using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.DB
{
    public class AppDbContext : DbContext
    {
        public DbSet<AccountDb> Accounts { get; set; }
        public DbSet<PlayerDb> Players { get; set; }

        public DbSet<ItemDb> Items { get; set; }
        public DbSet<MatchHistoryDb> MatchHistories { get; set; }
        public DbSet<MatchHistoryMemberDb> MatchHistoryMembers { get; set; }

        static readonly ILoggerFactory _logger = LoggerFactory.Create(
            builder => { builder.AddConsole();});

        readonly string _connectionString;

        public AppDbContext() : this(null)
        {
        }

        public AppDbContext(string connectionString)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? DatabaseSettings.GetConnectionString()
                : connectionString;
        }




        protected override void OnConfiguring(DbContextOptionsBuilder option)
        {
            option.UseLoggerFactory(_logger);
            if(DatabaseSettings.IsMySql) {
#if NET10_0_OR_GREATER
                option.UseMySQL(_connectionString, sql => sql.CommandTimeout(10));
#else
                throw new NotSupportedException("Use the .NET 10 server for MySQL.");
#endif
            } else option.UseSqlServer(_connectionString, sql => sql.CommandTimeout(10));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if(DatabaseSettings.IsMySql) {
                // Explicit lowercase table names also work on case-sensitive Linux hosts.
                modelBuilder.Entity<AccountDb>().ToTable("account");
                modelBuilder.Entity<PlayerDb>().ToTable("player");
                modelBuilder.Entity<ItemDb>().ToTable("item");
                modelBuilder.Entity<MatchHistoryDb>().ToTable("matchhistory");
                modelBuilder.Entity<MatchHistoryMemberDb>().ToTable("matchhistorymember");
                modelBuilder.Entity<AccountDb>().Property(p=>p.AccountName).HasMaxLength(450);
                modelBuilder.Entity<PlayerDb>().Property(p=>p.PlayerName).HasMaxLength(450);
                modelBuilder.Entity<MatchHistoryDb>().Property<string>("ServerRunId").HasMaxLength(32);
                modelBuilder.Entity<MatchHistoryDb>().HasIndex("ServerRunId", nameof(MatchHistoryDb.PartyId)).IsUnique();
            }
            modelBuilder.Entity<AccountDb>()
                .HasIndex(a => a.AccountName)
                .IsUnique();
            modelBuilder.Entity<PlayerDb>()
                .HasIndex(a => a.PlayerName)
                .IsUnique();
            modelBuilder.Entity<PlayerDb>().Property(p => p.Appearance).HasMaxLength(512);

            modelBuilder.Entity<MatchHistoryDb>()
                .HasIndex(m => m.PartyId);

            modelBuilder.Entity<MatchHistoryMemberDb>()
                .HasOne(m => m.MatchHistory)
                .WithMany(h => h.Members)
                .HasForeignKey(m => m.MatchHistoryId);
        }

    }
}
