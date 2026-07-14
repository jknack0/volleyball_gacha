namespace VG.Gameplay.Input
{
    /// <summary>
    /// §7.1–7.3 gesture thresholds. Pixel values are density-independent px at 160 dpi
    /// reference [structural]; the Unity layer converts device px before classification.
    /// Defaults = spec values.
    /// </summary>
    public sealed class InputTunables
    {
        /// <summary>Tap: down→up under 150 ms — §7.1 [tunable].</summary>
        public float TapMaxMs = 150f;

        /// <summary>Tap: travel under 24 px — §7.1 [tunable].</summary>
        public float TapMaxTravelPx = 24f;

        /// <summary>Swipe: travel ≥ 60 px — §7.1 [tunable].</summary>
        public float SwipeMinTravelPx = 60f;

        /// <summary>Swipe: completed within 250 ms — §7.1 [tunable].</summary>
        public float SwipeMaxMs = 250f;

        /// <summary>Input buffer: taps up to 100 ms BEFORE a window opens latch and evaluate as Δ = t_open − t — §7.2 [tunable].</summary>
        public float BufferMs = 100f;

        /// <summary>Double-tap: second tap within 80 ms is discarded — §7.3 [tunable].</summary>
        public float DoubleTapMs = 80f;

        /// <summary>Swipe within ±10° of a shot-mapping boundary resolves toward the safer shot — §7.3 [tunable].</summary>
        public float BoundaryDegrees = 10f;

        /// <summary>Short-swipe fallback: ≥ 24 px but &lt; 60 px = tap for timing, NO aim change — §7.3 [structural].</summary>
        public float ShortSwipeMinPx = 24f;

        /// <summary>§4.3: diagonal ≥ 25° from straight-ahead = cross [tunable].</summary>
        public float CrossAngleDegrees = 25f;

        /// <summary>§4.3: upward swipe shorter than 40% of SwipeMinTravelPx… roll uses short-UP swipes; spec pins 40% of min swipe length [tunable].</summary>
        public float RollLengthFraction = 0.4f;
    }
}
