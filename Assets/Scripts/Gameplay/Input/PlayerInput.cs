using System;

namespace VG.Gameplay.Input
{
    /// <summary>
    /// Quantized player input events — the replay-format vocabulary (data-schemas §4.3:
    /// "inputs keyed by sim tick, analog values quantized to int"). Live play and replays feed
    /// the sim the SAME stream; a recorded log replays bit-identically [structural].
    /// </summary>
    public enum PlayerInputKind
    {
        /// <summary>Serve: hold released. A = power_q (0..255 meter position), B = packed aim (zone index).</summary>
        ServeRelease,
        /// <summary>Receive: commit tap (selects the receive; §1.2 T5 window). No params in v0 (receiver auto by formation).</summary>
        ReceiveCommit,
        /// <summary>Receive/dig: the timing tap. Evaluated against ball-arrival tick.</summary>
        TimingTap,
        /// <summary>SetSelect: lane chosen. A = (int)SetOption.</summary>
        SetChoice,
        /// <summary>Spike: apex tap with aim. A = (int)SpikeShot, B = attack lane column at contact.</summary>
        SpikeTap,
    }

    /// <summary>One quantized input event. 12 bytes, replay-serializable.</summary>
    [Serializable]
    public struct PlayerInput
    {
        /// <summary>Sim tick the gesture's TIMESTAMP maps to (§7.2: captured at touch-down, dilation-converted).</summary>
        public int Tick;
        public PlayerInputKind Kind;
        public int A;
        public int B;

        public PlayerInput(int tick, PlayerInputKind kind, int a = 0, int b = 0)
        {
            Tick = tick;
            Kind = kind;
            A = a;
            B = b;
        }
    }

    /// <summary>§4.3 spike shots — swipe direction compiled to a shot, then to a zone by lane.</summary>
    public enum SpikeShot
    {
        Line,
        Cross,
        Roll,
        Feint,
    }
}
