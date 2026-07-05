namespace MochiV2.Core.Models
{
    /// <summary>
    /// A single FSM edge: from <see cref="From"/> state, on <see cref="Trigger"/>,
    /// transition to <see cref="To"/> state. PRD §10.
    /// </summary>
    /// <param name="From">Source state.</param>
    /// <param name="To">Target state.</param>
    /// <param name="Trigger">Trigger string (e.g. "walk", "done", "cursor_near").</param>
    public sealed record FSMTransition(FSMState From, FSMState To, string Trigger);
}