namespace VG.Data
{
    // Canonical enums — verbatim from docs/data-schemas.md vocabulary. [structural]
    // Renaming any member is a contract change: update data-schemas.md first.

    public enum StatId { Power, Jump, Technique, Serve, Receive, Speed }

    /// <summary>Timing grades, ordered worst → best so comparisons read naturally.</summary>
    public enum TimingGrade { Miss, Good, Great, Perfect }

    /// <summary>Receive display grades, ordered worst → best (docs/m0-gameplay-spec.md §3.2).</summary>
    public enum ReceiveGrade { Shank, C, B, A, S }

    public enum ContactType { Serve, Receive, Set, Spike, Block, Dig }

    /// <summary>Set options lit/gated by receive grade (docs/m0-gameplay-spec.md §3.3).</summary>
    public enum SetOption { QuickMiddle, HighOutside, BackRowPipe, Dump }

    /// <summary>Point-resolution outcomes (docs/m0-gameplay-spec.md §3.6).</summary>
    public enum RallyOutcome { Kill, Blocked, Tooled, Dug, Out, Net }

    public enum Position { S, OH, MB, OP, L }

    public enum Playstyle { Power, Quick, Technique, Guts }

    public enum Rarity { R, SR, SSR }

    public enum DifficultyTier { Easy, Normal, Hard }

    /// <summary>3×3 court zone grid, cols L/C/R × rows F(ront)/M(id)/B(ack) — docs/m0-gameplay-spec.md §4.</summary>
    public enum ZoneId { z_LF, z_CF, z_RF, z_LM, z_CM, z_RM, z_LB, z_CB, z_RB }
}
