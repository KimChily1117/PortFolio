#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectDawn.Combat
{
    public readonly struct FeedbackKey : IEquatable<FeedbackKey>
    {
        public readonly InputKey Input;
        public readonly int Stage, Target, Round;
        public FeedbackKey(InputKey input, int stage, int target, int round)
        { Input = input; Stage = stage; Target = target; Round = round; }
        public bool Equals(FeedbackKey x) => Input.Equals(x.Input) && Stage == x.Stage && Target == x.Target && Round == x.Round;
        public override bool Equals(object? x) => x is FeedbackKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Input, Stage, Target, Round);
    }

    public enum FeedbackDecision { None, Start, ConfirmExisting, CancelExisting, ResyncRequired }

    // Presentation-only bookkeeping. Never changes HP, logical time or another player's feedback.
    // The caller owns audio/particles/hitstop tokens keyed by FeedbackKey and consumes each decision once.
    public sealed class PredictionLedger
    {
        private sealed class Entry
        {
            public long Expires;
            public bool Active, Resolved;
        }
        private readonly Dictionary<FeedbackKey, Entry> _entries = new Dictionary<FeedbackKey, Entry>();
        private readonly HashSet<long> _events = new HashSet<long>();
        private readonly int _capacity;
        private long _encounter, _epoch;
        private readonly int _player;
        public PredictionLedger(long encounter, int player, long epoch, int capacity = 65536)
        {
            if (encounter < 1 || player < 1 || epoch < 1 || capacity < 1) throw new ArgumentOutOfRangeException(nameof(encounter));
            _encounter = encounter; _player = player; _epoch = epoch; _capacity = capacity;
        }
        private bool Owns(FeedbackKey key) => key.Input.Encounter == _encounter && key.Input.Player == _player &&
            key.Input.Epoch == _epoch && key.Input.Sequence > 0 && key.Stage >= 0 && key.Round >= 0;

        public FeedbackDecision Predict(FeedbackKey key, long expiresAtUs)
        {
            if (!Owns(key)) return FeedbackDecision.None;
            if (_entries.ContainsKey(key)) return FeedbackDecision.None;
            if (_entries.Count >= _capacity) return FeedbackDecision.ResyncRequired;
            _entries.Add(key, new Entry { Active = true, Expires = expiresAtUs });
            return FeedbackDecision.Start;
        }

        public FeedbackDecision Resolve(long serverEventSequence, FeedbackKey key, bool hit)
        {
            if (!Owns(key) || serverEventSequence <= 0 || _events.Contains(serverEventSequence)) return FeedbackDecision.None;
            if (_events.Count >= _capacity || (!_entries.ContainsKey(key) && _entries.Count >= _capacity))
                return FeedbackDecision.ResyncRequired;
            _events.Add(serverEventSequence);
            if (!_entries.TryGetValue(key, out var entry))
            {
                _entries.Add(key, new Entry { Resolved = true, Active = hit, Expires = 0 });
                return hit ? FeedbackDecision.Start : FeedbackDecision.None;
            }
            if (entry.Resolved) return FeedbackDecision.None;
            entry.Resolved = true;
            if (hit) return FeedbackDecision.ConfirmExisting;
            bool wasActive = entry.Active; entry.Active = false;
            return wasActive ? FeedbackDecision.CancelExisting : FeedbackDecision.None;
        }

        public FeedbackKey[] ExpireUnconfirmed(long nowUs)
        {
            var expired = _entries.Where(x => !x.Value.Resolved && x.Value.Active && nowUs >= x.Value.Expires).Select(x => x.Key).ToArray();
            foreach (var key in expired) _entries[key].Active = false;
            // Keep a tombstone: an expired sound must not play a second time when its result arrives late.
            return expired;
        }

        public FeedbackKey[] RejectAction(InputKey input)
        {
            var cancelled = _entries.Where(x => x.Key.Input.Equals(input) && !x.Value.Resolved && x.Value.Active).Select(x => x.Key).ToArray();
            foreach (var pair in _entries.Where(x => x.Key.Input.Equals(input) && !x.Value.Resolved))
            { pair.Value.Active = false; pair.Value.Resolved = true; }
            return cancelled;
        }

        public FeedbackKey[] Reset(long encounter, long epoch)
        {
            if (encounter < 1 || epoch < 1 || (encounter == _encounter && epoch <= _epoch))
                throw new ArgumentException("Reset needs a new encounter or newer connection epoch.");
            var active = _entries.Where(x => x.Value.Active).Select(x => x.Key).ToArray();
            _entries.Clear(); _events.Clear(); _encounter = encounter; _epoch = epoch;
            return active;
        }
    }
}
