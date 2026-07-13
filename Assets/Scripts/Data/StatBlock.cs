using System;

namespace VG.Data
{
    /// <summary>
    /// Six raw stats, 0..200 (docs/data-schemas.md §1.1; the raw scale is owned by
    /// docs/economy-progression.md). Gameplay math consumes <see cref="Normalized"/> 0..1 only.
    /// Fields are mutable ints for Unity serialization; out-of-range values clamp at read —
    /// authoring-time validation lives in the content importer (docs/tooling-pipeline.md §1).
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        public const int RawMax = 200;

        public int Power;
        public int Jump;
        public int Technique;
        public int Serve;
        public int Receive;
        public int Speed;

        public int Raw(StatId id)
        {
            switch (id)
            {
                case StatId.Power: return Power;
                case StatId.Jump: return Jump;
                case StatId.Technique: return Technique;
                case StatId.Serve: return Serve;
                case StatId.Receive: return Receive;
                case StatId.Speed: return Speed;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        /// <summary>Raw / 200, clamped to [0, 1]. The only form rally math may consume. [structural]</summary>
        public float Normalized(StatId id)
        {
            int raw = Raw(id);
            if (raw <= 0) return 0f;
            if (raw >= RawMax) return 1f;
            return raw / (float)RawMax;
        }
    }
}
