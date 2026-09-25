using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Server.Game.Navigation;

internal static class Program
{
    private static int _assertions;

    private static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void PutUInt32(byte[] bytes, int offset, uint value)
    {
        Check(offset >= 0 && offset + 4 <= bytes.Length, "Mutation offset is outside fixture.");
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static void PutSingle(byte[] bytes, int offset, float value)
    {
        PutUInt32(bytes, offset, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
    }

    private static byte[] Clone(byte[] bytes) => (byte[])bytes.Clone();

    private static void ExpectRejected(string directory, string name, byte[] bytes, NavGridLoadError expected)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".navgrid");
        File.WriteAllBytes(path, bytes);
        NavGridLoadResult result = NavGridAssetLoader.Load(path);
        Check(!result.Success, name + " unexpectedly loaded.");
        Check(result.Error == expected, name + " returned " + result.Error + " instead of " + expected + ".");
    }

    private static float ParseFloat(string value)
    {
        if (value == "NaN") return float.NaN;
        if (value == "Infinity") return float.PositiveInfinity;
        if (value == "-Infinity") return float.NegativeInfinity;
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string CoordinateText(NavGridAsset asset, Vector3 world)
    {
        return asset.TryWorldToCell(world, out NavGridCoordinate coordinate)
            ? coordinate.ToString()
            : "invalid";
    }

    private static string CenterText(Vector3 center)
    {
        return center.X.ToString("F6", CultureInfo.InvariantCulture) + "," +
            center.Y.ToString("F6", CultureInfo.InvariantCulture) + "," +
            center.Z.ToString("F6", CultureInfo.InvariantCulture);
    }

    private static string JsonEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: NavGridTests <golden.navgrid> <coordinate-vectors.csv> <result.json>");
                return 2;
            }

            string goldenPath = args[0];
            string vectorsPath = args[1];
            string resultPath = args[2];
            string mutationDirectory = Path.Combine(Path.GetDirectoryName(resultPath), "csharp-mutations");

            NavGridLoadResult load = NavGridAssetLoader.Load(goldenPath);
            Check(load.Success, "Golden asset load failed: " + load.Message);
            NavGridAsset asset = load.Asset;
            Check(asset.FormatVersion == 1, "FormatVersion mismatch.");
            Check(asset.MapId == "golden-grid-v1", "MapId mismatch.");
            Check(asset.Width == 4 && asset.Height == 3, "Dimensions mismatch.");
            Check(asset.CellSize == 1.0f, "CellSize mismatch.");
            Check(asset.Origin == new Vector3(-2.0f, 0.0f, -1.0f), "Origin mismatch.");
            Check(asset.DefaultAgentRadius == 0.5f, "Agent radius mismatch.");
            Check(asset.ContentHashHex == "d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77", "Golden content hash mismatch.");

            byte[] expectedCells = { 0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0 };
            var cells = new List<byte>();
            for (int z = 0; z < 3; ++z)
            {
                for (int x = 0; x < 4; ++x)
                {
                    byte value = (byte)asset.GetCell(x, z);
                    Check(value == expectedCells[z * 4 + x], "Cell value mismatch.");
                    cells.Add(value);
                }
            }
            byte[] cellHash = NavGridHash.ComputeSha256(cells.ToArray());

            var coordinateResults = new List<KeyValuePair<string, string>>();
            string[] vectorLines = File.ReadAllLines(vectorsPath);
            for (int i = 1; i < vectorLines.Length; ++i)
            {
                if (string.IsNullOrWhiteSpace(vectorLines[i]))
                    continue;
                string[] fields = vectorLines[i].Split(',');
                Check(fields.Length == 5, "Coordinate vector row must have five columns.");
                var world = new Vector3(ParseFloat(fields[1]), ParseFloat(fields[2]), ParseFloat(fields[3]));
                string actual = CoordinateText(asset, world);
                Check(actual == fields[4], "Coordinate mismatch for " + fields[0] + ".");
                coordinateResults.Add(new KeyValuePair<string, string>(fields[0], actual));
            }

            Check(asset.TryCellToWorldCenter(new NavGridCoordinate(0, 0), out Vector3 center00) && center00 == new Vector3(-1.5f, 0.0f, -0.5f), "Cell 0:0 center mismatch.");
            Check(asset.TryCellToWorldCenter(new NavGridCoordinate(3, 2), out Vector3 center32) && center32 == new Vector3(1.5f, 0.0f, 1.5f), "Cell 3:2 center mismatch.");
            Check(!asset.TryCellToWorldCenter(new NavGridCoordinate(-1, 0), out _), "Invalid cell center must be rejected.");

            byte[] golden = File.ReadAllBytes(goldenPath);
            const int widthOffset = 30;
            const int heightOffset = 34;
            const int cellSizeOffset = 38;
            const int originXOffset = 42;
            const int agentRadiusOffset = 54;
            const int encodingOffset = 58;
            const int dataLengthOffset = 62;
            const int hashOffset = 66;
            const int dataOffset = 98;

            byte[] mutated = Clone(golden); mutated[0] = (byte)'X';
            ExpectRejected(mutationDirectory, "bad_magic", mutated, NavGridLoadError.InvalidMagic);
            mutated = Clone(golden); PutUInt32(mutated, 4, 2);
            ExpectRejected(mutationDirectory, "unsupported_version", mutated, NavGridLoadError.UnsupportedVersion);
            mutated = Clone(golden); PutUInt32(mutated, 8, 99);
            ExpectRejected(mutationDirectory, "bad_header_size", mutated, NavGridLoadError.InvalidHeaderSize);
            mutated = Clone(golden); PutUInt32(mutated, 12, 129);
            ExpectRejected(mutationDirectory, "map_id_too_long", mutated, NavGridLoadError.MapIdTooLong);
            mutated = Clone(golden); PutUInt32(mutated, widthOffset, 0);
            ExpectRejected(mutationDirectory, "width_zero", mutated, NavGridLoadError.InvalidDimensions);
            mutated = Clone(golden); PutUInt32(mutated, heightOffset, 0);
            ExpectRejected(mutationDirectory, "height_zero", mutated, NavGridLoadError.InvalidDimensions);
            mutated = Clone(golden); PutUInt32(mutated, widthOffset, 4096); PutUInt32(mutated, heightOffset, 4096);
            ExpectRejected(mutationDirectory, "cell_count_too_large", mutated, NavGridLoadError.CellCountTooLarge);
            mutated = Clone(golden); PutUInt32(mutated, widthOffset, uint.MaxValue);
            ExpectRejected(mutationDirectory, "dimension_overflow", mutated, NavGridLoadError.InvalidDimensions);
            mutated = Clone(golden); PutSingle(mutated, cellSizeOffset, 0.0f);
            ExpectRejected(mutationDirectory, "cell_size_zero", mutated, NavGridLoadError.InvalidCellSize);
            mutated = Clone(golden); PutSingle(mutated, cellSizeOffset, -1.0f);
            ExpectRejected(mutationDirectory, "cell_size_negative", mutated, NavGridLoadError.InvalidCellSize);
            mutated = Clone(golden); mutated[16] = 0xff;
            ExpectRejected(mutationDirectory, "invalid_utf8_map_id", mutated, NavGridLoadError.InvalidUtf8MapId);
            mutated = Clone(golden); PutSingle(mutated, cellSizeOffset, float.NaN);
            ExpectRejected(mutationDirectory, "cell_size_nan", mutated, NavGridLoadError.InvalidCellSize);
            mutated = Clone(golden); PutSingle(mutated, cellSizeOffset, float.PositiveInfinity);
            ExpectRejected(mutationDirectory, "cell_size_infinity", mutated, NavGridLoadError.InvalidCellSize);
            mutated = Clone(golden); PutSingle(mutated, originXOffset, float.PositiveInfinity);
            ExpectRejected(mutationDirectory, "origin_infinity", mutated, NavGridLoadError.InvalidOrigin);
            mutated = Clone(golden); PutSingle(mutated, agentRadiusOffset, -0.5f);
            ExpectRejected(mutationDirectory, "agent_radius_negative", mutated, NavGridLoadError.InvalidAgentRadius);
            mutated = Clone(golden); PutUInt32(mutated, encodingOffset, 99);
            ExpectRejected(mutationDirectory, "unknown_encoding", mutated, NavGridLoadError.UnsupportedCellEncoding);
            mutated = new byte[20]; Array.Copy(golden, mutated, mutated.Length);
            ExpectRejected(mutationDirectory, "truncated_header", mutated, NavGridLoadError.TruncatedHeader);
            mutated = new byte[golden.Length - 1]; Array.Copy(golden, mutated, mutated.Length);
            ExpectRejected(mutationDirectory, "truncated_cell_data", mutated, NavGridLoadError.TruncatedCellData);
            mutated = Clone(golden); PutUInt32(mutated, dataLengthOffset, uint.MaxValue);
            ExpectRejected(mutationDirectory, "excessive_cell_data_length", mutated, NavGridLoadError.InvalidCellDataLength);
            mutated = Clone(golden); PutUInt32(mutated, dataLengthOffset, 11);
            ExpectRejected(mutationDirectory, "cell_data_length_mismatch", mutated, NavGridLoadError.InvalidCellDataLength);
            mutated = Clone(golden); mutated[dataOffset] = 3;
            ExpectRejected(mutationDirectory, "unknown_cell", mutated, NavGridLoadError.UnknownCellValue);
            mutated = Clone(golden); mutated[hashOffset] ^= 0x80;
            ExpectRejected(mutationDirectory, "hash_tamper", mutated, NavGridLoadError.HashMismatch);
            mutated = new byte[golden.Length + 1]; Array.Copy(golden, mutated, golden.Length);
            ExpectRejected(mutationDirectory, "trailing_data", mutated, NavGridLoadError.TrailingData);

            _assertions += RuntimeRegistrationTests.Run(goldenPath);
            _assertions += MovementPhase3Tests.Run(goldenPath);
            _assertions += PathfindingPhase4Tests.Run();
            _assertions += MovementPhase5Tests.Run();
            _assertions += CombatPhaseTests.Run();

            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"mapId\": \"").Append(JsonEscape(asset.MapId)).Append("\",\n");
            json.Append("  \"formatVersion\": 1,\n");
            json.Append("  \"width\": 4,\n");
            json.Append("  \"height\": 3,\n");
            json.Append("  \"cellSize\": \"1.000000\",\n");
            json.Append("  \"origin\": \"-2.000000,0.000000,-1.000000\",\n");
            json.Append("  \"defaultAgentRadius\": \"0.500000\",\n");
            json.Append("  \"contentHash\": \"").Append(asset.ContentHashHex).Append("\",\n");
            json.Append("  \"cellBytesHex\": \"000001000001010000000000\",\n");
            json.Append("  \"cellBytesSha256\": \"").Append(NavGridHash.ToHex(cellHash)).Append("\",\n");
            json.Append("  \"worldToCell\": {\n");
            for (int i = 0; i < coordinateResults.Count; ++i)
            {
                json.Append("    \"").Append(JsonEscape(coordinateResults[i].Key)).Append("\": \"").Append(coordinateResults[i].Value).Append("\"");
                json.Append(i + 1 == coordinateResults.Count ? "\n" : ",\n");
            }
            json.Append("  },\n");
            json.Append("  \"cellCenters\": {\n");
            json.Append("    \"0:0\": \"").Append(CenterText(center00)).Append("\",\n");
            json.Append("    \"3:2\": \"").Append(CenterText(center32)).Append("\"\n");
            json.Append("  }\n");
            json.Append("}\n");
            File.WriteAllText(resultPath, json.ToString(), new UTF8Encoding(false));

            Console.WriteLine("C# NavGrid tests passed (" + _assertions + " assertions).");
            Console.WriteLine("Content SHA-256: " + asset.ContentHashHex);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("C# NavGrid tests failed: " + exception.Message);
            return 1;
        }
    }
}