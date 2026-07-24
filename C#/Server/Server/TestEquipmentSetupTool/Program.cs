using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Data;
using Server.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TestEquipmentSetupTool
{
    internal class Program
    {
        private const int MinSlot = 0;
        private const int MaxSlot = 19;
        private const string DefaultPrefix = "PD_Dummy";

        private static int Main(string[] args)
        {
            Console.WriteLine("[GEAR] TestEquipmentSetupTool");
            Console.WriteLine("[GEAR] WARNING: This tool modifies test/demo equipment data. Run it before server/client login.");
            Console.WriteLine("[GEAR] Use --dryRun true to preview changes without saving.");

            ToolOptions options;
            if (!ToolOptions.TryParse(args, out options))
            {
                PrintUsage();
                return 1;
            }

            if (!ValidateOptions(options))
            {
                PrintUsage();
                return 1;
            }

            string dataPath;
            if (!TryResolveDataPath(options.DataPath, out dataPath))
            {
                Console.WriteLine("[GEAR] FAIL: Could not resolve item data path. Pass --dataPath <path-to-Resources/Data>.");
                return 1;
            }

            Dictionary<int, ItemData> itemDict;
            if (!TryLoadItemData(dataPath, out itemDict))
            {
                return 1;
            }

            ItemData weaponData;
            ItemData armorData;
            if (!ValidateTemplates(itemDict, options.WeaponTemplateId, options.ArmorTemplateId, out weaponData, out armorData))
            {
                return 1;
            }

            ArmorType targetArmorType = ((ArmorData)armorData).armorType;
            List<string> targetNames = BuildTargetNames(options);

            Console.WriteLine($"[GEAR] DataPath={dataPath}");
            Console.WriteLine($"[GEAR] ConnectionStringSource=AppDbContext default localdb GameDB");
            Console.WriteLine($"[GEAR] Targets={string.Join(",", targetNames)}");
            Console.WriteLine($"[GEAR] WeaponTemplateId={options.WeaponTemplateId}, ArmorTemplateId={options.ArmorTemplateId}");
            Console.WriteLine($"[GEAR] Equip={options.Equip}, OverwriteExistingEquip={options.OverwriteExistingEquip}, DryRun={options.DryRun}");

            if (options.CreateMissingPlayer)
            {
                Console.WriteLine("[GEAR] FAIL: --createMissingPlayer true is reserved for a later version. Create players through the normal login/create flow first.");
                return 1;
            }

            bool success = RunSetup(options, itemDict, targetNames, targetArmorType);
            Console.WriteLine(success ? "Result: SUCCESS" : "Result: FAIL");
            return success ? 0 : 1;
        }

        private static bool RunSetup(ToolOptions options, Dictionary<int, ItemData> itemDict, List<string> targetNames, ArmorType targetArmorType)
        {
            using (AppDbContext db = new AppDbContext())
            {
                List<PlayerDb> players = db.Players
                    .Where(p => targetNames.Contains(p.PlayerName))
                    .ToList();

                Dictionary<string, PlayerDb> playersByName = players.ToDictionary(p => p.PlayerName, p => p);
                List<string> missingPlayers = targetNames.Where(name => !playersByName.ContainsKey(name)).ToList();
                foreach (string missing in missingPlayers)
                    Console.WriteLine($"[GEAR] Missing player: {missing}");

                if (missingPlayers.Count > 0)
                    return false;

                List<int> playerIds = players.Select(p => p.PlayerDbId).ToList();
                List<ItemDb> allItems = db.Items
                    .Where(i => i.OwnerDbId.HasValue && playerIds.Contains(i.OwnerDbId.Value))
                    .ToList();

                bool allOk = true;
                foreach (PlayerDb player in players.OrderBy(p => targetNames.IndexOf(p.PlayerName)))
                {
                    List<ItemDb> playerItems = allItems.Where(i => i.OwnerDbId == player.PlayerDbId).ToList();
                    PlayerSetupResult result = PreparePlayer(db, itemDict, player, playerItems, targetArmorType, options);
                    allOk &= result.Success;
                    Console.WriteLine(result.Message);
                }

                if (!allOk)
                    return false;

                if (options.DryRun)
                {
                    Console.WriteLine("[GEAR] DryRun=true. SaveChanges skipped.");
                    return true;
                }

                int changes = db.SaveChanges();
                Console.WriteLine($"[GEAR] SaveChanges completed. Changes={changes}");
                return true;
            }
        }

        private static PlayerSetupResult PreparePlayer(
            AppDbContext db,
            Dictionary<int, ItemData> itemDict,
            PlayerDb player,
            List<ItemDb> playerItems,
            ArmorType targetArmorType,
            ToolOptions options)
        {
            List<string> actions = new List<string>();
            bool success = true;

            ItemDb weapon = FindTemplateItem(playerItems, options.WeaponTemplateId);
            if (weapon == null)
            {
                int? slot = FindEmptySlot(playerItems);
                if (!slot.HasValue)
                    return PlayerSetupResult.Fail($"[GEAR] Player={player.PlayerName} FAIL: no empty inventory slot for weapon");

                weapon = new ItemDb
                {
                    TemplateId = options.WeaponTemplateId,
                    Count = 1,
                    Slot = slot.Value,
                    OwnerDbId = player.PlayerDbId,
                    Equipped = false
                };
                playerItems.Add(weapon);
                db.Items.Add(weapon);
                actions.Add($"WeaponTemplate={options.WeaponTemplateId} created");
            }
            else
            {
                actions.Add($"WeaponTemplate={options.WeaponTemplateId} reused");
            }

            ItemDb armor = FindTemplateItem(playerItems, options.ArmorTemplateId);
            if (armor == null)
            {
                int? slot = FindEmptySlot(playerItems);
                if (!slot.HasValue)
                    return PlayerSetupResult.Fail($"[GEAR] Player={player.PlayerName} FAIL: no empty inventory slot for armor");

                armor = new ItemDb
                {
                    TemplateId = options.ArmorTemplateId,
                    Count = 1,
                    Slot = slot.Value,
                    OwnerDbId = player.PlayerDbId,
                    Equipped = false
                };
                playerItems.Add(armor);
                db.Items.Add(armor);
                actions.Add($"ArmorTemplate={options.ArmorTemplateId} created");
            }
            else
            {
                actions.Add($"ArmorTemplate={options.ArmorTemplateId} reused");
            }

            if (options.Equip)
            {
                EquipTargetItems(itemDict, playerItems, weapon, armor, targetArmorType, options, actions, ref success);
            }
            else
            {
                actions.Add("equip skipped");
            }

            return success
                ? PlayerSetupResult.Ok($"[GEAR] Player={player.PlayerName} {string.Join(" ", actions)}")
                : PlayerSetupResult.Fail($"[GEAR] Player={player.PlayerName} FAIL: {string.Join(" ", actions)}");
        }

        private static void EquipTargetItems(
            Dictionary<int, ItemData> itemDict,
            List<ItemDb> playerItems,
            ItemDb weapon,
            ItemDb armor,
            ArmorType targetArmorType,
            ToolOptions options,
            List<string> actions,
            ref bool success)
        {
            ItemDb equippedWeapon = playerItems.FirstOrDefault(i => i.Equipped && IsItemType(itemDict, i.TemplateId, ItemType.Weapon) && !ReferenceEquals(i, weapon));
            if (equippedWeapon != null)
            {
                if (options.OverwriteExistingEquip)
                {
                    equippedWeapon.Equipped = false;
                    actions.Add($"unequippedWeaponTemplate={equippedWeapon.TemplateId}");
                }
                else if (!weapon.Equipped)
                {
                    actions.Add($"weapon equip skipped existingEquippedWeaponTemplate={equippedWeapon.TemplateId}");
                    success = false;
                }
            }

            ItemDb equippedSameArmorType = playerItems.FirstOrDefault(i =>
                i.Equipped &&
                IsArmorType(itemDict, i.TemplateId, targetArmorType) &&
                !ReferenceEquals(i, armor));

            if (equippedSameArmorType != null)
            {
                if (options.OverwriteExistingEquip)
                {
                    equippedSameArmorType.Equipped = false;
                    actions.Add($"unequippedArmorTemplate={equippedSameArmorType.TemplateId}");
                }
                else if (!armor.Equipped)
                {
                    actions.Add($"armor equip skipped existingEquippedArmorTemplate={equippedSameArmorType.TemplateId}");
                    success = false;
                }
            }

            if (success || options.OverwriteExistingEquip)
            {
                weapon.Equipped = true;
                armor.Equipped = true;
                actions.Add("weapon equipped");
                actions.Add("armor equipped");
            }
        }

        private static ItemDb FindTemplateItem(List<ItemDb> items, int templateId)
        {
            return items
                .OrderByDescending(i => i.Equipped)
                .ThenBy(i => i.Slot)
                .FirstOrDefault(i => i.TemplateId == templateId);
        }

        private static int? FindEmptySlot(List<ItemDb> items)
        {
            HashSet<int> usedSlots = new HashSet<int>(items.Select(i => i.Slot));
            for (int slot = MinSlot; slot <= MaxSlot; slot++)
            {
                if (!usedSlots.Contains(slot))
                    return slot;
            }

            return null;
        }

        private static bool IsItemType(Dictionary<int, ItemData> itemDict, int templateId, ItemType itemType)
        {
            ItemData itemData;
            return itemDict.TryGetValue(templateId, out itemData) && itemData.itemType == itemType;
        }

        private static bool IsArmorType(Dictionary<int, ItemData> itemDict, int templateId, ArmorType armorType)
        {
            ItemData itemData;
            if (!itemDict.TryGetValue(templateId, out itemData))
                return false;

            ArmorData armorData = itemData as ArmorData;
            return armorData != null && armorData.armorType == armorType;
        }

        private static bool ValidateTemplates(
            Dictionary<int, ItemData> itemDict,
            int weaponTemplateId,
            int armorTemplateId,
            out ItemData weaponData,
            out ItemData armorData)
        {
            weaponData = null;
            armorData = null;

            if (!itemDict.TryGetValue(weaponTemplateId, out weaponData))
            {
                Console.WriteLine($"[GEAR] Invalid weaponTemplateId: {weaponTemplateId} not found");
                return false;
            }

            if (weaponData.itemType != ItemType.Weapon)
            {
                Console.WriteLine($"[GEAR] Invalid weaponTemplateId: {weaponTemplateId} is not Weapon");
                return false;
            }

            if (!itemDict.TryGetValue(armorTemplateId, out armorData))
            {
                Console.WriteLine($"[GEAR] Invalid armorTemplateId: {armorTemplateId} not found");
                return false;
            }

            if (armorData.itemType != ItemType.Armor)
            {
                Console.WriteLine($"[GEAR] Invalid armorTemplateId: {armorTemplateId} is not Armor");
                return false;
            }

            return true;
        }

        private static bool TryLoadItemData(string dataPath, out Dictionary<int, ItemData> itemDict)
        {
            itemDict = null;
            string itemDataPath = Path.Combine(dataPath, "ItemData.json");
            if (!File.Exists(itemDataPath))
            {
                Console.WriteLine($"[GEAR] FAIL: ItemData.json not found. Path={itemDataPath}");
                return false;
            }

            try
            {
                string text = File.ReadAllText(itemDataPath);
                ItemLoader loader = JsonConvert.DeserializeObject<ItemLoader>(text);
                itemDict = loader.MakeDict();
                Console.WriteLine($"[GEAR] ItemData loaded. Count={itemDict.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GEAR] FAIL: ItemData load failed. {ex.Message}");
                return false;
            }
        }

        private static bool TryResolveDataPath(string explicitDataPath, out string dataPath)
        {
            dataPath = null;

            if (!string.IsNullOrWhiteSpace(explicitDataPath))
                return TryUseDataPath(explicitDataPath, out dataPath);

            List<string> candidates = new List<string>();
            string cwd = Directory.GetCurrentDirectory();
            string baseDir = AppContext.BaseDirectory;

            AddConfigDataPathCandidate(Path.Combine(cwd, "config.json"), candidates);
            AddConfigDataPathCandidate(Path.Combine(baseDir, "config.json"), candidates);
            AddConfigDataPathCandidate(Path.Combine(cwd, "Server", "Server", "bin", "Debug", "netcoreapp3.1", "config.json"), candidates);

            DirectoryInfo cwdInfo = new DirectoryInfo(cwd);
            DirectoryInfo parent = cwdInfo.Parent;
            if (parent != null)
            {
                candidates.Add(Path.Combine(parent.FullName, "Unity Project", "Project_Dawn", "Assets", "Resources", "Data"));
                candidates.Add(Path.Combine(parent.FullName, "C#", "Project_Dawn", "Assets", "Resources", "Data"));
            }

            candidates.Add(Path.Combine(cwd, "..", "Unity Project", "Project_Dawn", "Assets", "Resources", "Data"));
            candidates.Add(Path.Combine(cwd, "..", "C#", "Project_Dawn", "Assets", "Resources", "Data"));

            foreach (string candidate in candidates.Distinct())
            {
                if (TryUseDataPath(candidate, out dataPath))
                    return true;
            }

            return false;
        }

        private static bool TryUseDataPath(string candidate, out string dataPath)
        {
            dataPath = null;
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            string fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "ItemData.json")))
            {
                dataPath = fullPath;
                return true;
            }

            return false;
        }

        private static void AddConfigDataPathCandidate(string configPath, List<string> candidates)
        {
            if (!File.Exists(configPath))
                return;

            try
            {
                JObject config = JObject.Parse(File.ReadAllText(configPath));
                string configuredDataPath = (string)config["dataPath"];
                if (string.IsNullOrWhiteSpace(configuredDataPath))
                    return;

                string resolved = Path.IsPathRooted(configuredDataPath)
                    ? configuredDataPath
                    : Path.Combine(Path.GetDirectoryName(configPath), configuredDataPath);
                candidates.Add(resolved);
            }
            catch
            {
                // Ignore invalid config candidates and continue probing common paths.
            }
        }

        private static List<string> BuildTargetNames(ToolOptions options)
        {
            if (options.PlayerNames.Count > 0)
                return options.PlayerNames;

            string prefix = string.IsNullOrWhiteSpace(options.Prefix) ? DefaultPrefix : options.Prefix;
            List<string> names = new List<string>();
            for (int i = 0; i < options.Count; i++)
                names.Add($"{prefix}_{options.StartIndex + i:0000}");
            return names;
        }

        private static bool ValidateOptions(ToolOptions options)
        {
            if (options.WeaponTemplateId <= 0)
            {
                Console.WriteLine("[GEAR] FAIL: --weaponTemplateId is required.");
                return false;
            }

            if (options.ArmorTemplateId <= 0)
            {
                Console.WriteLine("[GEAR] FAIL: --armorTemplateId is required.");
                return false;
            }

            bool hasPlayerNames = options.PlayerNames.Count > 0;
            bool hasPrefixMode = options.HasPrefix || options.Count > 0;

            if (hasPlayerNames && hasPrefixMode)
            {
                Console.WriteLine("[GEAR] FAIL: Use either --playerNames or --prefix/--count, not both.");
                return false;
            }

            if (!hasPlayerNames && !hasPrefixMode)
            {
                Console.WriteLine("[GEAR] FAIL: Target players are required. Use --playerNames or --prefix/--count.");
                return false;
            }

            if (hasPrefixMode && options.Count <= 0)
            {
                Console.WriteLine("[GEAR] FAIL: --count must be greater than 0 for prefix mode.");
                return false;
            }

            if (options.StartIndex <= 0)
            {
                Console.WriteLine("[GEAR] FAIL: --startIndex must be greater than 0.");
                return false;
            }

            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project Server/TestEquipmentSetupTool/TestEquipmentSetupTool.csproj -- --prefix PD_Dummy --count 3 --weaponTemplateId 1001 --armorTemplateId 2001 --equip true --overwriteExistingEquip true --dryRun true");
            Console.WriteLine("  dotnet run --project Server/TestEquipmentSetupTool/TestEquipmentSetupTool.csproj -- --playerNames Player_0054 --weaponTemplateId 1001 --armorTemplateId 2001 --equip true --overwriteExistingEquip true");
            Console.WriteLine("Options:");
            Console.WriteLine("  --prefix <prefix>                 Prefix mode target prefix. Names are prefix_0001, prefix_0002, ...");
            Console.WriteLine("  --count <count>                   Prefix mode target count.");
            Console.WriteLine("  --startIndex <index>              Prefix mode starting index. Default=1.");
            Console.WriteLine("  --playerNames <name1,name2>       Explicit target player names.");
            Console.WriteLine("  --weaponTemplateId <id>           Required weapon template id.");
            Console.WriteLine("  --armorTemplateId <id>            Required armor template id.");
            Console.WriteLine("  --equip <true|false>              Set target items Equipped=true. Default=true.");
            Console.WriteLine("  --overwriteExistingEquip <true|false>  Unequip conflicting equipped gear first. Default=true.");
            Console.WriteLine("  --createMissingPlayer <true|false>     Reserved. Default=false.");
            Console.WriteLine("  --dryRun <true|false>             Preview only. Default=false.");
            Console.WriteLine("  --dataPath <path>                 Optional path to Unity Assets/Resources/Data.");
        }
    }

    internal sealed class ToolOptions
    {
        public bool HasPrefix { get; private set; }
        public string Prefix { get; private set; }
        public int Count { get; private set; }
        public int StartIndex { get; private set; }
        public List<string> PlayerNames { get; private set; }
        public int WeaponTemplateId { get; private set; }
        public int ArmorTemplateId { get; private set; }
        public bool Equip { get; private set; }
        public bool OverwriteExistingEquip { get; private set; }
        public bool CreateMissingPlayer { get; private set; }
        public bool DryRun { get; private set; }
        public string DataPath { get; private set; }

        private ToolOptions()
        {
            StartIndex = 1;
            PlayerNames = new List<string>();
            Equip = true;
            OverwriteExistingEquip = true;
            CreateMissingPlayer = false;
            DryRun = false;
        }

        public static bool TryParse(string[] args, out ToolOptions options)
        {
            options = new ToolOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    Console.WriteLine($"[GEAR] FAIL: Unexpected argument '{key}'.");
                    return false;
                }

                if (i + 1 >= args.Length)
                {
                    Console.WriteLine($"[GEAR] FAIL: Missing value for {key}.");
                    return false;
                }

                string value = args[++i];
                switch (key.Substring(2))
                {
                    case "prefix":
                        options.Prefix = value;
                        options.HasPrefix = true;
                        break;
                    case "count":
                        int count;
                        if (!int.TryParse(value, out count))
                            return FailParse(key, value);
                        options.Count = count;
                        break;
                    case "startIndex":
                        int startIndex;
                        if (!int.TryParse(value, out startIndex))
                            return FailParse(key, value);
                        options.StartIndex = startIndex;
                        break;
                    case "playerNames":
                        options.PlayerNames = value
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(name => name.Trim())
                            .Where(name => name.Length > 0)
                            .Distinct()
                            .ToList();
                        break;
                    case "weaponTemplateId":
                        int weaponTemplateId;
                        if (!int.TryParse(value, out weaponTemplateId))
                            return FailParse(key, value);
                        options.WeaponTemplateId = weaponTemplateId;
                        break;
                    case "armorTemplateId":
                        int armorTemplateId;
                        if (!int.TryParse(value, out armorTemplateId))
                            return FailParse(key, value);
                        options.ArmorTemplateId = armorTemplateId;
                        break;
                    case "equip":
                        if (!TryParseBool(value, out bool equip))
                            return FailParse(key, value);
                        options.Equip = equip;
                        break;
                    case "overwriteExistingEquip":
                        if (!TryParseBool(value, out bool overwrite))
                            return FailParse(key, value);
                        options.OverwriteExistingEquip = overwrite;
                        break;
                    case "createMissingPlayer":
                        if (!TryParseBool(value, out bool createMissingPlayer))
                            return FailParse(key, value);
                        options.CreateMissingPlayer = createMissingPlayer;
                        break;
                    case "dryRun":
                        if (!TryParseBool(value, out bool dryRun))
                            return FailParse(key, value);
                        options.DryRun = dryRun;
                        break;
                    case "dataPath":
                        options.DataPath = value;
                        break;
                    default:
                        Console.WriteLine($"[GEAR] FAIL: Unknown option {key}.");
                        return false;
                }
            }

            return true;
        }

        private static bool TryParseBool(string value, out bool result)
        {
            if (bool.TryParse(value, out result))
                return true;

            if (value == "1")
            {
                result = true;
                return true;
            }

            if (value == "0")
            {
                result = false;
                return true;
            }

            return false;
        }

        private static bool FailParse(string key, string value)
        {
            Console.WriteLine($"[GEAR] FAIL: Invalid value for {key}: {value}");
            return false;
        }
    }

    internal sealed class PlayerSetupResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        private PlayerSetupResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static PlayerSetupResult Ok(string message)
        {
            return new PlayerSetupResult(true, message);
        }

        public static PlayerSetupResult Fail(string message)
        {
            return new PlayerSetupResult(false, message);
        }
    }
}



