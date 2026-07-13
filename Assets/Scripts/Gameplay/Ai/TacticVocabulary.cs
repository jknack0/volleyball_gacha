using VG.Data;
using VG.Gameplay.Resolution;

namespace VG.Gameplay.Ai
{
    /// <summary>Serve kinds the AI can attempt, mapped to the §6.3 serve column rows.</summary>
    public enum ServeTactic
    {
        CenterFloat,   // Easy+: center-zone float
        LineFloat,     // Normal+: line float serves
        CornerFloat,   // Normal+: corner targets
        JumpServe,     // Hard: jump serve (targets weakest receiver — min Receive stat — caller aims)
    }

    /// <summary>Per-tier §6.3 block behavior modes. The block-commit decision itself runs through §6.1 U.</summary>
    public enum BlockBehavior
    {
        ReadOnlyCenter,           // Easy: read only, center column
        ReadWithOccasionalCommit, // Normal: read + occasional commit (via U)
        FullMixAdjacentSoftCommit // Hard: full read/commit mix, adjacent-column soft commits
    }

    /// <summary>
    /// §6.3 tactic vocabulary per tier [structural] — the difficulty grammar, hard-coded like
    /// the §3.4 Cascade matrix rather than tunable. Each tier's vocabulary is a superset of
    /// the previous ("+" rows in the spec table). Pure predicates; no state, no rng.
    ///
    /// §6.4: vocabulary only ever restricts the AI's own choices — it carries no player-side
    /// concept whatsoever.
    /// </summary>
    public static class TacticVocabulary
    {
        /// <summary>§6.3 serve column: Easy = center float only; Normal adds line/corner floats; Hard adds jump serve.</summary>
        public static bool AllowsServe(DifficultyTier tier, ServeTactic serve)
        {
            switch (serve)
            {
                case ServeTactic.CenterFloat:
                    return true;
                case ServeTactic.LineFloat:
                case ServeTactic.CornerFloat:
                    return tier != DifficultyTier.Easy;
                case ServeTactic.JumpServe:
                    return tier == DifficultyTier.Hard;
                default:
                    return false;
            }
        }

        /// <summary>
        /// §6.3 set-option column — vocabulary ONLY (ignores receive quality):
        /// Easy = high-outside; Normal adds quick; Hard adds back-row-pipe and dump.
        /// </summary>
        public static bool AllowsSetOption(DifficultyTier tier, SetOption option)
        {
            switch (option)
            {
                case SetOption.HighOutside:
                    return true;
                case SetOption.QuickMiddle:
                    return tier != DifficultyTier.Easy;
                case SetOption.BackRowPipe:
                case SetOption.Dump:
                    return tier == DifficultyTier.Hard;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The AI may choose a set option only when BOTH gates pass: the §6.3 tier vocabulary
        /// AND the §3.4 Cascade lit matrix for this receive grade. A Hard AI on a C-grade
        /// receive is still stuck with high-outside; a Normal AI never quicks off a B receive.
        /// </summary>
        public static bool MayChooseSetOption(DifficultyTier tier, ReceiveGrade receive, SetOption option)
        {
            return AllowsSetOption(tier, option) && Cascade.IsSetOptionAvailable(receive, option);
        }

        /// <summary>§6.3 block column: tier → behavior mode.</summary>
        public static BlockBehavior BlockBehaviorFor(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: return BlockBehavior.ReadOnlyCenter;
                case DifficultyTier.Hard: return BlockBehavior.FullMixAdjacentSoftCommit;
                default: return BlockBehavior.ReadWithOccasionalCommit;
            }
        }
    }
}
