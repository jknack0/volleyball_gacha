using System;
using VG.Data;

namespace VG.Gameplay.Resolution
{
    /// <summary>§3.6/§4.1 constants. Defaults = spec values.</summary>
    public sealed class PointResolutionTunables
    {
        /// <summary>A = q_s × (AttackPowerBase + AttackPowerRiskScale × risk(z)) — §3.6 [tunable].</summary>
        public float AttackPowerBase = 0.75f;
        public float AttackPowerRiskScale = 0.25f;

        /// <summary>match(z): adjacent-column block factor — §3.6 [tunable].</summary>
        public float AdjacentColumnMatch = 0.35f;

        /// <summary>Read-commit (waited for the set): q_b × 0.85 — §3.6 [tunable].</summary>
        public float ReadCommitFactor = 0.85f;

        /// <summary>Early commit, correct column: ×1.15 — §3.6 [tunable].</summary>
        public float EarlyCorrectFactor = 1.15f;

        /// <summary>Step 2: q_s &lt; 0.08 → net — §3.6 [tunable].</summary>
        public float NetFloorQuality = 0.08f;

        /// <summary>Step 3: edge-zone out threshold 0.30, discounted 0.10 on Perfect timing — §3.6 [tunable].</summary>
        public float EdgeOutThreshold = 0.30f;
        public float PerfectRiskDiscount = 0.10f;

        /// <summary>Step 4: B − A ≥ 0.15 → stuff — §3.6 [tunable].</summary>
        public float StuffMargin = 0.15f;

        /// <summary>Step 5: A − B ≥ 0.25 (edge zone, touched) → tool — §3.6 [tunable].</summary>
        public float ToolMargin = 0.25f;

        /// <summary>Step 6: A_eff = A × (1 − DeflectionDamping × B) — §3.6 [tunable].</summary>
        public float DeflectionDamping = 0.5f;

        /// <summary>Step 7: dig succeeds iff q_d ≥ DigBase + DigPerAEff × A_eff — §3.6 [tunable].</summary>
        public float DigBase = 0.25f;
        public float DigPerAEff = 0.55f;

        /// <summary>§4.1 risk(z): corner zones 1.0, mid-edge 0.6, center 0.2 [tunable].</summary>
        public float RiskCorner = 1.0f;
        public float RiskMidEdge = 0.6f;
        public float RiskCenter = 0.2f;

        /// <summary>§4.1 risk table for an attacker-view zone.</summary>
        public float RiskOf(ZoneId zone)
        {
            switch (zone)
            {
                case ZoneId.z_LF:
                case ZoneId.z_RF:
                case ZoneId.z_LB:
                case ZoneId.z_RB:
                    return RiskCorner;
                case ZoneId.z_CM:
                    return RiskCenter;
                default:
                    return RiskMidEdge;
            }
        }
    }

    /// <summary>How the defense committed its block — §3.6/§4.4.</summary>
    public enum BlockCommit
    {
        /// <summary>No swipe: no block, B = 0 (§4.4).</summary>
        None,
        /// <summary>Swiped after set release: q_b × ReadCommitFactor (§3.6).</summary>
        Read,
        /// <summary>Held ≥ 0.4 s before the set: correct column ×EarlyCorrectFactor; wrong column match = 0 (§3.6).</summary>
        Early,
    }

    /// <summary>Block inputs to resolution. Columns are attacker-view 0/1/2 = L/C/R.</summary>
    public readonly struct BlockState
    {
        public readonly BlockCommit Commit;
        public readonly int Column;
        public readonly float Quality; // q_b via §3.2

        public BlockState(BlockCommit commit, int column, float quality)
        {
            Commit = commit;
            Column = column;
            Quality = quality;
        }

        public static BlockState None => new BlockState(BlockCommit.None, 0, 0f);
    }

    /// <summary>Attack resolution result — §3.6's outcome set plus the non-terminal flows.</summary>
    public enum AttackOutcome
    {
        /// <summary>Step 1: spike timing Miss — free ball over, rally continues.</summary>
        FreeBall,
        /// <summary>Step 2: into the net — point to defense (RallyOutcome.Net).</summary>
        Net,
        /// <summary>Step 3: sailed out — point to defense (RallyOutcome.Out).</summary>
        Out,
        /// <summary>Step 4: stuffed — point to defense (RallyOutcome.Blocked).</summary>
        Blocked,
        /// <summary>Step 5: off the block, out of reach — point to attack (RallyOutcome.Tooled).</summary>
        Tooled,
        /// <summary>Step 7: dug — rally continues (RallyOutcome.Dug at display level).</summary>
        Dug,
        /// <summary>Step 8: floor — point to attack (RallyOutcome.Kill).</summary>
        Kill,
    }

    /// <summary>Full §3.6 result: outcome + the intermediates downstream systems consume.</summary>
    public readonly struct AttackResolution
    {
        public readonly AttackOutcome Outcome;
        /// <summary>Attack power A after any block damping (feeds deflection T and dig UI).</summary>
        public readonly float EffectiveAttack;
        /// <summary>True when a block touch occurred (steps 5–6 path): deflection arc is authored ⚄ by the caller.</summary>
        public readonly bool BlockTouched;
        /// <summary>Dug only: display grade from the margin q_d − required, via the §3.3 table.</summary>
        public readonly ReceiveGrade DigDisplayGrade;

        public AttackResolution(AttackOutcome outcome, float effectiveAttack, bool blockTouched, ReceiveGrade digDisplayGrade)
        {
            Outcome = outcome;
            EffectiveAttack = effectiveAttack;
            BlockTouched = blockTouched;
            DigDisplayGrade = digDisplayGrade;
        }
    }

    /// <summary>
    /// §3.6 point resolution [structural pipeline]: evaluate in order, first terminal wins, NO RNG.
    /// Serve resolution reuses the pipeline with B = 0 and the receive as the "dig" (§3.6 tail).
    /// </summary>
    public static class PointResolution
    {
        /// <summary>Attacker-view column (0/1/2 = L/C/R) of a zone — §3.6 match(z) input.</summary>
        public static int ZoneColumn(ZoneId zone) => (int)zone % 3;

        /// <summary>§4.1: the 8 non-center zones are edge zones.</summary>
        public static bool IsEdgeZone(ZoneId zone) => zone != ZoneId.z_CM;

        /// <summary>Block strength B = q_b × match(z) with §3.6 commit modifiers.</summary>
        public static float BlockStrength(PointResolutionTunables t, in BlockState block, ZoneId aimZone)
        {
            if (block.Commit == BlockCommit.None) return 0f;

            int diff = Math.Abs(block.Column - ZoneColumn(aimZone));
            float match = diff == 0 ? 1f : diff == 1 ? t.AdjacentColumnMatch : 0f;

            switch (block.Commit)
            {
                case BlockCommit.Read:
                    return block.Quality * t.ReadCommitFactor * match;
                case BlockCommit.Early:
                    return diff == 0 ? block.Quality * t.EarlyCorrectFactor : 0f; // early wrong ⇒ match = 0
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// The §3.6 pipeline. <paramref name="digQuality"/> is negative when no dig was attempted.
        /// Deterministic: same inputs ⇒ same outcome; the deflection arc's ⚄ jitter is authored
        /// by the caller AFTER a non-terminal touch (Outcome ∈ {Dug, Kill} with BlockTouched).
        /// </summary>
        public static AttackResolution ResolveAttack(
            PointResolutionTunables t,
            ResolutionTunables resolution,
            float spikeQuality,
            TimingGrade spikeTiming,
            ZoneId aimZone,
            in BlockState block,
            float digQuality)
        {
            // Step 1 — timing Miss: free ball over, rally continues.
            if (spikeTiming == TimingGrade.Miss)
                return new AttackResolution(AttackOutcome.FreeBall, 0f, false, ReceiveGrade.Shank);

            // Step 2 — dumped into the net.
            if (spikeQuality < t.NetFloorQuality)
                return new AttackResolution(AttackOutcome.Net, 0f, false, ReceiveGrade.Shank);

            // Step 3 — edge-zone out (Perfect timing discounts the threshold).
            bool edge = IsEdgeZone(aimZone);
            if (edge)
            {
                float threshold = t.EdgeOutThreshold - (spikeTiming == TimingGrade.Perfect ? t.PerfectRiskDiscount : 0f);
                if (spikeQuality < threshold)
                    return new AttackResolution(AttackOutcome.Out, 0f, false, ReceiveGrade.Shank);
            }

            float risk = t.RiskOf(aimZone);
            float a = spikeQuality * (t.AttackPowerBase + t.AttackPowerRiskScale * risk);
            float b = BlockStrength(t, block, aimZone);

            // Step 4 — stuff.
            if (b - a >= t.StuffMargin)
                return new AttackResolution(AttackOutcome.Blocked, a, true, ReceiveGrade.Shank);

            // Step 5 — tool off the block.
            if (a - b >= t.ToolMargin && b > 0f && edge)
                return new AttackResolution(AttackOutcome.Tooled, a, true, ReceiveGrade.Shank);

            // Step 6 — non-terminal touch damps the attack.
            bool touched = b > 0f;
            float aEff = touched ? a * (1f - t.DeflectionDamping * b) : a;

            // Step 7 — dig.
            if (digQuality >= 0f)
            {
                float required = t.DigBase + t.DigPerAEff * aEff;
                if (digQuality >= required)
                {
                    var grade = QualityMath.ReceiveGradeOf(resolution, digQuality - required);
                    return new AttackResolution(AttackOutcome.Dug, aEff, touched, grade);
                }
            }

            // Step 8 — floor.
            return new AttackResolution(AttackOutcome.Kill, aEff, touched, ReceiveGrade.Shank);
        }

        /// <summary>
        /// §3.6 tail: "Serve resolution reuses the same pipeline with B = 0 and receive as the
        /// 'dig'": ace = step 8 (Kill), service error = steps 2–3. Step 1 never applies — a
        /// serve-timing Miss is grade-capped upstream (§1.2 T3), not a whiffed ball.
        /// </summary>
        public static AttackResolution ResolveServe(
            PointResolutionTunables t,
            ResolutionTunables resolution,
            float serveQuality,
            TimingGrade serveTiming,
            ZoneId aimZone,
            float receiveQuality)
        {
            TimingGrade timing = serveTiming == TimingGrade.Miss ? TimingGrade.Good : serveTiming;
            return ResolveAttack(t, resolution, serveQuality, timing, aimZone, BlockState.None, receiveQuality);
        }
    }
}
