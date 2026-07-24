using Server.Game.Room;
using Server.Game.Match;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Monitoring
{
    public class MonitoringApiHost
    {
        private readonly RoomManager _roomManager;
        private readonly MonitoringApiOptions _options;
        private readonly DateTime _startedAtUtc;
        private readonly JsonSerializerOptions _jsonOptions;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        public MonitoringApiHost(RoomManager roomManager, MonitoringApiOptions options)
        {
            _roomManager = roomManager ?? throw new ArgumentNullException(nameof(roomManager));
            _options = options ?? new MonitoringApiOptions();
            _startedAtUtc = DateTime.UtcNow;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            };
        }

        public void Start()
        {
            if (_listener != null)
                return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add(_options.UrlPrefix);

            Console.WriteLine($"[MONITOR_API] Starting. Url={_options.UrlPrefix.TrimEnd('/')}");
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            Console.WriteLine($"[MONITOR_API] Started. Url={_options.UrlPrefix.TrimEnd('/')}");
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                Console.WriteLine("[MONITOR_API] Stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MONITOR_API][WARN] Stop failed. {ex.Message}");
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestSafe(context), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MONITOR_API][WARN] Listen failed. {ex.Message}");
                }
            }
        }

        private void HandleRequestSafe(HttpListenerContext context)
        {
            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MONITOR_API][WARN] Request failed. Path={context?.Request?.Url?.AbsolutePath}, Error={ex.Message}");
                if (context != null)
                    WriteJson(context.Response, 500, new { error = "InternalServerError" });
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            AddCommonHeaders(response);

            if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) == false)
            {
                WriteJson(response, 405, new { error = "MethodNotAllowed" });
                return;
            }

            string path = request.Url.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(path))
                path = "/";

            if (string.Equals(path, "/api/health", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(response, 200, new
                {
                    ok = true,
                    service = _options.ServerName,
                    utcNow = DateTime.UtcNow
                });
                return;
            }

            if (string.Equals(path, "/api/server/status", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(response, 200, CreateServerStatusSnapshot());
                return;
            }

            if (string.Equals(path, "/api/players/online", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(response, 200, SessionManager.Instance.CreateOnlinePlayersSnapshot());
                return;
            }

            if (string.Equals(path, "/api/matching/queue", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(response, 200, MatchManager.Instance.CreateQueueSnapshot());
                return;
            }

            
            if (string.Equals(path, "/api/events/recent", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(response, 200, RecentEventBuffer.Snapshot());
                return;
            }

            if (string.Equals(path, "/api/rooms", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(response, 200, _roomManager.GetRoomSnapshots());
                return;
            }

            const string roomPrefix = "/api/rooms/";
            if (path.StartsWith(roomPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string idPart = path.Substring(roomPrefix.Length);
                int roomId;
                if (int.TryParse(idPart, out roomId) == false)
                {
                    WriteJson(response, 400, new { error = "InvalidRoomId", roomId = idPart });
                    return;
                }

                RoomSnapshot snapshot = _roomManager.GetRoomSnapshot(roomId);
                if (snapshot == null)
                {
                    WriteJson(response, 404, new { error = "RoomNotFound", roomId = roomId });
                    return;
                }

                WriteJson(response, 200, snapshot);
                return;
            }

            WriteJson(response, 404, new { error = "NotFound", path = path });
        }

        private ServerStatusSnapshot CreateServerStatusSnapshot()
        {
            List<RoomSnapshot> rooms = _roomManager.GetRoomSnapshots();
            DateTime nowUtc = DateTime.UtcNow;
            OnlinePlayersSnapshot onlinePlayers = SessionManager.Instance.CreateOnlinePlayersSnapshot();
            MatchingQueuesSnapshot matchingQueues = MatchManager.Instance.CreateQueueSnapshot();

            return new ServerStatusSnapshot
            {
                Online = true,
                StartedAtUtc = _startedAtUtc,
                SnapshotUpdatedAtUtc = _roomManager.SnapshotUpdatedAtUtc,
                UptimeSeconds = Math.Max(0, (nowUtc - _startedAtUtc).TotalSeconds),
                TotalRooms = rooms.Count,
                TownRooms = rooms.Count(r => string.Equals(r.RoomType, "Town", StringComparison.OrdinalIgnoreCase)),
                DungeonRooms = rooms.Count(r => r.IsDungeonRoom),
                TotalPlayers = rooms.Sum(r => r.PlayerCount),
                TotalEnemies = rooms.Sum(r => r.EnemyCount),
                OnlinePlayers = onlinePlayers.Count,
                MatchingWaitingPlayers = matchingQueues.TotalWaitingPlayers,
                MatchingQueueCount = matchingQueues.QueueCount,
                MaxRoomUpdateMs = rooms.Count == 0 ? 0 : rooms.Max(r => r.MaxUpdateMs),
                LastMaxRoomUpdateMs = rooms.Count == 0 ? 0 : rooms.Max(r => r.LastUpdateMs),
                ServerName = _options.ServerName,
                ApiVersion = _options.ApiVersion
            };
        }

        private void AddCommonHeaders(HttpListenerResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            response.Headers["Cache-Control"] = "no-store";
        }

        private void WriteJson(HttpListenerResponse response, int statusCode, object payload)
        {
            string json = JsonSerializer.Serialize(payload, _jsonOptions);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}




