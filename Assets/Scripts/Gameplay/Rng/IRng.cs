namespace VG.Gameplay.Rng
{
    /// <summary>
    /// docs/data-schemas.md §4.1 [structural]. Every random draw in sim and meta flows through
    /// this interface by injection — no statics, no time-based seeding, and UnityEngine.Random
    /// is PROHIBITED in VG.Data / sim code (enforced structurally: these asmdefs set
    /// noEngineReferences, and the dotnet test host would fail to compile an engine reference).
    /// </summary>
    public interface IRng
    {
        uint NextUInt();

        /// <summary>Uniform in [minInclusive, maxExclusive). Deterministic multiply-shift bound (bias &lt; 2^-32).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Uniform in [0, 1) with 24-bit mantissa precision.</summary>
        float NextFloat01();
    }

    /// <summary>
    /// Named streams [structural]: one system's consumption never shifts another's sequence.
    /// A UI-driven extra gacha roll cannot change the next rally; an inserted AI decision cannot
    /// move the next substat roll. All player-economy RNG (incl. stage drops) uses Gacha.
    /// </summary>
    public enum RngStream
    {
        Gacha = 0,
        Rally = 1,
        Ai = 2,
        Substats = 3,
    }

    public interface IRngSource
    {
        IRng Get(RngStream stream);
    }
}
