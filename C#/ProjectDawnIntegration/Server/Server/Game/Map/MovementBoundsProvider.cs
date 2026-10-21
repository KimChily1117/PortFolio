using Google.Protobuf.Protocol;
using Newtonsoft.Json;
using Server.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace Server.Game.Map
{
    public sealed class MovementBoundsFile
    {
        public int Version { get; set; }
        public string GeneratedAtUtc { get; set; }
        public List<MovementBoundsMap> Maps { get; set; }
    }

    public sealed class MovementBoundsMap
    {
        public string RoomType { get; set; }
        public string SceneName { get; set; }
        public string SourceTilemap { get; set; }
        public string CoordinateBasis { get; set; }
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }
        public int HasTileCount { get; set; }
        public List<MovementSpan> WalkableSpans { get; set; }

        public bool TryGetRandomPoint(Random random, out float x, out float y)
        {
            x = 0f;
            y = 0f;

            if (random == null || WalkableSpans == null || WalkableSpans.Count == 0)
                return false;

            MovementSpan span = WalkableSpans[random.Next(WalkableSpans.Count)];
            x = Lerp(span.XMin, span.XMax, (float)random.NextDouble());
            y = Lerp(span.YMin, span.YMax, (float)random.NextDouble());
            return true;
        }

        public bool IsWalkable(float x, float y)
        {
            if (WalkableSpans == null || WalkableSpans.Count == 0)
                return false;

            if (x < MinX || x > MaxX || y < MinY || y > MaxY)
                return false;

            foreach (MovementSpan span in WalkableSpans)
            {
                if (x >= span.XMin && x <= span.XMax && y >= span.YMin && y <= span.YMax)
                    return true;
            }

            return false;
        }

        public bool TryClamp(float x, float y, out float clampedX, out float clampedY)
        {
            clampedX = x;
            clampedY = y;

            if (WalkableSpans == null || WalkableSpans.Count == 0)
                return false;

            if (IsWalkable(x, y))
                return true;

            float bestDistanceSqr = float.MaxValue;
            foreach (MovementSpan span in WalkableSpans)
            {
                float candidateX = Clamp(x, span.XMin, span.XMax);
                float candidateY = Clamp(y, span.YMin, span.YMax);
                float dx = candidateX - x;
                float dy = candidateY - y;
                float distanceSqr = dx * dx + dy * dy;

                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                clampedX = candidateX;
                clampedY = candidateY;
            }

            return bestDistanceSqr < float.MaxValue;
        }

        private static float Lerp(float min, float max, float t)
        {
            return min + (max - min) * t;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }

    public sealed class MovementSpan
    {
        public float XMin { get; set; }
        public float XMax { get; set; }
        public float YMin { get; set; }
        public float YMax { get; set; }
    }

    public static class MovementBoundsProvider
    {
        private static readonly object Lock = new object();
        private static bool _loaded;
        private static readonly Dictionary<RoomType, MovementBoundsMap> Maps = new Dictionary<RoomType, MovementBoundsMap>();

        public static bool TryGet(RoomType roomType, out MovementBoundsMap map)
        {
            EnsureLoaded();
            lock (Lock)
            {
                return Maps.TryGetValue(roomType, out map);
            }
        }

        public static bool TryGetRandomPoint(RoomType roomType, Random random, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            return TryGet(roomType, out MovementBoundsMap map) && map.TryGetRandomPoint(random, out x, out y);
        }

        public static bool IsWalkable(RoomType roomType, float x, float y)
        {
            return TryGet(roomType, out MovementBoundsMap map) && map.IsWalkable(x, y);
        }

        public static bool TryClamp(RoomType roomType, float x, float y, out float clampedX, out float clampedY)
        {
            clampedX = x;
            clampedY = y;
            return TryGet(roomType, out MovementBoundsMap map) && map.TryClamp(x, y, out clampedX, out clampedY);
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            lock (Lock)
            {
                if (_loaded)
                    return;

                _loaded = true;
                string path = ResolveMovementBoundsPath();
                if (string.IsNullOrEmpty(path))
                {
                    Console.WriteLine("[MOVEMENT_BOUNDS] MovementBounds.json not found. Using code fallback bounds.");
                    return;
                }

                try
                {
                    MovementBoundsFile file = JsonConvert.DeserializeObject<MovementBoundsFile>(File.ReadAllText(path));
                    if (file?.Maps == null)
                    {
                        Console.WriteLine($"[MOVEMENT_BOUNDS][WARN] No maps loaded. Path={path}");
                        return;
                    }

                    int loadedCount = 0;
                    foreach (MovementBoundsMap map in file.Maps)
                    {
                        if (map == null || string.IsNullOrWhiteSpace(map.RoomType))
                            continue;

                        if (Enum.TryParse(map.RoomType, true, out RoomType roomType) == false)
                        {
                            Console.WriteLine($"[MOVEMENT_BOUNDS][WARN] Unknown RoomType={map.RoomType}");
                            continue;
                        }

                        Maps[roomType] = map;
                        loadedCount++;
                        Console.WriteLine($"[MOVEMENT_BOUNDS] Map loaded. RoomType={roomType}, SourceTilemap={map.SourceTilemap}, CoordinateBasis={map.CoordinateBasis}, HasTileCount={map.HasTileCount}, SpanCount={map.WalkableSpans?.Count ?? 0}, Bounds=({map.MinX:0.00},{map.MinY:0.00})..({map.MaxX:0.00},{map.MaxY:0.00})");
                    }

                    Console.WriteLine($"[MOVEMENT_BOUNDS] Loaded. Path={path}, Maps={loadedCount}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MOVEMENT_BOUNDS][WARN] Failed to load MovementBounds.json. Path={path}, Error={ex.Message}");
                }
            }
        }

        private static string ResolveMovementBoundsPath()
        {
            List<string> candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(ConfigManager.Config?.dataPath) == false)
                candidates.Add(Path.Combine(ConfigManager.Config.dataPath, "MovementBounds.json"));

            string baseDir = AppContext.BaseDirectory;
            string cwd = Directory.GetCurrentDirectory();
            candidates.Add(Path.Combine(cwd, "Data", "MovementBounds.json"));
            candidates.Add(Path.Combine(baseDir, "Data", "MovementBounds.json"));
            candidates.Add(Path.Combine(baseDir, "../../../../../../C#/Project_Dawn/Assets/Resources/Data/MovementBounds.json"));
            candidates.Add(Path.Combine(baseDir, "../../../../../../Unity Project/Project_Dawn/Assets/Resources/Data/MovementBounds.json"));

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
