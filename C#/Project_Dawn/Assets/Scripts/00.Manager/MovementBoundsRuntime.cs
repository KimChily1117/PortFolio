using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MovementBoundsRuntimeFile
{
    public int version;
    public string generatedAtUtc;
    public MovementBoundsRuntimeMap[] maps;
}

[Serializable]
public sealed class MovementBoundsRuntimeMap
{
    public string roomType;
    public string sceneName;
    public string sourceTilemap;
    public string coordinateBasis;
    public float minX;
    public float minY;
    public float maxX;
    public float maxY;
    public int hasTileCount;
    public MovementBoundsRuntimeSpan[] walkableSpans;

    public bool IsWalkable(Vector2 point)
    {
        if (walkableSpans == null || walkableSpans.Length == 0)
            return false;

        if (point.x < minX || point.x > maxX || point.y < minY || point.y > maxY)
            return false;

        foreach (MovementBoundsRuntimeSpan span in walkableSpans)
        {
            if (span == null)
                continue;

            if (point.x >= span.xMin && point.x <= span.xMax && point.y >= span.yMin && point.y <= span.yMax)
                return true;
        }

        return false;
    }

    public bool TryClamp(Vector2 requested, out Vector2 clamped)
    {
        clamped = requested;

        if (walkableSpans == null || walkableSpans.Length == 0)
            return false;

        if (IsWalkable(requested))
            return true;

        float bestDistanceSqr = float.MaxValue;
        foreach (MovementBoundsRuntimeSpan span in walkableSpans)
        {
            if (span == null)
                continue;

            float candidateX = Mathf.Clamp(requested.x, span.xMin, span.xMax);
            float candidateY = Mathf.Clamp(requested.y, span.yMin, span.yMax);
            float dx = candidateX - requested.x;
            float dy = candidateY - requested.y;
            float distanceSqr = dx * dx + dy * dy;

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            clamped = new Vector2(candidateX, candidateY);
        }

        return bestDistanceSqr < float.MaxValue;
    }
}

[Serializable]
public sealed class MovementBoundsRuntimeSpan
{
    public float xMin;
    public float xMax;
    public float yMin;
    public float yMax;
}

public static class MovementBoundsRuntime
{
    private const string ResourcePath = "Data/MovementBounds";
    private static readonly Dictionary<Define.Scenes, MovementBoundsRuntimeMap> MapsByScene = new Dictionary<Define.Scenes, MovementBoundsRuntimeMap>();
    private static bool _loaded;

    public static bool TryGet(Define.Scenes scene, out MovementBoundsRuntimeMap map)
    {
        EnsureLoaded();
        return MapsByScene.TryGetValue(scene, out map);
    }

    public static bool IsWalkable(Define.Scenes scene, Vector2 point)
    {
        return TryGet(scene, out MovementBoundsRuntimeMap map) && map.IsWalkable(point);
    }

    public static bool TryClamp(Define.Scenes scene, Vector2 requested, out Vector2 clamped)
    {
        clamped = requested;
        return TryGet(scene, out MovementBoundsRuntimeMap map) && map.TryClamp(requested, out clamped);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[MOVEMENT_BOUNDS] Resource not found. Path=Resources/{ResourcePath}.json");
            return;
        }

        MovementBoundsRuntimeFile file = JsonUtility.FromJson<MovementBoundsRuntimeFile>(asset.text);
        if (file?.maps == null)
        {
            Debug.LogWarning($"[MOVEMENT_BOUNDS] No maps loaded. Path=Resources/{ResourcePath}.json");
            return;
        }

        int loadedCount = 0;
        foreach (MovementBoundsRuntimeMap map in file.maps)
        {
            if (map == null || string.IsNullOrWhiteSpace(map.roomType))
                continue;

            if (!TrySceneFromRoomType(map.roomType, out Define.Scenes scene))
            {
                Debug.LogWarning($"[MOVEMENT_BOUNDS] Unknown roomType={map.roomType}");
                continue;
            }

            MapsByScene[scene] = map;
            loadedCount++;
            Debug.Log($"[MOVEMENT_BOUNDS] Map loaded. Scene={scene}, SourceTilemap={map.sourceTilemap}, Basis={map.coordinateBasis}, HasTileCount={map.hasTileCount}, Spans={map.walkableSpans?.Length ?? 0}, Bounds=({map.minX:0.00},{map.minY:0.00})..({map.maxX:0.00},{map.maxY:0.00})");
        }

        Debug.Log($"[MOVEMENT_BOUNDS] Loaded. Maps={loadedCount}");
    }

    private static bool TrySceneFromRoomType(string roomType, out Define.Scenes scene)
    {
        scene = Define.Scenes.NONE;
        if (string.Equals(roomType, "Town", StringComparison.OrdinalIgnoreCase))
        {
            scene = Define.Scenes.TOWN;
            return true;
        }

        if (string.Equals(roomType, "Bakal", StringComparison.OrdinalIgnoreCase))
        {
            scene = Define.Scenes.BAKAL;
            return true;
        }

        return false;
    }
}