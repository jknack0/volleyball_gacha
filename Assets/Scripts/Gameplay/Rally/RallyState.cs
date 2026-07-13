namespace VG.Gameplay.Rally
{
    /// <summary>
    /// docs/m0-gameplay-spec.md §1.1 — the 14 rally states, verbatim. One machine per rally,
    /// owned by RallySim; the defending side's Block/Dig windows are states of the same machine.
    ///
    /// BlockWindow is special (§1.2 T11: "parallel entry; does not leave AttackApproach"):
    /// it is never the value of <see cref="RallyStateMachine.CurrentState"/>. It surfaces via
    /// <see cref="RallyStateMachine.IsBlockWindowOpen"/> and the OnEnter/OnExit hooks.
    /// </summary>
    public enum RallyState
    {
        /// <summary>Lineup validation, coin flip for first serve ⚄ (RNG owned by RallySim, not this machine).</summary>
        MatchStart,

        /// <summary>Rotation/legality check, libero auto-swap, camera settles. Presentation only.</summary>
        PreServe,

        /// <summary>Drag-aim + hold-release power meter live.</summary>
        ServeAim,

        /// <summary>Instantaneous: timing grade at release, serve quality, trajectory authoring.</summary>
        ServeContact,

        /// <summary>Trajectory playback (§2). Re-entered after every contact. Emits plane-crossing events.</summary>
        BallInFlight,

        /// <summary>Commit tap selects receiver; timed tap on ball arrival. Also handles free balls.</summary>
        ReceiveWindow,

        /// <summary>Time-dilated attacker-lane choice (options gated by receive grade).</summary>
        SetSelect,

        /// <summary>Spiker approach; apex window arms near apex. Defense may enter BlockWindow in parallel.</summary>
        AttackApproach,

        /// <summary>Timed tap + swipe aim at apex.</summary>
        AttackContact,

        /// <summary>Read-or-commit + timed jump tap + hand-position swipe. Opens during AttackApproach, resolves at AttackContact.</summary>
        BlockWindow,

        /// <summary>Timed tap on spike arrival if ball passes/deflects off block.</summary>
        DigWindow,

        /// <summary>Outcome from §3.6, score + Hype updates, kill replay presentation.</summary>
        PointResolved,

        /// <summary>Sideout rotation on serve win, libero auto-swap, legality recompute.</summary>
        Rotation,

        /// <summary>First to 11 (quick) / 15 (story) / 25 (finale), win by 2 [structural].</summary>
        MatchEnd,
    }
}
