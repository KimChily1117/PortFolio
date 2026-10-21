using Server.Data;
using System;

namespace Server.DB
{
    public static class DatabaseSettings
    {
        // Default remains explicit at launch: legacy tooling can opt into SqlServer.
        public static bool IsMySql => string.Equals(Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_PROVIDER"), "MySql", StringComparison.OrdinalIgnoreCase);
        public static string Provider => IsMySql ? "MySql" : "SqlServer";
        public static string GetConnectionString()
        {
            string connectionString = Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = ConfigManager.Config?.connectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if(IsMySql) throw new InvalidOperationException("MySQL requires an explicit server-only connection string.");
                connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GameDB;Connect Timeout=5;";
            }
            return connectionString;
        }
    }
}
