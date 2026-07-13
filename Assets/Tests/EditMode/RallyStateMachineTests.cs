using System;
using System.Collections.Generic;
using NUnit.Framework;
using VG.Gameplay.Rally;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/m0-gameplay-spec.md §1: the §1.2 transition table 1:1 (T-rows cited),
    /// timer semantics, §1.3 interrupt rules (signature pause, once-per-side, Ignition shift),
    /// and the parallel BlockWindow (T11/T12).
    /// </summary>
    [TestFixture]
    public class RallyStateMachineTests
    {
        private static RallyStateMachine NewMachine() => new RallyStateMachine(new RallyTunables());

        /// <summary>Drive a fresh machine to <paramref name="target"/> along the canonical trigger path.</summary>
        private static RallyStateMachine At(RallyState target)
        {
            var m = NewMachine();
            foreach (var t in PathTo(target)) m.Fire(t);
            Assert.That(m.CurrentState, Is.EqualTo(target), "test-fixture path is broken");
            return m;
        }

        private static IEnumerable<RallyTrigger> PathTo(RallyState target)
        {
            if (target == RallyState.MatchStart) yield break;
            yield return RallyTrigger.LineupsValid;                    // → PreServe
            if (target == RallyState.PreServe) yield break;
            yield return RallyTrigger.PresentationDone;                // → ServeAim
            if (target == RallyState.ServeAim) yield break;
            yield return RallyTrigger.ServeReleased;                   // → ServeContact
            if (target == RallyState.ServeContact) yield break;
            yield return RallyTrigger.TrajectoryAuthored;              // → BallInFlight
            if (target == RallyState.BallInFlight) yield break;
            yield return RallyTrigger.BallCrossedNet;                  // → ReceiveWindow
            if (target == RallyState.ReceiveWindow) yield break;

            if (target == RallyState.PointResolved || target == RallyState.Rotation || target == RallyState.MatchEnd)
            {
                yield return RallyTrigger.ReceiveFailed;               // → PointResolved (T7)
                if (target == RallyState.PointResolved) yield break;
                yield return RallyTrigger.ScoreApplied;                // → Rotation (T16)
                if (target == RallyState.Rotation) yield break;
                yield return RallyTrigger.MatchPointReached;           // → MatchEnd (T18)
                yield break;
            }

            yield return RallyTrigger.ReceiveSucceeded;                // → SetSelect (T6)
            if (target == RallyState.SetSelect) yield break;
            yield return RallyTrigger.LaneChosen;                      // → BallInFlight (T8)
            yield return RallyTrigger.SetArcAscending;                 // → AttackApproach (T9)
            if (target == RallyState.AttackApproach) yield break;
            yield return RallyTrigger.AttackTapped;                    // → AttackContact (T10)
            if (target == RallyState.AttackContact) yield break;
            yield return RallyTrigger.TrajectoryAuthored;              // → BallInFlight (T12)
            yield return RallyTrigger.SpikePlayable;                   // → DigWindow (T13)
            if (target == RallyState.DigWindow) yield break;
            throw new ArgumentOutOfRangeException(nameof(target), target, "no canonical path");
        }

        // ---- §1.2 table, 1:1 --------------------------------------------------------------

        private static readonly (RallyState from, RallyTrigger trigger, RallyState to, string row)[] Table =
        {
            (RallyState.MatchStart, RallyTrigger.LineupsValid, RallyState.PreServe, "T1"),
            (RallyState.PreServe, RallyTrigger.PresentationDone, RallyState.ServeAim, "T2"),
            (RallyState.ServeAim, RallyTrigger.ServeReleased, RallyState.ServeContact, "T3"),
            (RallyState.ServeAim, RallyTrigger.ServeAimTimedOut, RallyState.ServeContact, "T3 timeout"),
            (RallyState.ServeContact, RallyTrigger.TrajectoryAuthored, RallyState.BallInFlight, "T4"),
            (RallyState.BallInFlight, RallyTrigger.BallCrossedNet, RallyState.ReceiveWindow, "T5"),
            (RallyState.ReceiveWindow, RallyTrigger.ReceiveSucceeded, RallyState.SetSelect, "T6"),
            (RallyState.ReceiveWindow, RallyTrigger.ReceiveFailed, RallyState.PointResolved, "T7"),
            (RallyState.SetSelect, RallyTrigger.LaneChosen, RallyState.BallInFlight, "T8"),
            (RallyState.SetSelect, RallyTrigger.SetSelectTimedOut, RallyState.BallInFlight, "T8 timeout"),
            (RallyState.BallInFlight, RallyTrigger.SetArcAscending, RallyState.AttackApproach, "T9"),
            (RallyState.AttackApproach, RallyTrigger.AttackTapped, RallyState.AttackContact, "T10"),
            (RallyState.AttackApproach, RallyTrigger.ApexPassed, RallyState.AttackContact, "T10 apex-miss"),
            (RallyState.AttackContact, RallyTrigger.TrajectoryAuthored, RallyState.BallInFlight, "T12"),
            (RallyState.BallInFlight, RallyTrigger.SpikePlayable, RallyState.DigWindow, "T13"),
            (RallyState.DigWindow, RallyTrigger.DigSucceeded, RallyState.SetSelect, "T14"),
            (RallyState.BallInFlight, RallyTrigger.TerminalOutcome, RallyState.PointResolved, "T15"),
            (RallyState.DigWindow, RallyTrigger.TerminalOutcome, RallyState.PointResolved, "T15 dig"),
            (RallyState.PointResolved, RallyTrigger.ScoreApplied, RallyState.Rotation, "T16"),
            (RallyState.Rotation, RallyTrigger.RotationApplied, RallyState.PreServe, "T17"),
            (RallyState.Rotation, RallyTrigger.MatchPointReached, RallyState.MatchEnd, "T18"),
        };

        [Test]
        public void TransitionTable_MatchesSpec_Row1To1()
        {
            // Bug caught: any table row wired to the wrong destination or silently dropped.
            foreach (var (from, trigger, to, row) in Table)
            {
                var m = At(from);
                m.Fire(trigger);
                Assert.That(m.CurrentState, Is.EqualTo(to), $"{row}: {from} --{trigger}--> expected {to}");
            }
        }

        [Test]
        public void IllegalPairs_Throw_WithStateAndTriggerNamed()
        {
            // Bug caught: illegal transitions silently ignored (desyncs RallySim from reality).
            (RallyState, RallyTrigger)[] illegal =
            {
                (RallyState.MatchStart, RallyTrigger.ServeReleased),
                (RallyState.BallInFlight, RallyTrigger.LineupsValid),
                (RallyState.PointResolved, RallyTrigger.BallCrossedNet),
                (RallyState.ServeAim, RallyTrigger.DigSucceeded),
                (RallyState.ReceiveWindow, RallyTrigger.AttackTapped),
                (RallyState.MatchEnd, RallyTrigger.LineupsValid),
            };
            foreach (var (state, trigger) in illegal)
            {
                var m = At(state);
                var ex = Assert.Throws<InvalidOperationException>(() => m.Fire(trigger), $"{state}/{trigger}");
                Assert.That(ex.Message, Does.Contain(state.ToString()).And.Contain(trigger.ToString()));
            }
        }

        [Test]
        public void PointResolved_IsReachable_FromEveryContactState()
        {
            // Bug caught: a contact state with no path to rally termination (soft-locked rally).
            var m = At(RallyState.ServeAim);
            m.Fire(RallyTrigger.ServeReleased);
            m.Fire(RallyTrigger.TrajectoryAuthored);
            m.Fire(RallyTrigger.TerminalOutcome); // service fault
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PointResolved), "from ServeAim");

            m = At(RallyState.ReceiveWindow);
            m.Fire(RallyTrigger.ReceiveFailed); // ace
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PointResolved), "from ReceiveWindow");

            m = At(RallyState.SetSelect);
            m.Fire(RallyTrigger.LaneChosen);
            m.Fire(RallyTrigger.TerminalOutcome); // set sails out
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PointResolved), "from SetSelect");

            m = At(RallyState.AttackApproach);
            m.Fire(RallyTrigger.ApexPassed);
            m.Fire(RallyTrigger.TrajectoryAuthored);
            m.Fire(RallyTrigger.TerminalOutcome);
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PointResolved), "from AttackApproach");

            m = At(RallyState.AttackContact);
            m.Fire(RallyTrigger.TrajectoryAuthored);
            m.Fire(RallyTrigger.TerminalOutcome); // kill
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PointResolved), "from AttackContact");

            m = At(RallyState.DigWindow);
            m.Fire(RallyTrigger.TerminalOutcome);
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PointResolved), "from DigWindow");
        }

        [Test]
        public void DigLoop_CascadeRepeats_AcrossMultipleExchanges()
        {
            // Bug caught: per-state bookkeeping not reset on re-entry (rally can't extend).
            var m = At(RallyState.DigWindow);
            for (int exchange = 0; exchange < 3; exchange++)
            {
                m.Fire(RallyTrigger.DigSucceeded);            // T14 → SetSelect
                m.Fire(RallyTrigger.LaneChosen);              // → BallInFlight
                m.Fire(RallyTrigger.SetArcAscending);         // → AttackApproach
                m.Fire(RallyTrigger.AttackTapped);            // → AttackContact
                m.Fire(RallyTrigger.TrajectoryAuthored);      // → BallInFlight
                m.Fire(RallyTrigger.SpikePlayable);           // → DigWindow
            }
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.DigWindow));
        }

        // ---- timers -------------------------------------------------------------------------

        [Test]
        public void Timer_Fires_ExactlyAtTheDeadline_NotOneTickEarly()
        {
            // Bug caught: off-by-one in timeout firing (§1.2 T2: 1.0 s = 60 ticks).
            var m = At(RallyState.PreServe);
            for (int i = 0; i < 59; i++) m.Tick();
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PreServe), "fired a tick early");
            m.Tick();
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeAim), "did not fire at the deadline");
        }

        [Test]
        public void ServeAim_TimesOut_IntoAutoServe()
        {
            var m = At(RallyState.ServeAim);
            for (int i = 0; i < 480; i++) m.Tick();
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeContact), "T3 timeout must auto-serve");
        }

        [Test]
        public void Timers_Rearm_OnStateReentry()
        {
            // Bug caught: stale deadline surviving re-entry (second rally's PreServe never advances).
            var m = At(RallyState.PointResolved);
            for (int i = 0; i < 90; i++) m.Tick();  // T16 → Rotation
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.Rotation));
            for (int i = 0; i < 48; i++) m.Tick();  // T17 → PreServe (second rally)
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.PreServe));
            for (int i = 0; i < 60; i++) m.Tick();  // T2 again, fresh 60
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeAim));
        }

        [Test]
        public void StatesWithoutTimers_NeverTimeout()
        {
            var m = At(RallyState.BallInFlight);
            for (int i = 0; i < 10_000; i++) m.Tick();
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.BallInFlight));
        }

        // ---- §1.3 interrupts ------------------------------------------------------------------

        [Test]
        public void Signature_PausesClock_AndResumesAtTheExactTick()
        {
            // Bug caught: cut-in eating window time (spec: "sim resumes at the exact paused tick").
            var m = At(RallyState.ServeAim);
            for (int i = 0; i < 100; i++) m.Tick();

            m.RequestSignature(TeamSide.Home);
            Assert.That(m.IsPaused, Is.True);
            for (int i = 0; i < 1000; i++) m.Tick(); // frozen — must not time out
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeAim));
            Assert.That(m.TicksInState, Is.EqualTo(100), "pause must freeze the state clock");

            m.Fire(RallyTrigger.SignatureCutInEnded);
            Assert.That(m.IsPaused, Is.False);
            for (int i = 0; i < 379; i++) m.Tick(); // 100 + 379 = 479 < 480
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeAim));
            m.Tick();                                // 480 exactly
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeContact));
        }

        [Test]
        public void Signature_IsIllegal_OutsideTheFiveOwnableStates()
        {
            var m = At(RallyState.PreServe);
            Assert.Throws<InvalidOperationException>(() => m.RequestSignature(TeamSide.Home));

            m = At(RallyState.BallInFlight);
            Assert.Throws<InvalidOperationException>(() => m.RequestSignature(TeamSide.Away));
        }

        [Test]
        public void Signature_OncePerSide_PerRallyState()
        {
            // Bug caught: signature spam within one window (§1.3 "one activation per side per rally state").
            var m = At(RallyState.SetSelect);
            m.RequestSignature(TeamSide.Home);
            m.Fire(RallyTrigger.SignatureCutInEnded);
            Assert.Throws<InvalidOperationException>(() => m.RequestSignature(TeamSide.Home), "same side, same state");

            m.RequestSignature(TeamSide.Away); // other side may still activate
            m.Fire(RallyTrigger.SignatureCutInEnded);

            m.Fire(RallyTrigger.LaneChosen);
            m.Fire(RallyTrigger.SetArcAscending); // new state → flags reset
            m.RequestSignature(TeamSide.Home);
            Assert.That(m.IsPaused, Is.True);
        }

        [Test]
        public void NormalTriggers_AreRejected_WhilePaused()
        {
            var m = At(RallyState.SetSelect);
            m.RequestSignature(TeamSide.Home);
            Assert.Throws<InvalidOperationException>(() => m.Fire(RallyTrigger.LaneChosen));
        }

        [Test]
        public void Ignition_NeverChangesState_AndShiftsTheOpenWindow()
        {
            // Bug caught: hit-stop stealing input time (§1.3: "windows shift by the hit-stop duration").
            var m = At(RallyState.ServeAim);
            for (int i = 0; i < 470; i++) m.Tick();

            m.Fire(RallyTrigger.IgnitionOnset);
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeAim), "Ignition must not change state");

            for (int i = 0; i < 27; i++) m.Tick(); // 470 + 27 = 497 < 480 + 18
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeAim), "window must be extended by the hit-stop");
            m.Tick();                               // 498 = 480 + 18 exactly
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.ServeContact));
        }

        // ---- parallel BlockWindow (T11/T12) ---------------------------------------------------

        [Test]
        public void BlockWindow_OpensInParallel_WithoutLeavingAttackApproach()
        {
            var m = At(RallyState.AttackApproach);
            m.Fire(RallyTrigger.DefenseCommitted);
            Assert.That(m.CurrentState, Is.EqualTo(RallyState.AttackApproach), "T11: parallel entry");
            Assert.That(m.IsBlockWindowOpen, Is.True);
        }

        [Test]
        public void BlockWindow_SecondCommit_Throws()
        {
            var m = At(RallyState.AttackApproach);
            m.Fire(RallyTrigger.DefenseCommitted);
            Assert.Throws<InvalidOperationException>(() => m.Fire(RallyTrigger.DefenseCommitted));
        }

        [Test]
        public void BlockWindow_IsIllegal_OutsideAttackApproach()
        {
            var m = At(RallyState.SetSelect);
            Assert.Throws<InvalidOperationException>(() => m.Fire(RallyTrigger.DefenseCommitted));
        }

        [Test]
        public void BlockWindow_SurvivesTheApexTap_AndClosesWhenTheSpikeResolves()
        {
            // Bug caught: window closing at AttackContact entry (block must resolve WITH the attack, T12).
            var exits = new List<(RallyState exited, RallyState next)>();
            var m = At(RallyState.AttackApproach);
            m.OnExit += (exited, next) => exits.Add((exited, next));

            m.Fire(RallyTrigger.DefenseCommitted);
            m.Fire(RallyTrigger.AttackTapped);
            Assert.That(m.IsBlockWindowOpen, Is.True, "window resolves with the attack, not at the tap");

            m.Fire(RallyTrigger.TrajectoryAuthored); // T12 → BallInFlight
            Assert.That(m.IsBlockWindowOpen, Is.False);
            Assert.That(exits, Does.Contain((RallyState.BlockWindow, RallyState.BallInFlight)));
        }

        [Test]
        public void BlockWindow_SignatureActivation_IsLegalWhileOpen()
        {
            var m = At(RallyState.AttackApproach);
            m.Fire(RallyTrigger.DefenseCommitted);
            m.RequestSignature(TeamSide.Away); // the blocking side's read — §1.3 lists BlockWindow
            Assert.That(m.IsPaused, Is.True);
        }

        // ---- hooks ------------------------------------------------------------------------------

        [Test]
        public void Hooks_ReportEnterAndExit_WithCorrectPairs()
        {
            var m = NewMachine();
            (RallyState entered, RallyState previous)? enter = null;
            (RallyState exited, RallyState next)? exit = null;
            m.OnEnter += (a, b) => enter = (a, b);
            m.OnExit += (a, b) => exit = (a, b);

            m.Fire(RallyTrigger.LineupsValid);
            Assert.That(exit, Is.EqualTo((RallyState.MatchStart, RallyState.PreServe)));
            Assert.That(enter, Is.EqualTo((RallyState.PreServe, RallyState.MatchStart)));
        }
    }
}
