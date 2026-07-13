namespace VG.Tests
{
    /// <summary>
    /// Regression pins for deterministic derivations (see RngTests golden test), captured
    /// from the first verified run (dotnet 8.0.422, arm64). A change here means replay/save
    /// derivation broke: bump SeedSet.CurrentVersion and migrate — never repin silently.
    /// </summary>
    internal static class GoldenVectors
    {
        internal const ulong Gacha = 13548777092215811879UL;
        internal const ulong Rally = 14740059474749261744UL;
        internal const ulong Ai = 16917368604825652286UL;
        internal const ulong Substats = 10135427439119160267UL;

        internal static readonly uint[] FirstDraws =
        {
            1877459470u, // Gacha
            870663021u,  // Rally
            887159109u,  // Ai
            1512238439u, // Substats
        };
    }
}
