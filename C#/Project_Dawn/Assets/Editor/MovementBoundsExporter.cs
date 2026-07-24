using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public sealed class MovementBoundsExporter : EditorWindow
{
    private const int FirstCellLogLimit = 12;

    [SerializeField] private Tilemap walkableTilemap;
    [SerializeField] private string roomType = "Bakal";
    [SerializeField] private string sceneName = "Bakal";
    [SerializeField] private string coordinateBasis = "CombatAnchor";
    [SerializeField] private string exportPath;

    [MenuItem("KIMCHILY_TOOL/Movement Bounds/Exporter")]
    public static void ShowWindow()
    {
        MovementBoundsExporter window = GetWindow<MovementBoundsExporter>("Movement Bounds");
        window.minSize = new Vector2(520f, 260f);
        window.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(exportPath))
            exportPath = Path.Combine(Application.dataPath, "Resources/Data/MovementBounds.json").Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(sceneName))
            sceneName = SceneManager.GetActiveScene().name;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tilemap Movement Bounds Export v1", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select the Tilemap that represents walkable cells. Occupied cells are compressed into world-space spans for the server. Coordinates are intended for the character foot/shadow/combat anchor, not prefab root.", MessageType.Info);

        walkableTilemap = (Tilemap)EditorGUILayout.ObjectField("Walkable Tilemap", walkableTilemap, typeof(Tilemap), true);
        roomType = EditorGUILayout.TextField("Room Type", roomType);
        sceneName = EditorGUILayout.TextField("Scene Name", sceneName);
        coordinateBasis = EditorGUILayout.TextField("Coordinate Basis", coordinateBasis);
        exportPath = EditorGUILayout.TextField("Export Path", exportPath);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Find Tilemap"))
            walkableTilemap = FindDefaultTilemap();

        if (GUILayout.Button("Export"))
            Export();
        EditorGUILayout.EndHorizontal();
    }

    private static Tilemap FindDefaultTilemap()
    {
        Tilemap[] tilemaps = FindObjectsOfType<Tilemap>();
        if (tilemaps == null || tilemaps.Length == 0)
            return null;

        foreach (Tilemap tilemap in tilemaps)
        {
            string name = tilemap.gameObject.name ?? string.Empty;
            if (name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0)
                return tilemap;
        }

        foreach (Tilemap tilemap in tilemaps)
        {
            if (string.Equals(tilemap.gameObject.name, "Tilemap", StringComparison.OrdinalIgnoreCase))
                return tilemap;
        }

        return tilemaps[0];
    }

    private void Export()
    {
        if (walkableTilemap == null)
        {
            EditorUtility.DisplayDialog("Movement Bounds", "Walkable Tilemap is required.", "OK");
            return;
        }

        ExportDiagnostics diagnostics = new ExportDiagnostics();
        MovementBoundsMap map = BuildMap(walkableTilemap, diagnostics);
        LogDiagnostics(walkableTilemap, map, diagnostics);

        if (map.walkableSpans.Count == 0)
        {
            EditorUtility.DisplayDialog("Movement Bounds", "No occupied cells were found in the selected Tilemap. Check the [BOUNDS_EXPORT] HasTileCount log.", "OK");
            return;
        }

        MovementBoundsFile file = BuildOutputFile(map);
        string json = JsonUtility.ToJson(file, true);
        string directory = Path.GetDirectoryName(exportPath);
        if (string.IsNullOrWhiteSpace(directory) == false)
            Directory.CreateDirectory(directory);

        File.WriteAllText(exportPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[MOVEMENT_BOUNDS_EXPORT] Exported RoomType={map.roomType}, Scene={map.sceneName}, Tilemap={map.sourceTilemap}, Basis={map.coordinateBasis}, HasTileCount={map.hasTileCount}, Spans={map.walkableSpans.Count}, Bounds=({map.minX:0.00},{map.minY:0.00})..({map.maxX:0.00},{map.maxY:0.00}), Path={exportPath}");
        EditorUtility.DisplayDialog("Movement Bounds", $"Exported {map.walkableSpans.Count} spans from {map.hasTileCount} cells.\n{exportPath}", "OK");
    }


    private MovementBoundsFile BuildOutputFile(MovementBoundsMap map)
    {
        MovementBoundsFile file = null;
        if (File.Exists(exportPath))
        {
            try
            {
                file = JsonUtility.FromJson<MovementBoundsFile>(File.ReadAllText(exportPath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MOVEMENT_BOUNDS_EXPORT] Existing file could not be parsed. It will be replaced. Path={exportPath}, Error={ex.Message}");
            }
        }

        if (file == null)
            file = new MovementBoundsFile();

        file.version = 1;
        file.generatedAtUtc = DateTime.UtcNow.ToString("o");
        if (file.maps == null)
            file.maps = new List<MovementBoundsMap>();

        file.maps.RemoveAll(existing => existing != null && string.Equals(existing.roomType, map.roomType, StringComparison.OrdinalIgnoreCase));
        file.maps.Add(map);
        return file;
    }
    private MovementBoundsMap BuildMap(Tilemap tilemap, ExportDiagnostics diagnostics)
    {
        BoundsInt bounds = tilemap.cellBounds;
        MovementBoundsMap map = new MovementBoundsMap
        {
            roomType = roomType,
            sceneName = string.IsNullOrWhiteSpace(sceneName) ? SceneManager.GetActiveScene().name : sceneName,
            sourceTilemap = tilemap.gameObject.name,
            coordinateBasis = string.IsNullOrWhiteSpace(coordinateBasis) ? "CombatAnchor" : coordinateBasis,
            minX = float.MaxValue,
            minY = float.MaxValue,
            maxX = float.MinValue,
            maxY = float.MinValue,
            hasTileCount = 0,
            walkableSpans = new List<MovementSpan>()
        };

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            int runStartX = int.MinValue;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                bool hasTile = tilemap.HasTile(cell);
                if (hasTile)
                {
                    map.hasTileCount++;
                    CaptureFirstCellDiagnostic(tilemap, cell, diagnostics);
                    if (runStartX == int.MinValue)
                        runStartX = x;
                }

                bool isLastX = x == bounds.xMax - 1;
                if (runStartX != int.MinValue && (!hasTile || isLastX))
                {
                    int runEndExclusive = hasTile && isLastX ? x + 1 : x;
                    AddSpan(tilemap, map, runStartX, runEndExclusive, y);
                    runStartX = int.MinValue;
                }
            }
        }

        if (map.walkableSpans.Count == 0)
        {
            map.minX = 0f;
            map.minY = 0f;
            map.maxX = 0f;
            map.maxY = 0f;
        }

        return map;
    }

    private static void AddSpan(Tilemap tilemap, MovementBoundsMap map, int xStart, int xEndExclusive, int y)
    {
        Grid grid = tilemap.layoutGrid;
        Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;

        Vector3 firstCenter = tilemap.GetCellCenterWorld(new Vector3Int(xStart, y, 0));
        Vector3 lastCenter = tilemap.GetCellCenterWorld(new Vector3Int(xEndExclusive - 1, y, 0));

        float minX = Mathf.Min(firstCenter.x, lastCenter.x) - Mathf.Abs(cellSize.x) * 0.5f;
        float maxX = Mathf.Max(firstCenter.x, lastCenter.x) + Mathf.Abs(cellSize.x) * 0.5f;
        float minY = Mathf.Min(firstCenter.y, lastCenter.y) - Mathf.Abs(cellSize.y) * 0.5f;
        float maxY = Mathf.Max(firstCenter.y, lastCenter.y) + Mathf.Abs(cellSize.y) * 0.5f;

        MovementSpan span = new MovementSpan
        {
            xMin = minX,
            xMax = maxX,
            yMin = minY,
            yMax = maxY
        };
        map.walkableSpans.Add(span);

        map.minX = Mathf.Min(map.minX, minX);
        map.minY = Mathf.Min(map.minY, minY);
        map.maxX = Mathf.Max(map.maxX, maxX);
        map.maxY = Mathf.Max(map.maxY, maxY);
    }

    private static void CaptureFirstCellDiagnostic(Tilemap tilemap, Vector3Int cell, ExportDiagnostics diagnostics)
    {
        if (diagnostics.firstCells.Count >= FirstCellLogLimit)
            return;

        Grid grid = tilemap.layoutGrid;
        Vector3 center = tilemap.GetCellCenterWorld(cell);
        Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
        Vector3 min = new Vector3(center.x - Mathf.Abs(cellSize.x) * 0.5f, center.y - Mathf.Abs(cellSize.y) * 0.5f, center.z);
        Vector3 cellToWorld = tilemap.CellToWorld(cell);

        diagnostics.firstCells.Add(new CellDiagnostic
        {
            cell = cell,
            cellToWorld = cellToWorld,
            center = center,
            worldMin = min
        });
    }

    private static void LogDiagnostics(Tilemap tilemap, MovementBoundsMap map, ExportDiagnostics diagnostics)
    {
        Grid grid = tilemap.layoutGrid;
        Transform gridTransform = grid != null ? grid.transform : null;
        BoundsInt bounds = tilemap.cellBounds;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"[BOUNDS_EXPORT] Tilemap={tilemap.gameObject.name}");
        builder.AppendLine($"[BOUNDS_EXPORT] CoordinateBasis={map.coordinateBasis}");
        builder.AppendLine($"[BOUNDS_EXPORT] GridCellSize={(grid != null ? grid.cellSize.ToString() : "<null>")}");
        builder.AppendLine($"[BOUNDS_EXPORT] GridTransform Pos={(gridTransform != null ? gridTransform.position.ToString() : "<null>")}, Scale={(gridTransform != null ? gridTransform.lossyScale.ToString() : "<null>")}");
        builder.AppendLine($"[BOUNDS_EXPORT] TilemapTransform Pos={tilemap.transform.position}, Scale={tilemap.transform.lossyScale}");
        builder.AppendLine($"[BOUNDS_EXPORT] TileAnchor={tilemap.tileAnchor}");
        builder.AppendLine($"[BOUNDS_EXPORT] CellBounds=(xMin={bounds.xMin}, yMin={bounds.yMin}, zMin={bounds.zMin}, sizeX={bounds.size.x}, sizeY={bounds.size.y}, sizeZ={bounds.size.z})");
        builder.AppendLine($"[BOUNDS_EXPORT] HasTileCount={map.hasTileCount}");
        builder.AppendLine("[BOUNDS_EXPORT] FirstCells:");
        foreach (CellDiagnostic cell in diagnostics.firstCells)
            builder.AppendLine($"  Cell={cell.cell}, CellToWorld={cell.cellToWorld}, Center={cell.center}, WorldMin={cell.worldMin}");
        builder.AppendLine($"[BOUNDS_EXPORT] Result Min=({map.minX:0.00},{map.minY:0.00}), Max=({map.maxX:0.00},{map.maxY:0.00}), SpanCount={map.walkableSpans.Count}");

        Debug.Log(builder.ToString());
    }

    private sealed class ExportDiagnostics
    {
        public readonly List<CellDiagnostic> firstCells = new List<CellDiagnostic>();
    }

    private sealed class CellDiagnostic
    {
        public Vector3Int cell;
        public Vector3 cellToWorld;
        public Vector3 center;
        public Vector3 worldMin;
    }

    [Serializable]
    private sealed class MovementBoundsFile
    {
        public int version;
        public string generatedAtUtc;
        public List<MovementBoundsMap> maps;
    }

    [Serializable]
    private sealed class MovementBoundsMap
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
        public List<MovementSpan> walkableSpans;
    }

    [Serializable]
    private sealed class MovementSpan
    {
        public float xMin;
        public float xMax;
        public float yMin;
        public float yMax;
    }
}