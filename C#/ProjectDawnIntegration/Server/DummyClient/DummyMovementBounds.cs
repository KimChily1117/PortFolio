using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DummyClient
{
    public sealed class DummyMovementBoundsFile
    {
        public int Version { get; set; }
        public string GeneratedAtUtc { get; set; }
        public List<DummyMovementBoundsMap> Maps { get; set; }
    }

    public sealed class DummyMovementBoundsMap
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
        public List<DummyMovementSpan> WalkableSpans { get; set; }

        public bool TryClamp(float x, float y, out float clampedX, out float clampedY)
        {
            clampedX = x;
            clampedY = y;

            if (WalkableSpans == null || WalkableSpans.Count == 0)
                return false;

            if (IsWalkable(x, y))
                return true;

            float bestX = x;
            float bestY = y;
            float bestDistanceSq = float.MaxValue;
            foreach (DummyMovementSpan span in WalkableSpans)
            {
                float candidateX = Math.Max(span.XMin, Math.Min(span.XMax, x));
                float candidateY = Math.Max(span.YMin, Math.Min(span.YMax, y));
                float dx = candidateX - x;
                float dy = candidateY - y;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestX = candidateX;
                bestY = candidateY;
            }

            clampedX = bestX;
            clampedY = bestY;
            return true;
        }

        public bool IsWalkable(float x, float y)
        {
            if (WalkableSpans == null || WalkableSpans.Count == 0)
                return false;

            if (x < MinX || x > MaxX || y < MinY || y > MaxY)
                return false;

            foreach (DummyMovementSpan span in WalkableSpans)
            {
                if (x >= span.XMin && x <= span.XMax && y >= span.YMin && y <= span.YMax)
                    return true;
            }

            return false;
        }
    }

    public sealed class DummyMovementSpan
    {
        public float XMin { get; set; }
        public float XMax { get; set; }
        public float YMin { get; set; }
        public float YMax { get; set; }
    }

    public static class DummyMovementBoundsProvider
    {
        private static readonly object Lock = new object();
        private static bool _loaded;
        private static readonly Dictionary<RoomType, DummyMovementBoundsMap> Maps = new Dictionary<RoomType, DummyMovementBoundsMap>();

        public static bool TryGet(RoomType roomType, out DummyMovementBoundsMap map)
        {
            EnsureLoaded();
            lock (Lock)
            {
                return Maps.TryGetValue(roomType, out map);
            }
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
                    Console.WriteLine("[DUMMY][MOVEMENT_BOUNDS] MovementBounds.json not found. Using CLI rectangle bounds.");
                    return;
                }

                try
                {
                    JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    DummyMovementBoundsFile file = JsonSerializer.Deserialize<DummyMovementBoundsFile>(File.ReadAllText(path), options);
                    if (file?.Maps == null)
                    {
                        Console.WriteLine($"[DUMMY][MOVEMENT_BOUNDS][WARN] No maps loaded. Path={path}");
                        return;
                    }

                    int loadedCount = 0;
                    foreach (DummyMovementBoundsMap map in file.Maps)
                    {
                        if (map == null || string.IsNullOrWhiteSpace(map.RoomType))
                            continue;

                        if (Enum.TryParse(map.RoomType, true, out RoomType roomType) == false)
                        {
                            Console.WriteLine($"[DUMMY][MOVEMENT_BOUNDS][WARN] Unknown RoomType={map.RoomType}");
                            continue;
                        }

                        Maps[roomType] = map;
                        loadedCount++;
                        Console.WriteLine($"[DUMMY][MOVEMENT_BOUNDS] Map loaded. RoomType={roomType}, SourceTilemap={map.SourceTilemap}, CoordinateBasis={map.CoordinateBasis}, HasTileCount={map.HasTileCount}, SpanCount={map.WalkableSpans?.Count ?? 0}, Bounds=({map.MinX:0.00},{map.MinY:0.00})..({map.MaxX:0.00},{map.MaxY:0.00})");
                    }

                    Console.WriteLine($"[DUMMY][MOVEMENT_BOUNDS] Loaded. Path={path}, Maps={loadedCount}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DUMMY][MOVEMENT_BOUNDS][WARN] Failed to load MovementBounds.json. Path={path}, Error={ex.Message}");
                }
            }
        }

        private static string ResolveMovementBoundsPath()
        {
            string baseDir = AppContext.BaseDirectory;
            string cwd = Directory.GetCurrentDirectory();
            string[] candidates =
            {
                Path.Combine(cwd, "Data", "MovementBounds.json"),
                Path.Combine(baseDir, "Data", "MovementBounds.json"),
                Path.Combine(baseDir, "../../../../../../C#/Project_Dawn/Assets/Resources/Data/MovementBounds.json"),
                Path.Combine(baseDir, "../../../../../../Unity Project/Project_Dawn/Assets/Resources/Data/MovementBounds.json"),
                Path.Combine(cwd, "..", "..", "C#", "Project_Dawn", "Assets", "Resources", "Data", "MovementBounds.json"),
                Path.Combine(cwd, "..", "C#", "Project_Dawn", "Assets", "Resources", "Data", "MovementBounds.json")
            };

            foreach (string candidate in candidates)
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
