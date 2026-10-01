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
            option.UseLoggerFactory(_logger)
                .UseSqlServer(_connectionString, sql => sql.CommandTimeout(10));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountDb>()
                .HasIndex(a => a.AccountName)
                .IsUnique();
            modelBuilder.Entity<PlayerDb>()
                .HasIndex(a => a.PlayerName)
                .IsUnique();

            modelBuilder.Entity<MatchHistoryDb>()
                .HasIndex(m => m.PartyId);

            modelBuilder.Entity<MatchHistoryMemberDb>()
                .HasOne(m => m.MatchHistory)
                .WithMany(h => h.Members)
                .HasForeignKey(m => m.MatchHistoryId);
        }

    }
}
