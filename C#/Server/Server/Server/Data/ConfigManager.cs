using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Server.Data
{
    [Serializable]
    public class ServerConfig
    {
        public string dataPath;
        public string connectionString;
    }

    public class ConfigManager
    {
        public static ServerConfig Config
        { get; private set; }

        public static string ConfigPath { get; private set; }

        public static void LoadConfig()
        {
            string path = Environment.GetEnvironmentVariable("PROJECT_DAWN_CONFIG_PATH");
            if (string.IsNullOrWhiteSpace(path))
                path = File.Exists("config.json") ? "config.json" : Path.Combine(AppContext.BaseDirectory, "config.json");

            ConfigPath = Path.GetFullPath(path);
            string text = File.ReadAllText(ConfigPath);
            Config = JsonConvert.DeserializeObject<ServerConfig>(text)
                ?? throw new InvalidDataException("Server config must be a JSON object.");

            if (string.IsNullOrWhiteSpace(Config.dataPath))
                throw new InvalidDataException("Server config must specify dataPath.");

            Config.dataPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ConfigPath), Config.dataPath));
        }


    }


}
