namespace Server.Monitoring
{
    public class MonitoringApiOptions
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8090;
        public string ServerName { get; set; } = "ProjectDawnGameServer";
        public string ApiVersion { get; set; } = "monitoring-v1";

        public string UrlPrefix => $"http://{Host}:{Port}/";
    }
}
