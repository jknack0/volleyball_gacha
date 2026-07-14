using System;

namespace VG.Gameplay.Input
{
    /// <summary>Raw pointer sample fed by the platform layer (device px pre-converted to 160-dpi reference px).</summary>
    public readonly struct PointerSample
    {
        public readonly int Tick;     // sim tick (dilation-converted at capture, §2.5/§7.2)
        public readonly float X;      // reference px
        public readonly float Y;

        public PointerSample(int tick, float x, float y)
        {
            Tick = tick;
            X = x;
            Y = y;
        }
    }

    public enum GestureKind { None, Tap, HoldRelease, Swipe, ShortSwipeAsTap }

    /// <summary>A classified gesture. Timestamp = touch-DOWN tick [structural §7.2].</summary>
    public readonly struct Gesture
    {
        public readonly GestureKind Kind;
        /// <summary>Touch-down tick — the timing evaluation timestamp (§7.2).</summary>
        public readonly int DownTick;
        /// <summary>Release tick (HoldRelease power evaluation happens here).</summary>
        public readonly int UpTick;
        /// <summary>Swipe direction, unit vector in screen space (+y up). Zero for non-swipes.</summary>
        public readonly float DirX;
        public readonly float DirY;

        public Gesture(GestureKind kind, int downTick, int upTick, float dirX = 0f, float dirY = 0f)
        {
            Kind = kind;
            DownTick = downTick;
            UpTick = upTick;
            DirX = dirX;
            DirY = dirY;
        }
    }

    /// <summary>
    /// §7.1 gesture grammar over pointer samples — pure, tick-based, engine-free.
    /// Feed Down/Move/Up; classified gestures come out of Up (drags are read continuously by
    /// the caller via <see cref="CurrentPosition"/>, no release requirement — §7.1).
    /// §7.3 forgiveness built in: short swipes degrade to timing taps with no aim change;
    /// double-taps within the window are discarded.
    /// </summary>
    public sealed class GestureClassifier
    {
        private const float TicksPerMs = 60f / 1000f;

        private readonly InputTunables _t;

        private bool _down;
        private PointerSample _downSample;
        private PointerSample _lastSample;
        private float _maxTravel;
        private int _lastTapTick = int.MinValue;

        public GestureClassifier(InputTunables tunables)
        {
            _t = tunables ?? throw new ArgumentNullException(nameof(tunables));
        }

        public bool IsDown => _down;

        /// <summary>Live pointer position while held — the drag read (serve reticle, §7.1).</summary>
        public PointerSample CurrentPosition => _lastSample;

        /// <summary>Ticks held so far (power meter progress read).</summary>
        public int HeldTicks(int nowTick) => _down ? nowTick - _downSample.Tick : 0;

        public void Down(in PointerSample sample)
        {
            _down = true;
            _downSample = sample;
            _lastSample = sample;
            _maxTravel = 0f;
        }

        public void Move(in PointerSample sample)
        {
            if (!_down) return;
            _lastSample = sample;
            float dx = sample.X - _downSample.X;
            float dy = sample.Y - _downSample.Y;
            float travel = MathF.Sqrt(dx * dx + dy * dy);
            if (travel > _maxTravel) _maxTravel = travel;
        }

        /// <summary>Release → classified gesture (§7.1 table, §7.3 forgiveness).</summary>
        public Gesture Up(in PointerSample sample)
        {
            if (!_down) return new Gesture(GestureKind.None, sample.Tick, sample.Tick);
            _down = false;
            _lastSample = sample;

            float dx = sample.X - _downSample.X;
            float dy = sample.Y - _downSample.Y;
            float travel = MathF.Sqrt(dx * dx + dy * dy);
            if (travel > _maxTravel) _maxTravel = travel;

            float heldMs = (sample.Tick - _downSample.Tick) / TicksPerMs;

            // Swipe: travel ≥ 60 px within 250 ms (§7.1).
            if (_maxTravel >= _t.SwipeMinTravelPx && heldMs <= _t.SwipeMaxMs)
            {
                float inv = 1f / MathF.Max(travel, 1e-4f);
                return new Gesture(GestureKind.Swipe, _downSample.Tick, sample.Tick, dx * inv, dy * inv);
            }

            // Short swipe (§7.3): ≥ 24 px but under swipe length → tap for timing, no aim.
            if (_maxTravel >= _t.ShortSwipeMinPx && heldMs <= _t.SwipeMaxMs)
                return Debounced(new Gesture(GestureKind.ShortSwipeAsTap, _downSample.Tick, sample.Tick));

            // Tap: quick + still (§7.1).
            if (heldMs < _t.TapMaxMs && _maxTravel < _t.TapMaxTravelPx)
                return Debounced(new Gesture(GestureKind.Tap, _downSample.Tick, sample.Tick));

            // Anything held ≥ 150 ms is a hold-release (power meter, §7.1).
            return new Gesture(GestureKind.HoldRelease, _downSample.Tick, sample.Tick);
        }

        /// <summary>§7.3: a second tap within 80 ms of the previous is discarded.</summary>
        private Gesture Debounced(in Gesture g)
        {
            // long math: the int.MinValue sentinel must not overflow the subtraction.
            float sinceMs = ((long)g.DownTick - _lastTapTick) / TicksPerMs;
            if (_lastTapTick != int.MinValue && sinceMs < _t.DoubleTapMs)
                return new Gesture(GestureKind.None, g.DownTick, g.UpTick);
            _lastTapTick = g.DownTick;
            return g;
        }
    }
}
