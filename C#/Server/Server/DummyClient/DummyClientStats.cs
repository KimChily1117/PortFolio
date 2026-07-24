using System;
using System.Collections.Generic;
using System.Linq;

namespace DummyClient
{
    public sealed class DummyClientStats
    {
        public void Print(DummyClientOptions options, IReadOnlyList<DummyClient> clients)
        {
            Console.WriteLine();
            Console.WriteLine($"Scenario: {options.Scenario}");
            PrintCount("Connected", clients, c => c.HasConnected);
            PrintCount("LoggedIn", clients, c => c.HasLoggedIn);
            PrintCount("PlayerReady", clients, c => c.HasPlayerReady);
            PrintCount("EnteredTown", clients, c => c.HasEnteredTown);
            PrintCount("MatchRequested", clients, c => c.HasMatchRequested);

            if (options.IsTownLoad)
            {
                PrintTownLoadMetrics(clients);
                PrintGameplaySummary(options, clients);
                Console.WriteLine($"HoldingTown: {options.HoldInTownSec} sec");
            }

            if (options.IsMatchScenario)
            {
                PrintCount("WaitingForExternal", clients, c => c.HasWaitingForExternal);
                PrintCount("PartyMatched", clients, c => c.HasPartyMatched);
                if (options.IsFillVisible)
                    Console.WriteLine($"ExternalMembersDetected: {clients.SelectMany(c => c.ExternalMembers).Distinct().Count()}");
                if (options.IsFillDummy)
                {
                    Console.WriteLine($"PartySize: {options.PartySize}");
                    Console.WriteLine($"ExpectedRooms: {options.ExpectedRooms}");
                }
                PrintCount("SceneMoveReceived", clients, c => c.HasSceneMoveReceived);
                PrintCount("SceneReadySent", clients, c => c.HasSceneReadySent);
                PrintCount("EnteredDungeon", clients, c => c.HasEnteredDungeon);
                PrintGameplaySummary(options, clients);
                Console.WriteLine($"HoldingConnection: {options.HoldAfterDungeonSec} sec");
                PrintPartySummary(clients);
            }

            Console.WriteLine($"Result: {(IsSuccess(options, clients) ? "SUCCESS" : "FAIL")}");
        }

        private static bool IsSuccess(DummyClientOptions options, IReadOnlyList<DummyClient> clients)
        {
            if (clients.Count != options.Clients)
                return false;

            if (clients.Any(c => c.HasConnected == false || c.HasLoggedIn == false || c.HasPlayerReady == false || c.HasEnteredTown == false))
                return false;

            if (options.IsCreatePlayers)
                return clients.All(c => c.HasMatchRequested == false && c.State != DummyClientState.Failed);

            if (options.IsTownLoad)
            {
                bool townGameplayOk = options.EnableMovement == false || clients.All(c => c.HasGameplayStarted && c.HasGameplayCompleted && c.MoveSentCount > 0);
                return townGameplayOk && clients.All(c => c.HasMatchRequested == false && c.State == DummyClientState.Completed);
            }

            bool gameplayOk = true;
            if (options.HasGameplayEnabled)
                gameplayOk = clients.All(c => c.HasGameplayStarted && c.HasGameplayCompleted);

            bool followOk = true;
            if (options.EnableMovement && options.IsFollowEnemyMovement)
                followOk = clients.All(c => c.HasEnemyTracked);

            bool collisionOk = true;
            if (options.EnableCollision)
                collisionOk = clients.Any(c => c.CollisionSentCount > 0);

            bool commonMatchOk = clients.All(c => c.HasMatchRequested &&
                                                 c.HasWaitingForExternal &&
                                                 c.HasPartyMatched &&
                                                 c.HasSceneMoveReceived &&
                                                 c.HasSceneReadySent &&
                                                 c.HasEnteredDungeon &&
                                                 c.State != DummyClientState.Failed) &&
                                 gameplayOk &&
                                 followOk &&
                                 collisionOk;

            if (commonMatchOk == false)
                return false;

            var rooms = clients.Where(c => c.TargetRoomId > 0).GroupBy(c => c.TargetRoomId).ToList();

            if (options.IsFillVisible)
            {
                int externalCount = clients.SelectMany(c => c.ExternalMembers).Distinct().Count();
                bool sameRoom = rooms.Count == 1;
                return externalCount == options.ExpectedExternal && sameRoom;
            }

            if (options.IsFillDummy)
            {
                bool expectedRoomCount = rooms.Count == options.ExpectedRooms;
                bool roomSizeOk = rooms.All(r => r.Count() == options.PartySize);
                bool noCrossRoomMove = clients.Sum(c => c.CrossRoomMoveSuspectedCount) == 0;
                return expectedRoomCount && roomSizeOk && noCrossRoomMove;
            }

            return false;
        }

        private static void PrintCount(string label, IReadOnlyList<DummyClient> clients, Func<DummyClient, bool> predicate)
        {
            Console.WriteLine($"{label}: {clients.Count(predicate)}/{clients.Count}");
        }

        private static void PrintTownLoadMetrics(IReadOnlyList<DummyClient> clients)
        {
            Console.WriteLine("TownLoadMetrics:");
            PrintDurationStats("  ConnectToTownMs", clients.Select(c => ElapsedMs(c.ConnectStartedAtUtc, c.EnteredTownAtUtc)));
            PrintDurationStats("  LoginToTownMs", clients.Select(c => ElapsedMs(c.LoggedInAtUtc, c.EnteredTownAtUtc)));
            PrintDurationStats("  TownHoldActualMs", clients.Select(c => ElapsedMs(c.TownLoadStartedAtUtc, c.TownLoadCompletedAtUtc)));
            Console.WriteLine($"  AllConnectedInMs: {WindowMs(clients.Select(c => c.ConnectedAtUtc)):0}");
            Console.WriteLine($"  AllEnteredTownInMs: {WindowMs(clients.Select(c => c.EnteredTownAtUtc)):0}");
            Console.WriteLine($"  TownLoadStartSpreadMs: {WindowMs(clients.Select(c => c.TownLoadStartedAtUtc)):0}");
            double movementWindowSec = Math.Max(0.001, WindowStartEndSeconds(clients.Select(c => c.TownLoadStartedAtUtc), clients.Select(c => c.TownLoadCompletedAtUtc)));
            int moveSent = clients.Sum(c => c.MoveSentCount);
            int receivedMove = clients.Sum(c => c.ReceivedMoveCount);
            Console.WriteLine($"  MoveSentPerSec: {moveSent / movementWindowSec:0.0}");
            Console.WriteLine($"  ReceivedMovePerSec: {receivedMove / movementWindowSec:0.0}");
            Console.WriteLine($"  FailedClients: {clients.Count(c => c.State == DummyClientState.Failed)}");
        }

        private static void PrintDurationStats(string label, IEnumerable<double> values)
        {
            List<double> valid = values.Where(v => v >= 0).ToList();
            if (valid.Count == 0)
            {
                Console.WriteLine($"{label}: n/a");
                return;
            }

            Console.WriteLine($"{label}: min={valid.Min():0}, avg={valid.Average():0}, max={valid.Max():0}");
        }

        private static double ElapsedMs(DateTime start, DateTime end)
        {
            if (start == default || end == default || end < start)
                return -1;

            return (end - start).TotalMilliseconds;
        }

        private static double WindowMs(IEnumerable<DateTime> timestamps)
        {
            List<DateTime> valid = timestamps.Where(t => t != default).ToList();
            if (valid.Count <= 1)
                return 0;

            return (valid.Max() - valid.Min()).TotalMilliseconds;
        }

        private static double WindowStartEndSeconds(IEnumerable<DateTime> starts, IEnumerable<DateTime> ends)
        {
            List<DateTime> validStarts = starts.Where(t => t != default).ToList();
            List<DateTime> validEnds = ends.Where(t => t != default).ToList();
            if (validStarts.Count == 0 || validEnds.Count == 0)
                return 0;

            return Math.Max(0, (validEnds.Max() - validStarts.Min()).TotalSeconds);
        }

        private static void PrintGameplaySummary(DummyClientOptions options, IReadOnlyList<DummyClient> clients)
        {
            Console.WriteLine($"MovementEnabled: {options.EnableMovement}");
            Console.WriteLine($"AttackEnabled: {options.EnableAttack}");
            Console.WriteLine($"CollisionEnabled: {options.EnableCollision}");

            if (options.HasGameplayEnabled == false)
                return;

            Console.WriteLine($"MovementPattern: {options.MovementPattern}");
            if (options.EnableMovement && options.IsPatrolMovement)
                Console.WriteLine($"PatrolMode: {options.GetRequestedPatrolMode()}");
            if (options.EnableMovement && options.IsFollowEnemyMovement)
            {
                Console.WriteLine($"TargetEnemyMode: {options.TargetEnemy}");
                PrintCount("EnemyTracked", clients, c => c.HasEnemyTracked);
                PrintCount("FollowReached", clients, c => c.HasFollowReached);
                Console.WriteLine($"EnemyCount: {clients.Sum(c => c.EnemyCount)}");
                Console.WriteLine($"FollowShouldMove: {clients.Sum(c => c.FollowShouldMoveCount)}");
                Console.WriteLine($"FollowIdle: {clients.Sum(c => c.FollowIdleCount)}");
                Console.WriteLine($"FollowArrived: {clients.Sum(c => c.FollowArrivedCount)}");
                Console.WriteLine($"IdleOnArrivalSent: {clients.Sum(c => c.IdleOnArrivalSentCount)}");
                Console.WriteLine($"StrafeMoveSent: {clients.Sum(c => c.StrafeMoveSentCount)}");
            }

            PrintCount("GameplayStarted", clients, c => c.HasGameplayStarted);
            PrintCount("GameplayCompleted", clients, c => c.HasGameplayCompleted);
            Console.WriteLine($"MoveSent: {clients.Sum(c => c.MoveSentCount)}");
            Console.WriteLine($"ReceivedMove: {clients.Sum(c => c.ReceivedMoveCount)}");
            Console.WriteLine($"CrossRoomMoveSuspected: {clients.Sum(c => c.CrossRoomMoveSuspectedCount)}");
            Console.WriteLine($"SkillSent: {clients.Sum(c => c.SkillSentCount)}");
            Console.WriteLine($"CollisionSent: {clients.Sum(c => c.CollisionSentCount)}");
            Console.WriteLine($"CollisionSkippedNoTarget: {clients.Sum(c => c.CollisionSkippedNoTargetCount)}");
            Console.WriteLine($"CollisionSkippedOutOfRange: {clients.Sum(c => c.CollisionSkippedOutOfRangeCount)}");
            Console.WriteLine($"CollisionSkippedDeadTarget: {clients.Sum(c => c.CollisionSkippedDeadTargetCount)}");
            Console.WriteLine($"CollisionSkippedLimit: {clients.Sum(c => c.CollisionSkippedLimitCount)}");
            Console.WriteLine($"AddItemReceived: {clients.Sum(c => c.AddItemReceivedCount)}");
            PrintCount("RewardObserved", clients, c => c.RewardObserved);

            foreach (DummyClient client in clients.OrderBy(c => c.Name))
            {
                Console.WriteLine($"{client.Name}: RoomId={client.TargetRoomId}, TransferId={client.TransferId}, EnteredDungeon={client.HasEnteredDungeon}, GameplayStarted={client.HasGameplayStarted}, MoveSent={client.MoveSentCount}, ReceivedMove={client.ReceivedMoveCount}, CrossRoomMoveSuspected={client.CrossRoomMoveSuspectedCount}, PatrolMode={client.PatrolModeName}, SkillSent={client.SkillSentCount}, CollisionSent={client.CollisionSentCount}, EnemyCount={client.EnemyCount}, FollowShouldMove={client.FollowShouldMoveCount}, FollowIdle={client.FollowIdleCount}, AddItemReceived={client.AddItemReceivedCount}, LastRewardTemplateId={client.LastRewardTemplateId}, LastRewardCount={client.LastRewardCount}, LastSkillId={client.LastSkillId}, LastCollisionTargetId={client.LastCollisionTargetId}, LastPosition=({client.LastPosX:0.00},{client.LastPosY:0.00}), EnemyTracked={client.HasEnemyTracked}, TargetEnemyId={client.TargetEnemyObjectId}, LastTarget=({client.LastTargetX:0.00},{client.LastTargetY:0.00}), LastDistance={client.LastTargetDistance:0.00}, FollowReached={client.HasFollowReached}, LastMoveState={client.LastMoveState}, LastInRange={client.LastInRange}, FollowArrived={client.FollowArrivedCount}, IdleOnArrivalSent={client.IdleOnArrivalSentCount}, StrafeMoveSent={client.StrafeMoveSentCount}");
            }
        }

        private static void PrintPartySummary(IReadOnlyList<DummyClient> clients)
        {
            var rooms = clients.Where(c => c.TargetRoomId > 0).GroupBy(c => c.TargetRoomId).ToList();
            int partyIndex = 1;
            foreach (var room in rooms)
            {
                List<string> dummyMembers = room.SelectMany(c => c.DummyMembers).Distinct().OrderBy(x => x).ToList();
                List<string> externalMembers = room.SelectMany(c => c.ExternalMembers).Distinct().OrderBy(x => x).ToList();
                Console.WriteLine();
                Console.WriteLine($"Party {partyIndex}:");
                Console.WriteLine($"RoomId={room.Key}");
                Console.WriteLine($"External/Visible Members={string.Join(",", externalMembers)}");
                Console.WriteLine($"Dummy Members={string.Join(",", dummyMembers)}");
                Console.WriteLine($"Result={(dummyMembers.Count > 0 ? "OBSERVED" : "UNKNOWN")}");
                partyIndex++;
            }
        }
    }
}








