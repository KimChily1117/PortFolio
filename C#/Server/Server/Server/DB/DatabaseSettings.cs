using Server.Data;
using System;

namespace Server.DB
{
    public static class DatabaseSettings
    {
        public static string GetConnectionString()
        {
            string connectionString = Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = ConfigManager.Config?.connectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GameDB;Connect Timeout=5;";
            }
            return connectionString;
        }
    }
}
