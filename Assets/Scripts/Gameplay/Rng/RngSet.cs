using System;

namespace VG.Gameplay.Rng
{
    /// <summary>
    /// Per-stream seeds derived <c>stream = splitmix64(master ^ fnv1a64(streamName))</c> —
    /// docs/data-schemas.md §4.1. Serialized into replays and MatchResult.
    /// </summary>
    [Serializable]
    public struct SeedSet
    {
        public const int CurrentVersion = 1;

        public int Version;
        public ulong Master;
        public ulong Gacha;
        public ulong Rally;
        public ulong Ai;
        public ulong Substats;

        public static SeedSet FromMaster(ulong master) => new SeedSet
        {
            Version = CurrentVersion,
            Master = master,
            Gacha = Derive(master, "gacha"),
            Rally = Derive(master, "rally"),
            Ai = Derive(master, "ai"),
            Substats = Derive(master, "substats"),
        };

        public ulong Seed(RngStream stream)
        {
            switch (stream)
            {
                case RngStream.Gacha: return Gacha;
                case RngStream.Rally: return Rally;
                case RngStream.Ai: return Ai;
                case RngStream.Substats: return Substats;
                default: throw new ArgumentOutOfRangeException(nameof(stream), stream, null);
            }
        }

        private static ulong Derive(ulong master, string streamName)
        {
            ulong s = master ^ Fnv1a64(streamName);
            return SplitMix64.Next(ref s);
        }

        /// <summary>FNV-1a 64 over UTF-16 code units. Stable across platforms/runtimes. [structural]</summary>
        internal static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }

    /// <summary>
    /// The four named streams behind one <see cref="IRngSource"/>. Construct from a
    /// <see cref="SeedSet"/> at sim/meta boundary; restore mid-run via captured states.
    /// </summary>
    public sealed class RngSet : IRngSource
    {
        private const int StreamCount = 4;

        private readonly Xoshiro128StarStar[] _streams;

        public SeedSet Seeds { get; }

        public RngSet(SeedSet seeds)
        {
            Seeds = seeds;
            _streams = new Xoshiro128StarStar[StreamCount];
            for (int i = 0; i < StreamCount; i++)
                _streams[i] = Xoshiro128StarStar.FromSeed(seeds.Seed((RngStream)i));
        }

        private RngSet(SeedSet seeds, RngState[] states)
        {
            Seeds = seeds;
            _streams = new Xoshiro128StarStar[StreamCount];
            for (int i = 0; i < StreamCount; i++)
                _streams[i] = new Xoshiro128StarStar(states[i]);
        }

        public static RngSet FromMaster(ulong master) => new RngSet(SeedSet.FromMaster(master));

        /// <summary>Restore mid-run (replay checkpoint / save). States indexed by <see cref="RngStream"/>.</summary>
        public static RngSet Restore(SeedSet seeds, RngState[] states)
        {
            if (states == null || states.Length != StreamCount)
                throw new ArgumentException("Expected exactly 4 stream states.", nameof(states));
            return new RngSet(seeds, states);
        }

        public IRng Get(RngStream stream) => _streams[(int)stream];

        /// <summary>Snapshot all stream states, indexed by <see cref="RngStream"/>.</summary>
        public RngState[] CaptureStates()
        {
            var states = new RngState[StreamCount];
            for (int i = 0; i < StreamCount; i++)
                states[i] = _streams[i].State;
            return states;
        }
    }
}
