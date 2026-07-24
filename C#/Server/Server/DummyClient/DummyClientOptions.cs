using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DummyClient
{
    public sealed class DummyClientOptions
    {
        public string Scenario { get; private set; } = "fill-visible";
        public int Clients { get; private set; } = 3;
        public int ExpectedExternal { get; private set; } = 1;
        public int PartySize { get; private set; } = 4;
        public int ExpectedRooms { get; private set; } = 0;
        public string Host { get; private set; } = "127.0.0.1";
        public int Port { get; private set; } = 8080;
        public string Prefix { get; private set; } = "PD_Dummy";
        public int DelayMs { get; private set; } = 100;
        public int SceneReadyDelayMs { get; private set; } = 100;
        public int TimeoutSec { get; private set; } = 120;
        public int HoldAfterDungeonSec { get; private set; } = 120;
        public int HoldInTownSec { get; private set; } = 120;
        public int StartIndex { get; private set; } = 1;
        public bool Verbose { get; private set; } = false;

        public bool EnableMovement { get; private set; } = false;
        public string MovementTransport { get; private set; } = "tcp";
        public int MovementIntervalMs { get; private set; } = 200;
        public float MovementRadius { get; private set; } = 1.5f;
        public string MovementPattern { get; private set; } = "patrol";
        public string PatrolMode { get; private set; } = "horizontal";
        public float MovementSpeed { get; private set; } = 1.0f;
        public string TargetEnemy { get; private set; } = "nearest";
        public float FollowStopDistance { get; private set; } = 1.2f;
        public float FollowArrivalEpsilon { get; private set; } = 0.05f;
        public bool SendIdleOnArrival { get; private set; } = true;
        public string FollowInRangeMode { get; private set; } = "idle";
        public float StrafeDistance { get; private set; } = 0.45f;
        public int StrafeSwitchMs { get; private set; } = 1200;
        public bool AttackOnlyInRange { get; private set; } = false;
        public float AttackRange { get; private set; } = 1.5f;
        public int EnemyRefreshIntervalMs { get; private set; } = 500;
        public bool UseDungeonMovementBounds { get; private set; } = true;
        public float DungeonMinX { get; private set; } = -8.0f;
        public float DungeonMaxX { get; private set; } = 8.0f;
        public float DungeonMinY { get; private set; } = -4.5f;
        public float DungeonMaxY { get; private set; } = 4.5f;

        public bool EnableAttack { get; private set; } = false;
        public int AttackIntervalMs { get; private set; } = 1500;
        public IReadOnlyList<int> SkillIds => _skillIds;
        private readonly List<int> _skillIds = new List<int> { 2, 3, 4 };

        public bool EnableCollision { get; private set; } = false;
        public int CollisionDelayMs { get; private set; } = 200;
        public bool CollisionOnlyInRange { get; private set; } = true;
        public int MaxCollisionsPerTarget { get; private set; } = 0;
        public bool StopAfterReward { get; private set; } = false;

        public int GameplayDurationSec { get; private set; } = 60;
        public int GameplayStartDelayMs { get; private set; } = 1000;
        public string CombatTestScenario { get; private set; }
        public string MonitoringApiUrl { get; private set; } = "http://127.0.0.1:8090";

        public bool IsCreatePlayers => string.Equals(Scenario, "create-players", StringComparison.OrdinalIgnoreCase);
        public bool IsTownLoad => string.Equals(Scenario, "town-load", StringComparison.OrdinalIgnoreCase);
        public bool IsFillVisible => string.Equals(Scenario, "fill-visible", StringComparison.OrdinalIgnoreCase);
        public bool IsFillDummy => string.Equals(Scenario, "fill-dummy", StringComparison.OrdinalIgnoreCase);
        public bool IsMatchScenario => IsFillVisible || IsFillDummy;
        public bool HasGameplayEnabled => EnableMovement || EnableAttack || EnableCollision;
        public bool IsPatrolMovement => string.Equals(MovementPattern, "patrol", StringComparison.OrdinalIgnoreCase) ||
                                        MovementPattern.StartsWith("patrol-", StringComparison.OrdinalIgnoreCase);
        public bool IsFollowEnemyMovement => string.Equals(MovementPattern, "follow-enemy", StringComparison.OrdinalIgnoreCase);
        public bool UseNearestEnemy => string.Equals(TargetEnemy, "nearest", StringComparison.OrdinalIgnoreCase);
        public bool UseFirstEnemy => string.Equals(TargetEnemy, "first", StringComparison.OrdinalIgnoreCase);
        public bool CombatSummaryEnabled => string.IsNullOrWhiteSpace(CombatTestScenario) == false;
        public bool IsNormalCombatTest => string.Equals(CombatTestScenario, "normal", StringComparison.OrdinalIgnoreCase);
        public bool IsSpamCombatTest => string.Equals(CombatTestScenario, "spam", StringComparison.OrdinalIgnoreCase);

        public bool IsFollowInRangeIdle => string.Equals(FollowInRangeMode, "idle", StringComparison.OrdinalIgnoreCase);
        public bool IsFollowInRangeStrafe => string.Equals(FollowInRangeMode, "strafe", StringComparison.OrdinalIgnoreCase);
        public static bool TryParse(string[] args, out DummyClientOptions options)
        {
            options = new DummyClientOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    Console.WriteLine($"[DUMMY] Invalid argument: {key}");
                    return false;
                }

                if (i + 1 >= args.Length)
                {
                    Console.WriteLine($"[DUMMY] Missing value for {key}");
                    return false;
                }

                string value = args[++i];
                switch (key.Substring(2))
                {
                    case "scenario": options.Scenario = value; break;
                    case "clients": if (!TryParseInt(key, value, out int clients)) return false; options.Clients = clients; break;
                    case "expectedExternal": if (!TryParseInt(key, value, out int expectedExternal)) return false; options.ExpectedExternal = expectedExternal; break;
                    case "partySize": if (!TryParseInt(key, value, out int partySize)) return false; options.PartySize = partySize; break;
                    case "expectedRooms": if (!TryParseInt(key, value, out int expectedRooms)) return false; options.ExpectedRooms = expectedRooms; break;
                    case "host": options.Host = value; break;
                    case "port": if (!TryParseInt(key, value, out int port)) return false; options.Port = port; break;
                    case "prefix": options.Prefix = value; break;
                    case "delayMs": if (!TryParseInt(key, value, out int delayMs)) return false; options.DelayMs = delayMs; break;
                    case "sceneReadyDelayMs": if (!TryParseInt(key, value, out int sceneReadyDelayMs)) return false; options.SceneReadyDelayMs = sceneReadyDelayMs; break;
                    case "timeoutSec": if (!TryParseInt(key, value, out int timeoutSec)) return false; options.TimeoutSec = timeoutSec; break;
                    case "holdAfterDungeonSec": if (!TryParseInt(key, value, out int holdAfterDungeonSec)) return false; options.HoldAfterDungeonSec = holdAfterDungeonSec; break;
                    case "holdInTownSec": if (!TryParseInt(key, value, out int holdInTownSec)) return false; options.HoldInTownSec = holdInTownSec; break;
                    case "startIndex": if (!TryParseInt(key, value, out int startIndex)) return false; options.StartIndex = startIndex; break;
                    case "verbose": if (!TryParseBool(key, value, out bool verbose)) return false; options.Verbose = verbose; break;
                    case "enableMovement": if (!TryParseBool(key, value, out bool enableMovement)) return false; options.EnableMovement = enableMovement; break;
                    case "movementTransport": options.MovementTransport = value; break;
                    case "movementIntervalMs": if (!TryParseInt(key, value, out int movementIntervalMs)) return false; options.MovementIntervalMs = movementIntervalMs; break;
                    case "movementRadius": if (!TryParseFloat(key, value, out float movementRadius)) return false; options.MovementRadius = movementRadius; break;
                    case "patrolIntervalMs": if (!TryParseInt(key, value, out int patrolIntervalMs)) return false; options.MovementIntervalMs = patrolIntervalMs; break;
                    case "patrolRadius": if (!TryParseFloat(key, value, out float patrolRadius)) return false; options.MovementRadius = patrolRadius; break;
                    case "movementPattern": options.MovementPattern = value; break;
                    case "patrolMode": options.PatrolMode = value; break;
                    case "movementSpeed": if (!TryParseFloat(key, value, out float movementSpeed)) return false; options.MovementSpeed = movementSpeed; break;
                    case "patrolSpeed": if (!TryParseFloat(key, value, out float patrolSpeed)) return false; options.MovementSpeed = patrolSpeed; break;
                    case "targetEnemy": options.TargetEnemy = value; break;
                    case "followStopDistance": if (!TryParseFloat(key, value, out float followStopDistance)) return false; options.FollowStopDistance = followStopDistance; break;
                    case "followArrivalEpsilon": if (!TryParseFloat(key, value, out float followArrivalEpsilon)) return false; options.FollowArrivalEpsilon = followArrivalEpsilon; break;
                    case "sendIdleOnArrival": if (!TryParseBool(key, value, out bool sendIdleOnArrival)) return false; options.SendIdleOnArrival = sendIdleOnArrival; break;
                    case "followInRangeMode": options.FollowInRangeMode = value; break;
                    case "strafeDistance": if (!TryParseFloat(key, value, out float strafeDistance)) return false; options.StrafeDistance = strafeDistance; break;
                    case "strafeSwitchMs": if (!TryParseInt(key, value, out int strafeSwitchMs)) return false; options.StrafeSwitchMs = strafeSwitchMs; break;
                    case "attackOnlyInRange": if (!TryParseBool(key, value, out bool attackOnlyInRange)) return false; options.AttackOnlyInRange = attackOnlyInRange; break;
                    case "attackRange": if (!TryParseFloat(key, value, out float attackRange)) return false; options.AttackRange = attackRange; break;
                    case "enemyRefreshIntervalMs": if (!TryParseInt(key, value, out int enemyRefreshIntervalMs)) return false; options.EnemyRefreshIntervalMs = enemyRefreshIntervalMs; break;
                    case "useDungeonMovementBounds": if (!TryParseBool(key, value, out bool useDungeonMovementBounds)) return false; options.UseDungeonMovementBounds = useDungeonMovementBounds; break;
                    case "dungeonBounds": if (!TryParseDungeonBounds(value, options)) return false; break;
                    case "dungeonMinX": if (!TryParseFloat(key, value, out float dungeonMinX)) return false; options.DungeonMinX = dungeonMinX; break;
                    case "dungeonMaxX": if (!TryParseFloat(key, value, out float dungeonMaxX)) return false; options.DungeonMaxX = dungeonMaxX; break;
                    case "dungeonMinY": if (!TryParseFloat(key, value, out float dungeonMinY)) return false; options.DungeonMinY = dungeonMinY; break;
                    case "dungeonMaxY": if (!TryParseFloat(key, value, out float dungeonMaxY)) return false; options.DungeonMaxY = dungeonMaxY; break;
                    case "enableAttack": if (!TryParseBool(key, value, out bool enableAttack)) return false; options.EnableAttack = enableAttack; break;
                    case "attackIntervalMs": if (!TryParseInt(key, value, out int attackIntervalMs)) return false; options.AttackIntervalMs = attackIntervalMs; break;
                    case "skillIds": if (!TryParseSkillIds(value, options._skillIds)) return false; break;
                    case "enableCollision": if (!TryParseBool(key, value, out bool enableCollision)) return false; options.EnableCollision = enableCollision; break;
                    case "collisionDelayMs": if (!TryParseInt(key, value, out int collisionDelayMs)) return false; options.CollisionDelayMs = collisionDelayMs; break;
                    case "collisionOnlyInRange": if (!TryParseBool(key, value, out bool collisionOnlyInRange)) return false; options.CollisionOnlyInRange = collisionOnlyInRange; break;
                    case "maxCollisionsPerTarget": if (!TryParseInt(key, value, out int maxCollisionsPerTarget)) return false; options.MaxCollisionsPerTarget = maxCollisionsPerTarget; break;
                    case "stopAfterReward": if (!TryParseBool(key, value, out bool stopAfterReward)) return false; options.StopAfterReward = stopAfterReward; break;
                    case "gameplayDurationSec": if (!TryParseInt(key, value, out int gameplayDurationSec)) return false; options.GameplayDurationSec = gameplayDurationSec; break;
                    case "gameplayStartDelayMs": if (!TryParseInt(key, value, out int gameplayStartDelayMs)) return false; options.GameplayStartDelayMs = gameplayStartDelayMs; break;
                    case "combatTestScenario": options.CombatTestScenario = value; break;
                    case "monitoringApiUrl": options.MonitoringApiUrl = value.TrimEnd('/'); break;
                    default:
                        Console.WriteLine($"[DUMMY] Unknown option: {key}");
                        return false;
                }
            }

            return options.Validate();
        }

        public string BuildClientName(int zeroBasedIndex)
        {
            return $"{Prefix}_{StartIndex + zeroBasedIndex:0000}";
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  --scenario create-players|town-load|fill-visible|fill-dummy --clients 3 --host 127.0.0.1 --port 8080 --prefix PD_Dummy");
            Console.WriteLine("  town-load: --clients 200 --holdInTownSec 120 --enableMovement true --movementPattern patrol --patrolMode mixed");
            Console.WriteLine("  fill-visible: --expectedExternal 1 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120");
            Console.WriteLine("  fill-dummy: --clients 8 --partySize 4 --expectedRooms 2 --enableMovement true --patrolMode mixed --enableAttack false --enableCollision false");
            Console.WriteLine("  gameplay v2.x: --enableMovement true --movementTransport tcp --movementPattern patrol|follow-enemy --enableAttack true --skillIds 2,3,4");
            Console.WriteLine("  patrol demo: --enableMovement true --movementPattern patrol --patrolMode mixed --enableAttack false --enableCollision false");
            Console.WriteLine("  follow v2.2.4: --followArrivalEpsilon 0.05 --sendIdleOnArrival true --followInRangeMode idle|strafe --strafeDistance 0.45 --strafeSwitchMs 1200");
            Console.WriteLine("  collision v2.3: --enableCollision true --collisionDelayMs 200 --collisionOnlyInRange true");
            Console.WriteLine("  dungeon bounds: --useDungeonMovementBounds true --dungeonBounds -8,-4.5,8,4.5");
            Console.WriteLine("  combat summary: --combatTestScenario normal|spam --monitoringApiUrl http://127.0.0.1:8090");
        }

        private bool Validate()
        {
            if (!IsCreatePlayers && !IsTownLoad && !IsFillVisible && !IsFillDummy)
            {
                Console.WriteLine("[DUMMY] --scenario must be create-players, town-load, fill-visible, or fill-dummy.");
                return false;
            }

            if (Clients <= 0)
            {
                Console.WriteLine("[DUMMY] --clients must be greater than 0.");
                return false;
            }

            if (StartIndex <= 0)
            {
                Console.WriteLine("[DUMMY] --startIndex must be greater than 0.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Prefix))
            {
                Console.WriteLine("[DUMMY] --prefix is required.");
                return false;
            }

            if (!string.Equals(MovementTransport, "tcp", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[DUMMY] movementTransport={MovementTransport} is not supported in v2.3. Use tcp.");
                return false;
            }

            if (!IsPatrolMovement && !IsFollowEnemyMovement)
            {
                Console.WriteLine($"[DUMMY] movementPattern={MovementPattern} is not supported. Use patrol, patrol-horizontal, patrol-vertical, patrol-box, patrol-mixed, or follow-enemy.");
                return false;
            }

            if (IsPatrolMovement && IsSupportedPatrolMode(GetRequestedPatrolMode()) == false)
            {
                Console.WriteLine($"[DUMMY] patrolMode={GetRequestedPatrolMode()} is not supported. Use horizontal, vertical, box, diagonal, or mixed.");
                return false;
            }

            if (!UseNearestEnemy && !UseFirstEnemy)
            {
                Console.WriteLine($"[DUMMY] targetEnemy={TargetEnemy} is not supported. Use nearest or first.");
                return false;
            }

            if (MovementIntervalMs <= 0 || AttackIntervalMs <= 0 || GameplayDurationSec < 0 || GameplayStartDelayMs < 0 || EnemyRefreshIntervalMs <= 0 || CollisionDelayMs < 0 || StrafeSwitchMs <= 0)
            {
                Console.WriteLine("[DUMMY] gameplay intervals and durations must be non-negative, and intervals must be greater than 0.");
                return false;
            }

            if (MovementRadius < 0f || MovementSpeed < 0f || FollowStopDistance < 0f || FollowArrivalEpsilon < 0f || StrafeDistance < 0f || AttackRange < 0f)
            {
                Console.WriteLine("[DUMMY] movement distances and speed must be non-negative.");
                return false;
            }

            if (DungeonMinX >= DungeonMaxX || DungeonMinY >= DungeonMaxY)
            {
                Console.WriteLine("[DUMMY] dungeon bounds must satisfy minX < maxX and minY < maxY.");
                return false;
            }

            if (!IsFollowInRangeIdle && !IsFollowInRangeStrafe)
            {
                Console.WriteLine($"[DUMMY] followInRangeMode={FollowInRangeMode} is not supported. Use idle or strafe.");
                return false;
            }

            if (MaxCollisionsPerTarget < 0)
            {
                Console.WriteLine("[DUMMY] maxCollisionsPerTarget must be zero or greater.");
                return false;
            }

            if (_skillIds.Count == 0)
            {
                Console.WriteLine("[DUMMY] --skillIds must contain at least one skill id.");
                return false;
            }

            if (CombatSummaryEnabled)
            {
                if (!IsNormalCombatTest && !IsSpamCombatTest)
                {
                    Console.WriteLine("[DUMMY] --combatTestScenario must be normal or spam.");
                    return false;
                }

                if (!IsMatchScenario || !EnableAttack)
                {
                    Console.WriteLine("[DUMMY] --combatTestScenario requires a match scenario and --enableAttack true.");
                    return false;
                }

                if (!Uri.TryCreate(MonitoringApiUrl, UriKind.Absolute, out Uri monitoringUri) ||
                    (monitoringUri.Scheme != Uri.UriSchemeHttp && monitoringUri.Scheme != Uri.UriSchemeHttps))
                {
                    Console.WriteLine("[DUMMY] --monitoringApiUrl must be an absolute HTTP or HTTPS URL.");
                    return false;
                }
            }

            if (EnableCollision && EnableAttack == false)
                Console.WriteLine("[DUMMY] Warning: enableCollision=true requires C_Skill first. No collision will be sent unless enableAttack=true.");

            if (IsTownLoad)
            {
                if (HoldInTownSec <= 0)
                {
                    Console.WriteLine("[DUMMY] town-load requires --holdInTownSec > 0.");
                    return false;
                }

                if (EnableAttack)
                    Console.WriteLine("[DUMMY] Warning: town-load ignores enableAttack; Town load currently sends movement only.");

                if (EnableCollision)
                    Console.WriteLine("[DUMMY] Warning: town-load ignores enableCollision; Town load currently sends movement only.");

                if (HasGameplayEnabled == false)
                {
                    EnableMovement = true;
                    EnableAttack = false;
                    EnableCollision = false;
                    MovementPattern = "patrol";
                    PatrolMode = "mixed";
                    MovementIntervalMs = 100;
                    MovementSpeed = 0.35f;
                }
            }

            if (IsFillDummy)
            {
                if (PartySize <= 0)
                {
                    Console.WriteLine("[DUMMY] fill-dummy requires --partySize > 0.");
                    return false;
                }

                if (Clients < PartySize)
                {
                    Console.WriteLine("[DUMMY] fill-dummy requires --clients >= --partySize.");
                    return false;
                }

                if (Clients % PartySize != 0)
                {
                    Console.WriteLine("[DUMMY] fill-dummy requires clients to be divisible by partySize.");
                    return false;
                }

                if (ExpectedRooms <= 0)
                    ExpectedRooms = Clients / PartySize;

                if (ExpectedRooms != Clients / PartySize)
                {
                    Console.WriteLine($"[DUMMY] fill-dummy expectedRooms mismatch. ExpectedRooms={ExpectedRooms}, Clients={Clients}, PartySize={PartySize}.");
                    return false;
                }

                if (HasGameplayEnabled == false)
                {
                    EnableMovement = true;
                    EnableAttack = false;
                    EnableCollision = false;
                    MovementPattern = "patrol";
                    PatrolMode = "mixed";
                    MovementIntervalMs = 100;
                    MovementSpeed = 0.35f;
                }
            }


            if (IsFillVisible)
            {
                if (Clients >= 4)
                {
                    Console.WriteLine("[DUMMY] fill-visible requires --clients 1..3. Four dummies can match before Unity joins.");
                    return false;
                }

                if (ExpectedExternal <= 0)
                {
                    Console.WriteLine("[DUMMY] fill-visible requires --expectedExternal > 0.");
                    return false;
                }

                if (Clients + ExpectedExternal != 4)
                {
                    Console.WriteLine("[DUMMY] fill-visible requires clients + expectedExternal == 4.");
                    return false;
                }
            }

            return true;
        }

        public string GetRequestedPatrolMode()
        {
            if (MovementPattern.StartsWith("patrol-", StringComparison.OrdinalIgnoreCase))
                return MovementPattern.Substring("patrol-".Length);

            return PatrolMode;
        }

        public static bool IsSupportedPatrolMode(string mode)
        {
            return string.Equals(mode, "horizontal", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "vertical", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "box", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "diagonal", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "mixed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseInt(string key, string value, out int result)
        {
            if (int.TryParse(value, out result))
                return true;

            Console.WriteLine($"[DUMMY] Invalid integer for {key}: {value}");
            return false;
        }

        private static bool TryParseFloat(string key, string value, out float result)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return true;

            Console.WriteLine($"[DUMMY] Invalid float for {key}: {value}");
            return false;
        }

        private static bool TryParseDungeonBounds(string value, DummyClientOptions options)
        {
            string[] tokens = value.Split(',');
            if (tokens.Length != 4)
            {
                Console.WriteLine($"[DUMMY] Invalid dungeonBounds: {value}. Use minX,minY,maxX,maxY.");
                return false;
            }

            if (!TryParseFloat("--dungeonBounds minX", tokens[0], out float minX) ||
                !TryParseFloat("--dungeonBounds minY", tokens[1], out float minY) ||
                !TryParseFloat("--dungeonBounds maxX", tokens[2], out float maxX) ||
                !TryParseFloat("--dungeonBounds maxY", tokens[3], out float maxY))
                return false;

            options.DungeonMinX = minX;
            options.DungeonMinY = minY;
            options.DungeonMaxX = maxX;
            options.DungeonMaxY = maxY;
            return true;
        }

        private static bool TryParseBool(string key, string value, out bool result)
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

            Console.WriteLine($"[DUMMY] Invalid bool for {key}: {value}");
            return false;
        }

        private static bool TryParseSkillIds(string value, List<int> result)
        {
            result.Clear();
            foreach (string token in value.Split(','))
            {
                if (!int.TryParse(token.Trim(), out int skillId))
                {
                    Console.WriteLine($"[DUMMY] Invalid skill id: {token}");
                    return false;
                }

                result.Add(skillId);
            }

            return true;
        }
    }
}

















