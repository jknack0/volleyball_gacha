using System;

namespace VG.Gameplay.Rally
{
    /// <summary>Team identity for machine-level bookkeeping (signature once-per-side rule §1.3).</summary>
    public enum TeamSide { Home = 0, Away = 1 }

    /// <summary>
    /// The rally state machine — docs/m0-gameplay-spec.md §1, implemented 1:1 against the §1.2
    /// transition table (T-row numbers cited inline). Pure, tick-driven, engine-free.
    ///
    /// Responsibilities: state sequencing, state timers (timeout triggers fired internally by
    /// <see cref="Tick"/>), §1.3 interrupt bookkeeping (signature pause, Ignition window shift),
    /// and the parallel BlockWindow flag (§1.2 T11 — never the value of <see cref="CurrentState"/>).
    /// NOT its job: contact math, trajectories, scores, Hype amounts — RallySim (VB-8) composes those.
    ///
    /// Allocation-free after construction: transitions are switch-based, hooks are pre-wired delegates.
    /// </summary>
    public sealed class RallyStateMachine
    {
        private readonly RallyTunables _tunables;

        private int _ticksInState;
        private int _timeoutDeadline;         // 0 = no timer armed
        private RallyTrigger _timeoutTrigger; // valid only when _timeoutDeadline > 0
        private bool _paused;                 // signature cut-in (§1.3): sim clock frozen
        private bool _sigUsedHome;            // one activation per side per rally state (§1.3)
        private bool _sigUsedAway;

        public RallyState CurrentState { get; private set; }

        /// <summary>§1.2 T11: block window is a parallel overlay on AttackApproach/AttackContact.</summary>
        public bool IsBlockWindowOpen { get; private set; }

        /// <summary>Sim clock paused by a signature cut-in (§1.3); <see cref="Tick"/> is a no-op.</summary>
        public bool IsPaused => _paused;

        /// <summary>Ticks spent in the current state (frozen while paused).</summary>
        public int TicksInState => _ticksInState;

        /// <summary>(entered, previous). BlockWindow surfaces here on open.</summary>
        public event Action<RallyState, RallyState> OnEnter;

        /// <summary>(exited, next). BlockWindow surfaces here on close.</summary>
        public event Action<RallyState, RallyState> OnExit;

        public RallyStateMachine(RallyTunables tunables)
        {
            _tunables = tunables ?? throw new ArgumentNullException(nameof(tunables));
            CurrentState = RallyState.MatchStart;
            ArmTimer();
        }

        /// <summary>
        /// Advance exactly one fixed sim step (§2.5). Fires the state's timeout trigger
        /// internally when its timer elapses. No-op while paused (§1.3 signature cut-in).
        /// </summary>
        public void Tick()
        {
            if (_paused) return;
            _ticksInState++;
            if (_timeoutDeadline > 0 && _ticksInState >= _timeoutDeadline)
                Fire(_timeoutTrigger);
        }

        /// <summary>
        /// Fire a transition trigger per the §1.2 table. Illegal (state, trigger) pairs throw.
        /// Signature interrupts go through <see cref="RequestSignature"/> instead.
        /// </summary>
        public void Fire(RallyTrigger trigger)
        {
            if (trigger == RallyTrigger.SignatureActivated)
                throw new InvalidOperationException(
                    "Signature activation carries a side — use RequestSignature(side).");

            if (_paused)
            {
                if (trigger != RallyTrigger.SignatureCutInEnded)
                    throw new InvalidOperationException(
                        $"Sim is paused by a signature cut-in; cannot fire {trigger}.");
                _paused = false; // §1.3: resumes at the exact paused tick — timers untouched
                return;
            }

            switch (trigger)
            {
                case RallyTrigger.SignatureCutInEnded:
                    throw new InvalidOperationException("SignatureCutInEnded while not paused.");

                case RallyTrigger.IgnitionOnset:
                    // §1.3: never changes rally state; open windows shift by the hit-stop so no
                    // input is stolen — implemented as a deadline extension.
                    if (_timeoutDeadline > 0)
                        _timeoutDeadline += _tunables.IgnitionHitStopTicks;
                    return;

                case RallyTrigger.DefenseCommitted:
                    // T11: parallel entry; does not leave AttackApproach.
                    if (CurrentState != RallyState.AttackApproach)
                        throw Illegal(trigger);
                    if (IsBlockWindowOpen)
                        throw new InvalidOperationException("Block window is already open (one commit per attack).");
                    IsBlockWindowOpen = true;
                    OnEnter?.Invoke(RallyState.BlockWindow, RallyState.AttackApproach);
                    return;

                default:
                    TransitionTo(Next(trigger));
                    return;
            }
        }

        /// <summary>
        /// §1.3 signature-move interrupt. Legal only in ServeAim / ReceiveWindow / SetSelect /
        /// AttackApproach, or while the parallel BlockWindow is open; once per side per rally
        /// state. Pauses the sim clock; resume with <see cref="RallyTrigger.SignatureCutInEnded"/>.
        /// Hype cost validation and contact-ownership checks are RallySim's job.
        /// </summary>
        public void RequestSignature(TeamSide side)
        {
            if (_paused)
                throw new InvalidOperationException("A signature cut-in is already playing.");

            bool stateLegal = CurrentState == RallyState.ServeAim
                           || CurrentState == RallyState.ReceiveWindow
                           || CurrentState == RallyState.SetSelect
                           || CurrentState == RallyState.AttackApproach
                           || IsBlockWindowOpen;
            if (!stateLegal)
                throw new InvalidOperationException(
                    $"Signature activation is illegal in {CurrentState} (§1.3).");

            bool used = side == TeamSide.Home ? _sigUsedHome : _sigUsedAway;
            if (used)
                throw new InvalidOperationException(
                    $"{side} already activated a signature in {CurrentState} (§1.3: one per side per rally state).");

            if (side == TeamSide.Home) _sigUsedHome = true;
            else _sigUsedAway = true;
            _paused = true;
        }

        // ---- transition table (§1.2, T-rows) -----------------------------------------------

        private RallyState Next(RallyTrigger trigger)
        {
            switch (CurrentState)
            {
                case RallyState.MatchStart:
                    if (trigger == RallyTrigger.LineupsValid) return RallyState.PreServe;                 // T1
                    break;

                case RallyState.PreServe:
                    if (trigger == RallyTrigger.PresentationDone) return RallyState.ServeAim;             // T2
                    break;

                case RallyState.ServeAim:
                    if (trigger == RallyTrigger.ServeReleased) return RallyState.ServeContact;            // T3
                    if (trigger == RallyTrigger.ServeAimTimedOut) return RallyState.ServeContact;         // T3 timeout
                    break;

                case RallyState.ServeContact:
                    if (trigger == RallyTrigger.TrajectoryAuthored) return RallyState.BallInFlight;       // T4
                    break;

                case RallyState.BallInFlight:
                    if (trigger == RallyTrigger.BallCrossedNet) return RallyState.ReceiveWindow;          // T5 (+ free ball)
                    if (trigger == RallyTrigger.SetArcAscending) return RallyState.AttackApproach;        // T9
                    if (trigger == RallyTrigger.SpikePlayable) return RallyState.DigWindow;               // T13
                    if (trigger == RallyTrigger.TerminalOutcome) return RallyState.PointResolved;         // T15
                    break;

                case RallyState.ReceiveWindow:
                    if (trigger == RallyTrigger.ReceiveSucceeded) return RallyState.SetSelect;            // T6
                    if (trigger == RallyTrigger.ReceiveFailed) return RallyState.PointResolved;           // T7
                    break;

                case RallyState.SetSelect:
                    if (trigger == RallyTrigger.LaneChosen) return RallyState.BallInFlight;               // T8
                    if (trigger == RallyTrigger.SetSelectTimedOut) return RallyState.BallInFlight;        // T8 timeout
                    break;

                case RallyState.AttackApproach:
                    if (trigger == RallyTrigger.AttackTapped) return RallyState.AttackContact;            // T10
                    if (trigger == RallyTrigger.ApexPassed) return RallyState.AttackContact;              // T10 (Miss payload)
                    break;

                case RallyState.AttackContact:
                    if (trigger == RallyTrigger.TrajectoryAuthored) return RallyState.BallInFlight;       // T12
                    break;

                case RallyState.DigWindow:
                    if (trigger == RallyTrigger.DigSucceeded) return RallyState.SetSelect;                // T14
                    if (trigger == RallyTrigger.TerminalOutcome) return RallyState.PointResolved;         // T15
                    break;

                case RallyState.PointResolved:
                    if (trigger == RallyTrigger.ScoreApplied) return RallyState.Rotation;                 // T16
                    break;

                case RallyState.Rotation:
                    if (trigger == RallyTrigger.RotationApplied) return RallyState.PreServe;              // T17
                    if (trigger == RallyTrigger.MatchPointReached) return RallyState.MatchEnd;            // T18
                    break;
            }

            throw Illegal(trigger);
        }

        private void TransitionTo(RallyState next)
        {
            RallyState previous = CurrentState;

            // Parallel BlockWindow closes when the attack resolves (T12: block engagement is
            // computed with the spike) — the only transition that keeps it open is
            // AttackApproach → AttackContact.
            if (IsBlockWindowOpen && next != RallyState.AttackContact)
            {
                IsBlockWindowOpen = false;
                OnExit?.Invoke(RallyState.BlockWindow, next);
            }

            OnExit?.Invoke(previous, next);
            CurrentState = next;
            _ticksInState = 0;
            _sigUsedHome = false;
            _sigUsedAway = false;
            ArmTimer();
            OnEnter?.Invoke(next, previous);
        }

        private void ArmTimer()
        {
            switch (CurrentState)
            {
                case RallyState.PreServe:
                    _timeoutDeadline = _tunables.PreServePresentationTicks;
                    _timeoutTrigger = RallyTrigger.PresentationDone;
                    break;
                case RallyState.ServeAim:
                    _timeoutDeadline = _tunables.ServeAimTimeoutTicks;
                    _timeoutTrigger = RallyTrigger.ServeAimTimedOut;
                    break;
                case RallyState.SetSelect:
                    _timeoutDeadline = _tunables.SetSelectTimeoutTicks;
                    _timeoutTrigger = RallyTrigger.SetSelectTimedOut;
                    break;
                case RallyState.PointResolved:
                    _timeoutDeadline = _tunables.PointResolvedPresentationTicks;
                    _timeoutTrigger = RallyTrigger.ScoreApplied;
                    break;
                case RallyState.Rotation:
                    _timeoutDeadline = _tunables.RotationPresentationTicks;
                    _timeoutTrigger = RallyTrigger.RotationApplied;
                    break;
                default:
                    _timeoutDeadline = 0;
                    break;
            }
        }

        private InvalidOperationException Illegal(RallyTrigger trigger)
            => new InvalidOperationException($"Illegal transition: {trigger} in state {CurrentState} (§1.2).");
    }
}
