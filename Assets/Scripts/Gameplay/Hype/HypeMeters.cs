using System;
using VG.Gameplay.Rally;

namespace VG.Gameplay.Hype
{
    /// <summary>§3.7 accrual events. Long-rally accrual is team-symmetric: apply once per team.</summary>
    public enum HypeEvent
    {
        PerfectContact,
        Kill,
        StuffBlock,
        Ace,
        LongRallyContact,
        BigSpikeDug,
        OwnError,
    }

    /// <summary>
    /// Both teams' Hype meters — contract: per-team 0–100, threshold 100 → Ignition.
    ///
    /// Ignition LATCHES [structural M0 decision, documented]: once a team crosses the threshold
    /// it stays Ignited for the rest of the match even as signatures spend Hype back below 100.
    /// Rationale: §1.3 gates signature activation on "team Hype ≥ move cost", NOT on Ignition —
    /// Ignition is the earned-crescendo state (music/VFX/presentation §5 C12), and revoking it on
    /// spend would punish using the moves it celebrates. Deterministic, no RNG.
    /// </summary>
    public sealed class HypeMeters
    {
        private readonly HypeTunables _t;
        private readonly int[] _hype = new int[2];
        private readonly bool[] _ignited = new bool[2];

        public HypeMeters(HypeTunables tunables)
        {
            _t = tunables ?? throw new ArgumentNullException(nameof(tunables));
        }

        public int Hype(TeamSide side) => _hype[(int)side];

        public bool IsIgnited(TeamSide side) => _ignited[(int)side];

        /// <summary>§3.7 table lookup for a team-scoped event.</summary>
        public int DeltaFor(HypeEvent e)
        {
            switch (e)
            {
                case HypeEvent.PerfectContact: return _t.PerfectContact;
                case HypeEvent.Kill: return _t.Kill;
                case HypeEvent.StuffBlock: return _t.StuffBlock;
                case HypeEvent.Ace: return _t.Ace;
                case HypeEvent.LongRallyContact: return _t.LongRallyPerContact;
                case HypeEvent.BigSpikeDug: return _t.BigSpikeDug;
                case HypeEvent.OwnError: return _t.OwnError;
                default: throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }

        /// <summary>Apply a §3.7 event to a team. Returns true when Ignition NEWLY latched (§5 C12 hook).</summary>
        public bool Apply(HypeEvent e, TeamSide team) => Add(team, DeltaFor(e));

        /// <summary>Clamped raw adjustment (also the (e) HypeDelta primitive entry point). Returns true when Ignition newly latched.</summary>
        public bool Add(TeamSide team, int amount)
        {
            int i = (int)team;
            int next = _hype[i] + amount;
            if (next < 0) next = 0;
            else if (next > _t.HypeMax) next = _t.HypeMax;
            _hype[i] = next;

            if (!_ignited[i] && next >= _t.IgnitionThreshold)
            {
                _ignited[i] = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// §1.3 signature gate: activation requires Hype ≥ cost. Deducts on success; NEVER
        /// unlatches Ignition. Returns false (unchanged) when the team can't afford it.
        /// </summary>
        public bool TrySpend(TeamSide team, int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            int i = (int)team;
            if (_hype[i] < cost) return false;
            _hype[i] -= cost;
            return true;
        }
    }
}
