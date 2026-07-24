using System;
using System.Collections.Generic;
using System.Linq;
using Server.Monitoring;

namespace Server.Game.Object
{
    public sealed class SkillCastState
    {
        public sealed class ActiveCast
        {
            public int SkillId { get; set; }
            public int CastSeq { get; set; }
            public DateTime CastTimeUtc { get; set; }
            public DateTime ActiveUntilUtc { get; set; }
            public DateTime LockUntilUtc { get; set; }
            public HashSet<int> HitTargets { get; } = new HashSet<int>();

            public bool IsActive(DateTime nowUtc)
            {
                return nowUtc <= ActiveUntilUtc;
            }
        }

        private readonly Dictionary<int, ActiveCast> _activeCastsBySkillId = new Dictionary<int, ActiveCast>();
        private readonly Dictionary<int, DateTime> _lastCastTimeUtcBySkillId = new Dictionary<int, DateTime>();

        public int SkillId { get; private set; }
        public int CastSeq { get; private set; }
        public DateTime CastTimeUtc { get; private set; }
        public DateTime ActiveUntilUtc { get; private set; }
        public DateTime LockUntilUtc { get; private set; }
        public bool HasActiveCast { get; private set; }

        public void BeginCast(int skillId, int activeWindowMs, DateTime nowUtc)
        {
            BeginCast(skillId, activeWindowMs, activeWindowMs, nowUtc);
        }

        public void BeginCast(int skillId, int activeWindowMs, int actionLockMs, DateTime nowUtc)
        {
            CombatActivityMetrics.RecordActiveCast();
            SkillId = skillId;
            CastSeq++;
            CastTimeUtc = nowUtc;
            ActiveUntilUtc = CastTimeUtc.AddMilliseconds(Math.Max(0, activeWindowMs));
            LockUntilUtc = CastTimeUtc.AddMilliseconds(Math.Max(0, actionLockMs));
            HasActiveCast = true;
            _lastCastTimeUtcBySkillId[skillId] = CastTimeUtc;

            ActiveCast cast = new ActiveCast
            {
                SkillId = skillId,
                CastSeq = CastSeq,
                CastTimeUtc = CastTimeUtc,
                ActiveUntilUtc = ActiveUntilUtc,
                LockUntilUtc = LockUntilUtc
            };

            _activeCastsBySkillId[skillId] = cast;
            CleanupExpired(CastTimeUtc);
        }

        public bool TryGetCooldownRemaining(int skillId, int cooldownMs, DateTime nowUtc, out int remainingMs)
        {
            remainingMs = 0;
            if (cooldownMs <= 0)
                return false;

            if (_lastCastTimeUtcBySkillId.TryGetValue(skillId, out DateTime lastCastTimeUtc) == false)
                return false;

            DateTime readyAtUtc = lastCastTimeUtc.AddMilliseconds(cooldownMs);
            if (nowUtc >= readyAtUtc)
                return false;

            remainingMs = Math.Max(1, (int)Math.Ceiling((readyAtUtc - nowUtc).TotalMilliseconds));
            return true;
        }

        public bool TryGetActiveLock(DateTime nowUtc, out ActiveCast activeCast)
        {
            CleanupExpired(nowUtc);
            activeCast = _activeCastsBySkillId.Values
                .Where(cast => nowUtc <= cast.LockUntilUtc)
                .OrderByDescending(cast => cast.CastSeq)
                .FirstOrDefault();

            return activeCast != null;
        }

        public bool IsActive(DateTime nowUtc)
        {
            CleanupExpired(nowUtc);
            return HasActiveCast && nowUtc <= ActiveUntilUtc;
        }

        public bool TryGetActiveCast(int skillId, DateTime nowUtc, out ActiveCast cast)
        {
            CleanupExpired(nowUtc);
            if (_activeCastsBySkillId.TryGetValue(skillId, out cast) == false)
                return false;

            if (cast.IsActive(nowUtc) == false)
            {
                _activeCastsBySkillId.Remove(skillId);
                RefreshLatestState(nowUtc);
                cast = null;
                return false;
            }

            return true;
        }

        private void CleanupExpired(DateTime nowUtc)
        {
            if (_activeCastsBySkillId.Count == 0)
            {
                RefreshLatestState(nowUtc);
                return;
            }

            List<int> expiredSkillIds = null;
            foreach (KeyValuePair<int, ActiveCast> pair in _activeCastsBySkillId)
            {
                if (pair.Value.IsActive(nowUtc))
                    continue;

                if (expiredSkillIds == null)
                    expiredSkillIds = new List<int>();

                expiredSkillIds.Add(pair.Key);
            }

            if (expiredSkillIds != null)
            {
                foreach (int skillId in expiredSkillIds)
                    _activeCastsBySkillId.Remove(skillId);
            }

            RefreshLatestState(nowUtc);
        }

        private void RefreshLatestState(DateTime nowUtc)
        {
            ActiveCast latest = _activeCastsBySkillId.Values
                .Where(cast => cast.IsActive(nowUtc))
                .OrderByDescending(cast => cast.CastSeq)
                .FirstOrDefault();

            HasActiveCast = latest != null;
            if (latest == null)
                return;

            SkillId = latest.SkillId;
            CastTimeUtc = latest.CastTimeUtc;
            ActiveUntilUtc = latest.ActiveUntilUtc;
            LockUntilUtc = latest.LockUntilUtc;
        }
    }
}
