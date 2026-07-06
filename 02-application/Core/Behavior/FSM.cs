using System;
using System.Collections.Generic;
using System.Linq;
using MochiV2.Core.Models;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Finite State Machine for Mochi. PRD §10.
    /// Holds a transition table, current/previous state (for interrupt restore),
    /// and validates that every playOnce state has at least one terminal transition.
    /// </summary>
    public sealed class FSM
    {
        /// <summary>
        /// States whose manifest playback mode is playOnce / playOnceReversed /
        /// playOnceThenHoldLast. Every one MUST declare a terminal transition —
        /// PRD §10 RULES: "no dead ends".
        /// </summary>
        public static readonly IReadOnlySet<FSMState> PlayOnceStates = new HashSet<FSMState>
        {
            FSMState.JumpVar1,
            FSMState.JumpVar2,
            FSMState.MeowLeft,
            FSMState.MeowRight,
            FSMState.ScratchLeft,
            FSMState.ScratchRight,
            FSMState.Blink,
            FSMState.Surprised,
            FSMState.Fall,
            FSMState.WakeUp,
            // Sleeping yawn→hold is playOnceThenHoldLast; it still needs a terminal
            // exit (e.g. wake). Treat as playOnce for dead-end checking.
            FSMState.Sleeping,
            //Drag interrupt-only treated non-terminal until released.
            //NOT add Drag terminal path RestoreFromInterrupt.
            FSMState.ClimbUp,
        };

        private readonly Dictionary<(FSMState From, string Trigger), FSMState> _transitions;

        /// <summary>
        /// Create an FSM with the given transition table. The table is validated:
        /// every playOnce state must have at least one outgoing transition.
        /// </summary>
        /// <param name="transitions">Transition table keyed by (from, trigger).</param>
        /// <exception cref="InvalidOperationException">
        /// A playOnce state has no outgoing (terminal) transition — dead end.
        /// </exception>
        public FSM(Dictionary<(FSMState, string), FSMState> transitions)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            ValidateNoDeadEnds();
            CurrentState = FSMState.Idle;
            PreviousState = null;
        }

        /// <summary>Current FSM state.</summary>
        public FSMState CurrentState { get; private set; }

        /// <summary>
        /// State saved when an interrupt occurred, or <c>null</c> if no interrupt
        /// is active. <see cref="RestoreFromInterrupt"/> returns here.
        /// </summary>
        public FSMState? PreviousState { get; private set; }

        /// <summary>
        /// Raised after a successful state change. Carries the old and new state.
        /// Subscribers (AnimationManager, AudioManager, …) listen via the event bus
        /// in production; this event is the low-level hook used by tests.
        /// </summary>
        public event Action<FSMState, FSMState>? StateChanged;

        /// <summary>True if <see cref="PreviousState"/> is set (interrupt active).</summary>
        public bool IsInterruptActive => PreviousState.HasValue;

        /// <summary>
        /// Check whether a transition exists from <paramref name="from"/> on
        /// <paramref name="trigger"/> without performing it.
        /// </summary>
        public bool CanTransition(FSMState from, string trigger)
            => _transitions.ContainsKey((from, trigger));

        /// <summary>
        /// Fire <paramref name="trigger"/> from the current state. Throws if no
        /// transition is defined for (CurrentState, trigger).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No transition defined for (CurrentState, <paramref name="trigger"/>).
        /// </exception>
        public void Fire(string trigger)
        {
            if (!_transitions.TryGetValue((CurrentState, trigger), out var target))
            {
                throw new InvalidOperationException(
                    $"No transition from {CurrentState} on trigger \"{trigger}\".");
            }
            TransitionTo(target);
        }

        /// <summary>
        /// Transition directly to <paramref name="target"/>. Validates that an
        /// edge (CurrentState → target) exists in the table (via any trigger) OR
        /// that the caller is performing an interrupt/restore (see
        /// <see cref="Interrupt"/> / <see cref="RestoreFromInterrupt"/>), then
        /// publishes <see cref="StateChanged"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="Interrupt"/> and <see cref="RestoreFromInterrupt"/> bypass
        /// table validation because interrupts are explicitly allowed from any
        /// state (PRD §10). Direct <see cref="TransitionTo"/> calls require a
        /// defined edge.
        /// </remarks>
        /// <param name="target">State to enter.</param>
        /// <param name="bypassValidation">
        /// Internal: when true (interrupt/restore path) skip table-edge check.
        /// </param>
        public void TransitionTo(FSMState target, bool bypassValidation = false)
        {
            if (!bypassValidation && !HasEdge(CurrentState, target))
            {
                throw new InvalidOperationException(
                    $"No edge from {CurrentState} to {target} in transition table.");
            }
            var old = CurrentState;
            if (old == target) return; // no-op self-transition
            CurrentState = target;
            StateChanged?.Invoke(old, target);
        }

        /// <summary>
        /// Interrupt: save the current state as <see cref="PreviousState"/> and
        /// transition to <paramref name="interruptState"/>. Interrupts may come
        /// from any state (PRD §10 INTERRUPTS block) so the table is not checked.
        /// </summary>
        public void Interrupt(FSMState interruptState)
        {
            PreviousState = CurrentState;
            TransitionTo(interruptState, bypassValidation: true);
        }

        /// <summary>
        /// Return to <see cref="PreviousState"/> (the state saved by
        /// <see cref="Interrupt"/>), or <see cref="FSMState.Idle"/> if no
        /// interrupt is active. Clears <see cref="PreviousState"/>.
        /// </summary>
        public void RestoreFromInterrupt()
        {
            var target = PreviousState ?? FSMState.Idle;
            PreviousState = null;
            TransitionTo(target, bypassValidation: true);
        }

        /// <summary>
        /// Reset to Idle and clear any active interrupt. Useful for tests and
        /// teardown.
        /// </summary>
        public void Reset()
        {
            PreviousState = null;
            if (CurrentState != FSMState.Idle)
            {
                var old = CurrentState;
                CurrentState = FSMState.Idle;
                StateChanged?.Invoke(old, FSMState.Idle);
            }
        }

        /// <summary>Enumerate all transitions in the table (for inspection/tests).</summary>
        public IEnumerable<FSMTransition> Transitions
            => _transitions.Select(kv => new FSMTransition(kv.Key.Item1, kv.Value, kv.Key.Item2));

        /// <summary>True if any edge from→to exists (any trigger).</summary>
        private bool HasEdge(FSMState from, FSMState to)
            => _transitions.Any(kv => kv.Key.Item1 == from && kv.Value == to);

        /// <summary>
        /// PRD §10 RULES: "Every playOnce state MUST declare a terminal transition
        /// — no dead ends." Throw if any playOnce state has zero outgoing edges.
        /// </summary>
        private void ValidateNoDeadEnds()
        {
            var deadEnds = PlayOnceStates
                .Where(s => !_transitions.Keys.Any(k => k.Item1 == s))
                .ToList();
            if (deadEnds.Count != 0)
            {
                throw new InvalidOperationException(
                    "PlayOnce states with no terminal transition (dead ends): " +
                    string.Join(", ", deadEnds));
            }
        }
    }
}