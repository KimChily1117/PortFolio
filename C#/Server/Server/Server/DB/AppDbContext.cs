using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Text;
using Pomelo.EntityFrameworkCore.MySql;

namespace Server.DB
{
    public class AppDbContext : DbContext
    {
        public DbSet<AccountDb> Accounts { get; set; }   
        public DbSet<PlayerDb> Players { get; set; }
         
        static readonly ILoggerFactory _logger = LoggerFactory.Create(
            builder => { builder.AddConsole();});

        //string _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GameDB;";
        string _awsConnectionString = @"Server=database-1.cte0o02aow5r.ap-southeast-2.rds.amazonaws.com;Database=GameDB;User Id=kimchily;Password=a987654!;";

        protected override void OnConfiguring(DbContextOptionsBuilder option)
        {
            option.UseLoggerFactory(_logger)
                .UseSqlServer(_awsConnectionString);
         }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountDb>()
                .HasIndex(a => a.AccountName)
                .IsUnique();
            modelBuilder.Entity<PlayerDb>()
                .HasIndex(a => a.PlayerName)
                .IsUnique();
        }

    }
}
