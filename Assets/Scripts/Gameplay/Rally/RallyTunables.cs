namespace VG.Gameplay.Rally
{
    /// <summary>
    /// Rally state-machine timer durations in sim ticks (1/60 s) — docs/m0-gameplay-spec.md
    /// §1.2/§1.3. Defaults = spec values. Consolidation into ScriptableObjects at VB-12+.
    /// </summary>
    public sealed class RallyTunables
    {
        /// <summary>T2: PreServe presentation, 1.0 s = 60 ticks [tunable].</summary>
        public int PreServePresentationTicks = 60;

        /// <summary>T3: ServeAim timeout, 8 s = 480 ticks → auto-serve (grade cap is resolution-layer) [tunable].</summary>
        public int ServeAimTimeoutTicks = 480;

        /// <summary>
        /// T8: SetSelect timeout. Spec: "2.5 s real-time at 0.3× time dilation" — dilation is
        /// presentation-only (§2.5), so the SIM sees 2.5 × 0.3 = 0.75 s = 45 ticks [tunable].
        /// </summary>
        public int SetSelectTimeoutTicks = 45;

        /// <summary>T16: PointResolved presentation, 1.5 s = 90 ticks [tunable].</summary>
        public int PointResolvedPresentationTicks = 90;

        /// <summary>T17: Rotation presentation, 0.8 s = 48 ticks [tunable].</summary>
        public int RotationPresentationTicks = 48;

        /// <summary>§1.3: Ignition hit-stop 0.3 s = 18 ticks; open windows shift by this so no input is stolen [tunable].</summary>
        public int IgnitionHitStopTicks = 18;
    }
}
