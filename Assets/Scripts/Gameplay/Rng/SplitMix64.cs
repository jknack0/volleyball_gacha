namespace VG.Gameplay.Rng
{
    /// <summary>
    /// SplitMix64 (Vigna) — seed expander/deriver only. Never used as a live game stream;
    /// its job is turning one master seed into well-distributed per-stream xoshiro states
    /// (docs/data-schemas.md §4.1).
    /// </summary>
    public static class SplitMix64
    {
        public static ulong Next(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
