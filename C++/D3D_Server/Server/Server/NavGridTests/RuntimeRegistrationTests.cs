using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Server.Game.Navigation;
using Server.Game.Room;

internal static class RuntimeRegistrationTests
{
    private static int _assertions;

    public static int Run(string goldenPath)
    {
        _assertions = 0;
        RunTemporaryContentTests(goldenPath);
        RunDeployedContentAndRoomTests();
        return _assertions;
    }

    private static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ExpectThrows<TException>(Action action, string message)
        where TException : Exception
    {
        ++_assertions;
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static NavigationMapConfig MakeConfig(
        int roomId,
        string mapId,
        string assetPath,
        string contentHash,
        uint version = 1,
        bool required = true)
    {
        return new NavigationMapConfig
        {
            RoomId = roomId,
            SceneId = 0,
            NavigationMapId = mapId,
            AssetPath = assetPath,
            FormatVersion = version,
            ContentHashHex = contentHash,
            Enabled = true,
            Required = required
        };
    }

    private static void RunTemporaryContentTests(string goldenPath)
    {
        string root = Path.Combine(Path.GetTempPath(), "D3D-NavGridTests-" + Guid.NewGuid().ToString("N"));
        string otherWorkingDirectory = Path.Combine(root, "OtherWorkingDirectory");
        string navigationDirectory = Path.Combine(root, "Navigation");
        string relativeAssetPath = "Navigation/golden-grid-v1.navgrid";
        string copiedAssetPath = Path.Combine(navigationDirectory, "golden-grid-v1.navgrid");
        string originalWorkingDirectory = Environment.CurrentDirectory;

        Directory.CreateDirectory(navigationDirectory);
        Directory.CreateDirectory(otherWorkingDirectory);
        File.Copy(goldenPath, copiedAssetPath);

        try
        {
            Check(NavigationContentPath.TryResolveAssetPath(root, relativeAssetPath, out string resolved, out _), "Relative content path must resolve.");
            Check(string.Equals(resolved, copiedAssetPath, StringComparison.OrdinalIgnoreCase), "Resolved content path mismatch.");
            Check(!NavigationContentPath.TryResolveAssetPath(root, "../outside.navgrid", out _, out _), "../ traversal must be rejected.");
            Check(!NavigationContentPath.TryResolveAssetPath(root, "..\\outside.navgrid", out _, out _), "..\\ traversal must be rejected.");
            Check(!NavigationContentPath.TryResolveAssetPath(root, Path.GetFullPath(goldenPath), out _, out _), "Absolute asset path must be rejected.");
            Check(!NavigationContentPath.TryResolveAssetPath(root, "C:drive-relative.navgrid", out _, out _), "Drive-relative asset path must be rejected.");
            Check(!NavigationContentPath.TryResolveAssetPath(root, "\\\\server\\share\\asset.navgrid", out _, out _), "UNC asset path must be rejected.");
            Check(!NavigationContentPath.TryResolveContentRoot("relative-root", out _, out _), "Relative Content Root override must be rejected.");

            const string goldenHash = "d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77";
            NavigationMapConfig valid = MakeConfig(0, "golden-grid-v1", relativeAssetPath, goldenHash);
            NavigationRegistryLoadResult build = NavigationRegistry.Build(root, new[] { valid });
            Check(build.Success, "Valid Registry build failed: " + build.Error);
            NavigationRegistry registry = build.Registry;
            Check(registry.TryGetByMapId("golden-grid-v1", out NavGridAsset mapAsset), "MapId lookup failed.");
            Check(registry.TryGetByRoomId(0, out NavGridAsset roomAsset), "RoomId lookup failed.");
            Check(object.ReferenceEquals(mapAsset, roomAsset), "Registry lookups must return the same immutable asset.");
            Check(object.ReferenceEquals(registry.GetRequiredForRoom(0), roomAsset), "Required Room lookup mismatch.");
            ExpectThrows<KeyNotFoundException>(() => registry.GetRequiredForRoom(999), "Missing required Room must throw.");

            NavigationRegistryLoadResult duplicateMap = NavigationRegistry.Build(root, new[]
            {
                valid,
                MakeConfig(1, "golden-grid-v1", relativeAssetPath, goldenHash)
            });
            Check(!duplicateMap.Success && duplicateMap.Error.Contains("Duplicate NavigationMapId"), "Duplicate MapId must be rejected.");

            NavigationRegistryLoadResult duplicateRoom = NavigationRegistry.Build(root, new[]
            {
                valid,
                MakeConfig(0, "another-map", relativeAssetPath, goldenHash)
            });
            Check(!duplicateRoom.Success && duplicateRoom.Error.Contains("Duplicate RoomId"), "Duplicate RoomId must be rejected.");

            NavigationRegistryLoadResult mapMismatch = NavigationRegistry.Build(root, new[]
            {
                MakeConfig(0, "wrong-map-id", relativeAssetPath, goldenHash)
            });
            Check(!mapMismatch.Success && mapMismatch.Error.Contains("NavigationMapId mismatch"), "File/config MapId mismatch must be rejected.");

            NavigationRegistryLoadResult hashMismatch = NavigationRegistry.Build(root, new[]
            {
                MakeConfig(0, "golden-grid-v1", relativeAssetPath, new string('0', 64))
            });
            Check(!hashMismatch.Success && hashMismatch.Error.Contains("ContentHash mismatch"), "Expected Hash mismatch must be rejected.");

            NavigationRegistryLoadResult versionMismatch = NavigationRegistry.Build(root, new[]
            {
                MakeConfig(0, "golden-grid-v1", relativeAssetPath, goldenHash, 2)
            });
            Check(!versionMismatch.Success && versionMismatch.Error.Contains("FormatVersion"), "Unsupported configured Version must be rejected.");

            NavigationRegistryLoadResult missingRequired = NavigationRegistry.Build(root, new[]
            {
                MakeConfig(0, "golden-grid-v1", "Navigation/missing.navgrid", goldenHash)
            });
            Check(!missingRequired.Success && missingRequired.Error.Contains("load failed"), "Missing required file must fail Registry creation.");

            NavigationRegistryLoadResult missingOptional = NavigationRegistry.Build(root, new[]
            {
                MakeConfig(5, "optional-map", "Navigation/missing.navgrid", goldenHash, 1, false)
            });
            Check(missingOptional.Success && missingOptional.Registry.Registrations.Count == 0, "Missing optional mapping must be explicitly skipped.");

            NavigationRegistryLoadResult invalidConfig = NavigationRegistry.Build(root, new[]
            {
                MakeConfig(-1, "golden-grid-v1", relativeAssetPath, "bad")
            });
            Check(!invalidConfig.Success, "Invalid configuration values must be rejected.");

            var catalog = new NavigationMapCatalogConfig();
            catalog.NavigationMaps.Add(valid);
            string configPath = Path.Combine(navigationDirectory, "navigation-maps.json");
            File.WriteAllText(configPath, JsonConvert.SerializeObject(catalog, Formatting.Indented), new UTF8Encoding(false));

            Environment.CurrentDirectory = otherWorkingDirectory;
            NavigationRegistryLoadResult workingDirectoryLoad = NavigationRegistry.LoadFromContent(root);
            Check(workingDirectoryLoad.Success, "Registry load must not depend on Working Directory: " + workingDirectoryLoad.Error);
            Check(workingDirectoryLoad.Registry.TryGetByRoomId(0, out _), "Working Directory regression Room lookup failed.");

            Check(registry.TryGetRegistrationByRoomId(0, out NavigationRegistration goldenRegistration), "Golden registration lookup failed.");
            var goldenRoom = new GameRoom(goldenRegistration);
            Check(goldenRoom.NavigationMapId == "golden-grid-v1", "Room NavigationMapId mismatch.");
            Check(goldenRoom.NavigationContentHash == goldenHash, "Room Navigation Hash mismatch.");
            Check(goldenRoom.Navigation.TryWorldToCell(-2.0f, 0.0f, -1.0f, out NavGridCoordinate firstCell) && firstCell.Equals(new NavGridCoordinate(0, 0)), "Room WorldToCell failed.");
            Check(goldenRoom.Navigation.IsWalkable(0, 0), "Walkable Cell query failed.");
            Check(!goldenRoom.Navigation.IsWalkable(2, 0), "Blocked Cell query failed.");
            Check(goldenRoom.Navigation.GetCellType(2, 0) == NavCellType.Blocked, "Blocked Cell type mismatch.");
            Check(!goldenRoom.Navigation.IsValidWorldPosition(2.0f, 0.0f, 1.5f), "Outside world position must be rejected.");
            Check(goldenRoom.Navigation.TryGetCellWorldCenter(3, 2, out Vector3 goldenCenter) && goldenCenter == new Vector3(1.5f, 0.0f, 1.5f), "Room Cell center lookup failed.");
            Check(!NavigationBoundsValidator.ValidateLegacyTilemap(goldenRoom.Navigation, new Vector2(4.0f, 3.0f)).Success, "Offset golden Bounds must not match zero-origin Tilemap Bounds.");
        }
        finally
        {
            Environment.CurrentDirectory = originalWorkingDirectory;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void RunDeployedContentAndRoomTests()
    {
        string defaultRoot = NavigationContentPath.GetDefaultContentRoot();
        Check(Path.IsPathRooted(defaultRoot), "Default Content Root must be absolute.");
        NavigationRegistryLoadResult deployedLoad = NavigationRegistry.LoadFromContent(defaultRoot);
        Check(deployedLoad.Success, "Deployed Content Registry load failed: " + deployedLoad.Error);
        Check(deployedLoad.Registry.TryGetRegistrationByRoomId(0, out NavigationRegistration room0Registration), "Deployed Room 0 mapping missing.");
        Check(room0Registration.NavigationMapId == "room-0-nav-v1", "Deployed Room 0 MapId mismatch.");
        Check(room0Registration.Asset.ContentHashHex.Length == 64, "Deployed Room 0 SHA-256 must be 64 hex characters.");
        Check(NavigationBoundsValidator.ValidateLegacyTilemap(room0Registration.Asset, new Vector2(145.0f, 145.0f)).Success, "Room 0 NavGrid/Tilemap Bounds must match.");

        var roomManager = new RoomManager();
        roomManager.Initialize(deployedLoad.Registry);
        roomManager.AddConfiguredRooms();
        GameRoom room0 = roomManager.Find(0);
        Check(room0 != null, "Room 0 was not created.");
        Check(room0.SceneId == 0 && room0.NavigationMapId == "room-0-nav-v1", "Room 0 Navigation metadata mismatch.");
        Check(room0.Navigation.IsValidWorldPosition(144.9999f, 0.0f, 144.9999f), "Room 0 max-bound interior must be valid.");
        Check(!room0.Navigation.IsValidWorldPosition(145.0f, 0.0f, 144.0f), "Room 0 exclusive max boundary must be invalid.");
        Check(object.ReferenceEquals(room0.Navigation, room0Registration.Asset), "Room 0 must reference the validated Registry asset.");
        Check(room0.Navigation.ContentHashHex == room0Registration.Asset.ContentHashHex, "Room 0 Navigation hash mismatch.");
        Check(room0.Navigation.GetCellType(6, 3) == NavCellType.Walkable, "Room 0 spawn area must remain Walkable.");
        Check(room0.Navigation.TryGetCellWorldCenter(144, 144, out Vector3 room0Center) && room0Center == new Vector3(144.5f, 0.0f, 144.5f), "Room 0 Cell center mismatch.");

        var missingRoomManager = new RoomManager();
        missingRoomManager.Initialize(deployedLoad.Registry);
        ExpectThrows<InvalidOperationException>(() => missingRoomManager.Add(999), "Room without Navigation mapping must not be created.");
    }
}