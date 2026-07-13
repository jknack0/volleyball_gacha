using VG.Data;
using VG.Gameplay.Hype;
using VG.Gameplay.Rally;

namespace VG.Gameplay.Signatures
{
    /// <summary>
    /// Applies signature-move primitives (a)–(f) as modifiers on pending contacts — §1.3:
    /// "the move's mechanical primitives are applied as modifiers to the pending contact(s)".
    ///
    /// Consumption model [structural]: the sim calls the Consume* queries once per evaluated
    /// contact; counted modifiers ((b)/(d)/(f)) decrement per consumption, (a)/(c) are one-shot.
    /// Re-applying the same primitive for a side REPLACES the active instance
    /// [structural M0 simplification — no stacking; schema caps a move at 2 effects anyway].
    /// Deterministic, allocation-free after construction, no RNG.
    ///
    /// Percent conventions: (b) +X widens / −X shrinks the window (multiplier 1 + X/100);
    /// (f) multiplies final quality by 1 + X/100 — §3.2 says the CALLER clamps quality to 1;
    /// (d) multiplies the TARGET team's block/dig quality by 1 − X/100 (floored at 0).
    /// </summary>
    public sealed class SignatureEngine
    {
        private struct Counted
        {
            public float Percent;
            public int Remaining;
        }

        private readonly TimingGrade?[] _guaranteed = new TimingGrade?[2];
        private readonly Counted[] _window = new Counted[2];
        private readonly Counted[] _quality = new Counted[2];
        private readonly Counted[] _blockDigDebuff = new Counted[2]; // indexed by TARGET side
        private readonly string[] _trajectoryOverride = new string[2];

        private static TeamSide Opponent(TeamSide side)
            => side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;

        /// <summary>
        /// Apply one effect for a caster. Hype cost validation happens BEFORE this call
        /// (HypeMeters.TrySpend, §1.3); (e) resolves immediately against <paramref name="hype"/>.
        /// </summary>
        public void Apply(in SigEffect effect, TeamSide caster, HypeMeters hype)
        {
            switch (effect.Primitive)
            {
                case SigPrimitive.GuaranteedTimingGrade:
                    _guaranteed[(int)caster] = effect.Grade;
                    break;

                case SigPrimitive.TimingWindowAdjust:
                    _window[(int)caster] = new Counted { Percent = effect.Percent, Remaining = effect.Contacts };
                    break;

                case SigPrimitive.TrajectoryOverride:
                    _trajectoryOverride[(int)caster] = effect.TrajectoryOverrideId;
                    break;

                case SigPrimitive.OpponentContactDebuff:
                    _blockDigDebuff[(int)Opponent(caster)] = new Counted { Percent = effect.Percent, Remaining = effect.Contacts };
                    break;

                case SigPrimitive.HypeDelta:
                    hype.Add(effect.HypeTargetsOpponent ? Opponent(caster) : caster, effect.HypeAmount);
                    break;

                case SigPrimitive.TeamQualityBuff:
                    _quality[(int)caster] = new Counted { Percent = effect.Percent, Remaining = effect.Contacts };
                    break;
            }
        }

        /// <summary>(a): one-shot grade override for the side's next evaluated contact.</summary>
        public bool TryConsumeGuaranteedGrade(TeamSide side, out TimingGrade grade)
        {
            int i = (int)side;
            if (_guaranteed[i].HasValue)
            {
                grade = _guaranteed[i].Value;
                _guaranteed[i] = null;
                return true;
            }
            grade = default;
            return false;
        }

        /// <summary>(b): window multiplier for one of the side's contacts; 1.0 when inactive.</summary>
        public float ConsumeWindowMultiplier(TeamSide side)
            => ConsumeCounted(_window, (int)side, widen: true);

        /// <summary>(f): final-quality multiplier for one of the side's contacts; caller clamps quality to 1 (§3.2).</summary>
        public float ConsumeQualityMultiplier(TeamSide side)
            => ConsumeCounted(_quality, (int)side, widen: true);

        /// <summary>(d): quality multiplier for one of the TARGET side's block/dig contacts; 1.0 when inactive, floored at 0.</summary>
        public float ConsumeBlockDigDebuffMultiplier(TeamSide contactingSide)
            => ConsumeCounted(_blockDigDebuff, (int)contactingSide, widen: false);

        /// <summary>(c): one-shot authored-arc override for the side's next authored trajectory.</summary>
        public bool TryConsumeTrajectoryOverride(TeamSide side, out string overrideId)
        {
            int i = (int)side;
            overrideId = _trajectoryOverride[i];
            _trajectoryOverride[i] = null;
            return overrideId != null;
        }

        private static float ConsumeCounted(Counted[] slots, int i, bool widen)
        {
            if (slots[i].Remaining <= 0) return 1f;
            slots[i].Remaining--;
            float m = widen ? 1f + slots[i].Percent / 100f : 1f - slots[i].Percent / 100f;
            return m < 0f ? 0f : m;
        }
    }
}
