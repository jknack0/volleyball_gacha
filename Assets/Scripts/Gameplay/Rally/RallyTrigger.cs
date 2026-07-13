namespace VG.Gameplay.Rally
{
    /// <summary>
    /// docs/m0-gameplay-spec.md §1.2/§1.3 — one member per distinct transition trigger.
    /// T-row citations refer to the §1.2 transition table. Timeout triggers are fired
    /// internally by <see cref="RallyStateMachine.Tick"/> when the state's timer elapses,
    /// but remain externally fireable (the (state, trigger) pair is the table row).
    /// </summary>
    public enum RallyTrigger
    {
        /// <summary>T1: lineups valid — MatchStart → PreServe.</summary>
        LineupsValid,

        /// <summary>T2: presentation done — PreServe → ServeAim. Timer-fired after PreServePresentationTicks.</summary>
        PresentationDone,

        /// <summary>T3: player releases hold — ServeAim → ServeContact.</summary>
        ServeReleased,

        /// <summary>T3 timeout: 8 s elapsed — ServeAim → ServeContact (auto-serve at current meter, grade capped Good — payload is VB-7/8).</summary>
        ServeAimTimedOut,

        /// <summary>T4/T12: trajectory authored — ServeContact → BallInFlight; AttackContact (+BlockWindow resolution) → BallInFlight.</summary>
        TrajectoryAuthored,

        /// <summary>T5: ball crosses net plane toward receiving side — BallInFlight → ReceiveWindow. Also the free-ball entry (§1.2 free-ball rule).</summary>
        BallCrossedNet,

        /// <summary>T6: receive grade ≥ C (or playable Shank per §3.3/§3.4 desperation-ball rule) — ReceiveWindow → SetSelect.</summary>
        ReceiveSucceeded,

        /// <summary>T7: Shank with unplayable quality (§3.3) or no commit before arrival (= ace) — ReceiveWindow → PointResolved.</summary>
        ReceiveFailed,

        /// <summary>T8: lane chosen — SetSelect → BallInFlight.</summary>
        LaneChosen,

        /// <summary>T8 timeout: SetSelect window elapsed — SetSelect → BallInFlight (auto high-outside, set grade capped Good — payload is VB-7/8).</summary>
        SetSelectTimedOut,

        /// <summary>T9: set arc ascending; spiker begins approach — BallInFlight → AttackApproach.</summary>
        SetArcAscending,

        /// <summary>T10: attacker tap inside spike window — AttackApproach → AttackContact.</summary>
        AttackTapped,

        /// <summary>T10: apex passed with no tap — AttackApproach → AttackContact (resolves as Miss → free ball, §3.6 case E — payload is VB-7/8).</summary>
        ApexPassed,

        /// <summary>T11: defense commit input, or AI block decision tick — parallel BlockWindow entry; does NOT leave AttackApproach.</summary>
        DefenseCommitted,

        /// <summary>T13: spike/deflection heading to defending court, not terminal — BallInFlight → DigWindow.</summary>
        SpikePlayable,

        /// <summary>T14: dig success (§3.6) — DigWindow → SetSelect; cascade repeats.</summary>
        DigSucceeded,

        /// <summary>T15: terminal outcome (kill, blocked, tooled, out, net) — BallInFlight / DigWindow → PointResolved.</summary>
        TerminalOutcome,

        /// <summary>T16: score applied — PointResolved → Rotation. Timer-fired after PointResolvedPresentationTicks.</summary>
        ScoreApplied,

        /// <summary>T17: rotation applied — Rotation → PreServe. Timer-fired after RotationPresentationTicks.</summary>
        RotationApplied,

        /// <summary>T18: match point reached — Rotation → MatchEnd.</summary>
        MatchPointReached,

        /// <summary>§1.3 interrupt: signature-move activation. Legal only in ServeAim, ReceiveWindow, SetSelect, AttackApproach, BlockWindow. Pauses the sim clock; state does not change.</summary>
        SignatureActivated,

        /// <summary>§1.3 interrupt: cut-in finished — sim resumes at the exact paused tick. Legal only while paused.</summary>
        SignatureCutInEnded,

        /// <summary>§1.3 interrupt: team Hype crossed 100. Never changes rally state; applies a hit-stop that shifts open windows by its duration.</summary>
        IgnitionOnset,
    }
}
