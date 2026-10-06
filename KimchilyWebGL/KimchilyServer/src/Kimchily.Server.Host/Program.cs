using Kimchily.Server.Core;

namespace Kimchily.Server.Host
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
            });

            if (string.IsNullOrEmpty(builder.Configuration["urls"]))
            {
                builder.WebHost.UseUrls("http://127.0.0.1:8790");
            }

            // 개발 실행은 run.ps1이 원본 games 폴더를 지정한다. 해시 파일 추가만으로 새 월드 규칙을 제공할 수 있다.
            string scriptsRoot = builder.Configuration["Realtime:ScriptsRoot"]
                ?? Path.Combine(AppContext.BaseDirectory, "games");
            builder.Services.AddSingleton(new ApprovedScriptCatalog(scriptsRoot));
            builder.Services.AddSingleton<RoomHub>();
            builder.Services.AddHostedService<RoomPump>();
            builder.Services.AddSingleton<SocketEndpoint>();

            WebApplication app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; "
                    + "frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
                context.Response.Headers.CacheControl = "no-store";
                await next(context);
            });

            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(20),
                KeepAliveTimeout = TimeSpan.FromSeconds(20)
            });
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapGet("/health", () =>
            {
                return Results.Json(new
                {
                    service = "kimchily-realtime",
                    protocolVersion = Protocol.Version,
                    status = "ready"
                });
            });

            app.MapGet("/api/rooms", async (RoomHub rooms, CancellationToken cancellation) =>
            {
                RoomSummary[] snapshot = await rooms.SnapshotAsync().WaitAsync(cancellation);
                return Results.Json(snapshot, Protocol.Json);
            });

            app.Map("/ws", (HttpContext context, SocketEndpoint endpoint) =>
            {
                return endpoint.RunAsync(context);
            });

            app.Run();
        }
    }

    public sealed class RoomPump : BackgroundService
    {
        private readonly RoomHub _rooms;

        public RoomPump(RoomHub rooms)
        {
            _rooms = rooms;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20)))
            {
                try
                {
                    do
                    {
                        _rooms.TryQueueGameTick();
                        _rooms.Flush();
                    }
                    while (await timer.WaitForNextTickAsync(stoppingToken));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 서비스 종료 요청은 정상적인 펌프 종료 경로이다.
                }
            }
        }
    }
}
