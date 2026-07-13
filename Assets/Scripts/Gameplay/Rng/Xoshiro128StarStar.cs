using System;

namespace VG.Gameplay.Rng
{
    /// <summary>Serializable PRNG state — 4×uint xoshiro128** words (save files, replay checkpoints).</summary>
    [Serializable]
    public struct RngState
    {
        public uint S0, S1, S2, S3;
    }

    /// <summary>
    /// xoshiro128** 1.1 (Blackman &amp; Vigna) — engine-free, allocation-free, serializable.
    /// [structural per docs/data-schemas.md §4.1]
    /// </summary>
    public sealed class Xoshiro128StarStar : IRng
    {
        private uint _s0, _s1, _s2, _s3;

        public Xoshiro128StarStar(RngState state)
        {
            // All-zero is the single illegal xoshiro state (fixed point of all-zero output).
            if ((state.S0 | state.S1 | state.S2 | state.S3) == 0)
                state.S0 = 0x9E3779B9;
            _s0 = state.S0;
            _s1 = state.S1;
            _s2 = state.S2;
            _s3 = state.S3;
        }

        public static Xoshiro128StarStar FromSeed(ulong seed)
        {
            ulong sm = seed;
            ulong a = SplitMix64.Next(ref sm);
            ulong b = SplitMix64.Next(ref sm);
            return new Xoshiro128StarStar(new RngState
            {
                S0 = (uint)a,
                S1 = (uint)(a >> 32),
                S2 = (uint)b,
                S3 = (uint)(b >> 32),
            });
        }

        public RngState State => new RngState { S0 = _s0, S1 = _s1, S2 = _s2, S3 = _s3 };

        private static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        public uint NextUInt()
        {
            uint result = Rotl(_s1 * 5u, 7) * 9u;
            uint t = _s1 << 9;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 11);

            return result;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Range must be non-empty.");
            uint range = (uint)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(((ulong)NextUInt() * range) >> 32));
        }

        public float NextFloat01() => (NextUInt() >> 8) * (1f / 16777216f);
    }
}
