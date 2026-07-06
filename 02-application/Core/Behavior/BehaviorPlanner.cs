using System;
using System.Collections.Generic;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// PRD §6.4 — weighted-random behavior planner. Decides Mochi's next
    /// FSM state from the current state, mood, and a personality dial
    /// (0.0 = Calm, 1.0 = Chaotic). Supports chained sequences
    /// (walk → scratch → glance → walk, idle → blink → idle).
    /// </summary>
    /// <remarks>
    /// Design notes:
    /// - Uses injected <see cref="IRandom"/> for deterministic tests.
    /// - Triggers transitions by calling <see cref="FSM.Fire(string)"/>
    ///   for known triggers, and publishes <see cref="StateChangedEvent"/>
    ///   via <see cref="EventBus"/> so subscribers stay decoupled.
    /// - Weight tables are per-mood × personality-shifted. Chaotic
    ///   boosts run variants / jumps / playful; Calm boosts idle, blink,
    ///   and slow walks.
    /// </remarks>
    public sealed class BehaviorPlanner
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(BehaviorPlanner));

        // ───────────────────────── Dependencies ─────────────────────────

        private readonly FSM _fsm;
        private readonly EventBus _eventBus;
        private readonly IRandom _random;

        // ─────────────── Mood + personality config tables ────────────────

        /// <summary>
        /// Known mood names (PRD §6.4 / MoodResolver). The planner treats
        /// mood as a string key so it stays decoupled from the mood
        /// resolver implementation (T-013 not yet built).
        /// </summary>
        public const string MoodHappy = "happy";
        public const string MoodNeutral = "neutral";
        public const string MoodHungry = "hungry";
        public const string MoodTired = "tired";
        public const string MoodSad = "sad";

        /// <summary>
        /// Canonical set of states the planner may choose from when
        /// leaving <see cref="FSMState.Idle"/>. Matches the trigger
        /// table in <see cref="FSMBuilder.CreateDefault"/>.
        /// </summary>
        private static readonly FSMState[] IdleCandidateStates =
        {
            FSMState.Idle,          // stay put
            FSMState.WalkLeft,
            FSMState.WalkRight,
            FSMState.WalkForward,
            FSMState.RunVar1,
            FSMState.RunVar2,
            FSMState.JumpVar1,
            FSMState.JumpVar2,
            FSMState.ScratchLeft,
            FSMState.ScratchRight,
            FSMState.MeowLeft,
            FSMState.MeowRight,
            FSMState.Blink,
            FSMState.Playful,
        };

        /// <summary>Trigger string for each candidate state.</summary>
        private static readonly Dictionary<FSMState, string> StateTriggers = new()
        {
            { FSMState.Idle,        "stop" },          // no-op (stay idle)
            { FSMState.WalkLeft,    "walk_left" },
            { FSMState.WalkRight,   "walk_right" },
            { FSMState.WalkForward, "walk_forward" },
            { FSMState.RunVar1,     "run_1" },
            { FSMState.RunVar2,     "run_2" },
            { FSMState.JumpVar1,    "jump_1" },
            { FSMState.JumpVar2,    "jump_2" },
            { FSMState.ScratchLeft, "scratch_left" },
            { FSMState.ScratchRight,"scratch_right" },
            { FSMState.MeowLeft,    "meow_left" },
            { FSMState.MeowRight,   "meow_right" },
            { FSMState.Blink,       "blink" },
            { FSMState.Playful,     "playful" },
        };

        // ─────────────────────── Chained sequences ───────────────────────

        /// <summary>
        /// Chained sequences keyed by their starting state. When the
        /// planner is invoked and the FSM just entered the key state,
        /// the next state in the chain is returned (deterministically,
        /// before random selection). PRD §6.4 examples:
        /// walk → scratch → glance → walk, idle → blink → idle.
        /// </summary>
        private static readonly Dictionary<FSMState, FSMState[]> Chains = new()
        {
            // idle → blink → idle  (fidget chain)
            { FSMState.Blink, new[] { FSMState.Idle } },

            // walk → scratch → glance(meow) → walk
            // WalkLeft / WalkRight both chain into a scratch, then a
            // meow (glance stand-in), then back to a walk.
            { FSMState.WalkLeft,  new[] { FSMState.ScratchLeft, FSMState.MeowLeft,  FSMState.WalkRight } },
            { FSMState.WalkRight, new[] { FSMState.ScratchRight, FSMState.MeowRight, FSMState.WalkLeft } },

            // run → jump → idle (chaotic burst)
            { FSMState.RunVar1, new[] { FSMState.JumpVar1, FSMState.Idle } },
            { FSMState.RunVar2, new[] { FSMState.JumpVar2, FSMState.Idle } },

            // scratch → idle (return after fidget)
            { FSMState.ScratchLeft,  new[] { FSMState.Idle } },
            { FSMState.ScratchRight, new[] { FSMState.Idle } },

            // meow → idle
            { FSMState.MeowLeft,  new[] { FSMState.Idle } },
            { FSMState.MeowRight, new[] { FSMState.Idle } },

            // jump → idle
            { FSMState.JumpVar1, new[] { FSMState.Idle } },
            { FSMState.JumpVar2, new[] { FSMState.Idle } },

            // playful → idle (settle down)
            { FSMState.Playful, new[] { FSMState.Idle } },
        };

        // ────────────────────── Per-chain progress ───────────────────────

        private FSMState? _chainAnchor;        // state that started the chain
        private int _chainIndex;               // next step within the chain

        // ─────────────────────── Base weight table ───────────────────────
        //
        // Weights are positive doubles; only relative magnitude matters
        // (normalised during selection). Indexed [mood][state].
        // Personality shifts are applied on top at selection time.

        private static readonly Dictionary<string, Dictionary<FSMState, double>> MoodWeights = new()
        {
            [MoodNeutral] = new()
            {
                { FSMState.Idle,        30.0 },
                { FSMState.WalkLeft,    12.0 },
                { FSMState.WalkRight,   12.0 },
                { FSMState.WalkForward,  6.0 },
                { FSMState.RunVar1,      4.0 },
                { FSMState.RunVar2,      4.0 },
                { FSMState.JumpVar1,     3.0 },
                { FSMState.JumpVar2,     3.0 },
                { FSMState.ScratchLeft,  4.0 },
                { FSMState.ScratchRight, 4.0 },
                { FSMState.MeowLeft,     3.0 },
                { FSMState.MeowRight,    3.0 },
                { FSMState.Blink,       10.0 },
                { FSMState.Playful,      2.0 },
            },

            [MoodHappy] = new()
            {
                { FSMState.Idle,        20.0 },
                { FSMState.WalkLeft,    14.0 },
                { FSMState.WalkRight,   14.0 },
                { FSMState.WalkForward,  8.0 },
                { FSMState.RunVar1,      8.0 },
                { FSMState.RunVar2,      8.0 },
                { FSMState.JumpVar1,     6.0 },
                { FSMState.JumpVar2,     6.0 },
                { FSMState.ScratchLeft,  4.0 },
                { FSMState.ScratchRight, 4.0 },
                { FSMState.MeowLeft,     5.0 },
                { FSMState.MeowRight,    5.0 },
                { FSMState.Blink,        8.0 },
                { FSMState.Playful,     10.0 },  // happy → playful boost
            },

            [MoodTired] = new()
            {
                { FSMState.Idle,        55.0 },  // lots of resting
                { FSMState.WalkLeft,     4.0 },
                { FSMState.WalkRight,    4.0 },
                { FSMState.WalkForward,  2.0 },
                { FSMState.RunVar1,      1.0 },
                { FSMState.RunVar2,      1.0 },
                { FSMState.JumpVar1,     0.5 },
                { FSMState.JumpVar2,     0.5 },
                { FSMState.ScratchLeft,  2.0 },
                { FSMState.ScratchRight, 2.0 },
                { FSMState.MeowLeft,     1.0 },
                { FSMState.MeowRight,    1.0 },
                { FSMState.Blink,       12.0 },
                { FSMState.Playful,      1.0 },
            },

            [MoodHungry] = new()
            {
                { FSMState.Idle,        25.0 },
                { FSMState.WalkLeft,     8.0 },
                { FSMState.WalkRight,    8.0 },
                { FSMState.WalkForward,  4.0 },
                { FSMState.RunVar1,      2.0 },
                { FSMState.RunVar2,      2.0 },
                { FSMState.JumpVar1,     1.0 },
                { FSMState.JumpVar2,     1.0 },
                { FSMState.ScratchLeft,  3.0 },
                { FSMState.ScratchRight, 3.0 },
                { FSMState.MeowLeft,    12.0 },  // meow (beg) boost
                { FSMState.MeowRight,   12.0 },
                { FSMState.Blink,        6.0 },
                { FSMState.Playful,      2.0 },
            },

            [MoodSad] = new()
            {
                { FSMState.Idle,        45.0 },
                { FSMState.WalkLeft,     6.0 },
                { FSMState.WalkRight,    6.0 },
                { FSMState.WalkForward,  3.0 },
                { FSMState.RunVar1,      1.0 },
                { FSMState.RunVar2,      1.0 },
                { FSMState.JumpVar1,     0.5 },
                { FSMState.JumpVar2,     0.5 },
                { FSMState.ScratchLeft,  2.0 },
                { FSMState.ScratchRight, 2.0 },
                { FSMState.MeowLeft,     4.0 },
                { FSMState.MeowRight,    4.0 },
                { FSMState.Blink,        8.0 },
                { FSMState.Playful,      1.0 },
            },
        };

        /// <summary>
        /// Personality shift coefficients. For each state we store a
        /// (calmBias, chaoticBias) pair. At selection time the effective
        /// weight is <c>base * lerp(calmBias, chaoticBias, personality)</c>.
        /// Calm (0.0) favours idle/blink/walk; Chaotic (1.0) favours
        /// run/jump/playful.
        /// </summary>
        private static readonly Dictionary<FSMState, (double Calm, double Chaotic)> PersonalityShift = new()
        {
            { FSMState.Idle,        (1.4, 0.5) },   // calm → more idle
            { FSMState.WalkLeft,    (1.2, 0.7) },   // calm → slow walks
            { FSMState.WalkRight,   (1.2, 0.7) },
            { FSMState.WalkForward, (1.0, 0.8) },
            { FSMState.RunVar1,     (0.4, 1.8) },   // chaotic → runs
            { FSMState.RunVar2,     (0.4, 1.8) },
            { FSMState.JumpVar1,    (0.3, 2.0) },   // chaotic → jumps
            { FSMState.JumpVar2,    (0.3, 2.0) },
            { FSMState.ScratchLeft, (0.9, 1.1) },
            { FSMState.ScratchRight,(0.9, 1.1) },
            { FSMState.MeowLeft,    (0.8, 1.2) },
            { FSMState.MeowRight,   (0.8, 1.2) },
            { FSMState.Blink,       (1.5, 0.6) },   // calm → blink
            { FSMState.Playful,     (0.2, 2.2) },   // chaotic → playful
        };

        // ─────────────────────────── Ctor ────────────────────────────────

        /// <summary>
        /// Create the behavior planner.
        /// </summary>
        /// <param name="fsm">FSM to drive (transitions fired via triggers).</param>
        /// <param name="eventBus">EventBus for <see cref="StateChangedEvent"/>.</param>
        /// <param name="random">Injected RNG for testable weighted selection.</param>
        /// <exception cref="ArgumentNullException">Any dependency null.</exception>
        public BehaviorPlanner(FSM fsm, EventBus eventBus, IRandom random)
        {
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        // ─────────────────────── Public planning API ─────────────────────

        /// <summary>
        /// Decide and **apply** Mochi's next action, then return the
        /// resulting FSM state. PRD §6.4.
        /// </summary>
        /// <param name="currentState">Current FSM state (usually
        /// <see cref="FSM.CurrentState"/>).</param>
        /// <param name="mood">Mood name (see <c>Mood*</c> constants).
        /// Unknown moods fall back to <see cref="MoodNeutral"/>.</param>
        /// <param name="personality">Calm↔Chaotic dial, 0.0 = calm,
        /// 1.0 = chaotic. Clamped to [0,1].</param>
        /// <returns>The new FSM state after the transition (may equal
        /// <paramref name="currentState"/> for no-op / chain stay).</returns>
        public FSMState PlanNextAction(FSMState currentState, string mood, double personality)
        {
            if (string.IsNullOrWhiteSpace(mood))
                mood = MoodNeutral;
            personality = Math.Clamp(personality, 0.0, 1.0);

            // 1. Continue an active chain if applicable.
            var next = ResolveChainStep(currentState);
            if (next.HasValue)
            {
                Logger.Debug("Chain step: {Current} → {Next} (chain #{Idx})",
                    currentState, next.Value, _chainIndex);
                ApplyTransition(currentState, next.Value);
                return _fsm.CurrentState;
            }

            // 2. Weighted random selection (only when we're at Idle or
            //    a state without an active chain — i.e. a decision point).
            var chosen = SelectWeightedState(currentState, mood, personality);
            Logger.Debug("Planner chose {Chosen} (mood={Mood}, p={P:F2}, from={From})",
                chosen, mood, personality, currentState);

            // Start a chain if the chosen state is a chain anchor and
            // we're coming from Idle (so the chain reads as a sequence).
            if (currentState == FSMState.Idle && Chains.TryGetValue(chosen, out var chain) && chain.Length > 0)
            {
                _chainAnchor = chosen;
                _chainIndex = 0;
            }

            ApplyTransition(currentState, chosen);
            return _fsm.CurrentState;
        }

        // ───────────────────────── Chain handling ────────────────────────

        /// <summary>
        /// If <paramref name="currentState"/> is the just-entered step of
        /// an active chain, return the next state in the chain (and
        /// advance the index). Returns <c>null</c> when no chain is
        /// active or the chain has been exhausted.
        /// </summary>
        private FSMState? ResolveChainStep(FSMState currentState)
        {
            if (_chainAnchor is null)
                return null;

            if (!Chains.TryGetValue(_chainAnchor.Value, out var chain))
            {
                // Stale anchor — clear it.
                _chainAnchor = null;
                _chainIndex = 0;
                return null;
            }

            // The first chain entry applies after the anchor state has
            // been *completed*. We detect completion by the FSM having
            // transitioned back to Idle (playOnce states return to Idle
            // via the "done" trigger, loops via "stop"). So: if we're
            // Idle and mid-chain, advance.
            if (currentState != FSMState.Idle)
                return null; // still playing the chain step; wait.

            if (_chainIndex >= chain.Length)
            {
                // Chain exhausted.
                _chainAnchor = null;
                _chainIndex = 0;
                return null;
            }

            var next = chain[_chainIndex];
            _chainIndex++;

            if (_chainIndex >= chain.Length)
            {
                // This was the last step — clear after returning it.
                _chainAnchor = null;
                _chainIndex = 0;
            }

            return next;
        }

        // ─────────────────── Weighted random selection ───────────────────

        /// <summary>
        /// Pick the next state using mood × personality-weighted random
        /// selection from the candidate pool. When <paramref name="currentState"/>
        /// is not Idle, the planner still selects (so e.g. a loop state
        /// can decide to stop), but the candidate pool is restricted to
        /// {Idle, currentState} to avoid illegal jumps.
        /// </summary>
        private FSMState SelectWeightedState(FSMState currentState, string mood, double personality)
        {
            // Non-Idle, non-chain states: only allow staying or returning
            // to Idle (the FSM transition table enforces edges anyway).
            IReadOnlyList<FSMState> pool;
            if (currentState != FSMState.Idle)
            {
                pool = new[] { FSMState.Idle, currentState };
            }
            else
            {
                pool = IdleCandidateStates;
            }

            if (!MoodWeights.TryGetValue(mood, out var moodTable))
                moodTable = MoodWeights[MoodNeutral];

            // Build effective weights: base × personality lerp.
            var weights = new double[pool.Count];
            for (var i = 0; i < pool.Count; i++)
            {
                var s = pool[i];
                if (!moodTable.TryGetValue(s, out var baseW))
                    baseW = 1.0;
                if (!PersonalityShift.TryGetValue(s, out var shift))
                    shift = (1.0, 1.0);
                var mult = Lerp(shift.Calm, shift.Chaotic, personality);
                weights[i] = Math.Max(baseW * mult, 0.0);
            }

            // Normalise + sample.
            var total = 0.0;
            for (var i = 0; i < weights.Length; i++)
                total += weights[i];

            if (total <= 0.0)
            {
                // Degenerate — fall back to Idle.
                return FSMState.Idle;
            }

            var roll = _random.NextDouble() * total;
            var acc = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (roll < acc)
                    return pool[i];
            }

            // Floating-point tail — return last.
            return pool[^1];
        }

        // ─────────────────────── Transition execution ────────────────────

        /// <summary>
        /// Drive the FSM to <paramref name="target"/> and publish
        /// <see cref="StateChangedEvent"/>. Uses the canonical trigger
        /// when available (so the FSM's transition table is exercised);
        /// falls back to <see cref="FSM.TransitionTo"/> with validation
        /// bypass for chain steps whose edge may be recorded under a
        /// different trigger.
        /// </summary>
        private void ApplyTransition(FSMState current, FSMState target)
        {
            if (current == target)
                return; // no-op

            var oldState = _fsm.CurrentState;

            if (StateTriggers.TryGetValue(target, out var trigger) &&
                _fsm.CanTransition(_fsm.CurrentState, trigger))
            {
                _fsm.Fire(trigger);
            }
            else
            {
                // Chain steps (e.g. Idle → ScratchLeft) re-enter via the
                // Idle trigger table. If the FSM is at Idle this works;
                // otherwise we bypass validation (chains are planner-
                // authored and trusted).
                try
                {
                    _fsm.TransitionTo(target, bypassValidation: true);
                }
                catch (InvalidOperationException ex)
                {
                    // Fail loud per CONSTITUTION rule 5 — but don't crash
                    // the pet loop; log and stay put.
                    Logger.Warning(ex,
                        "Planner transition {From}→{To} rejected; staying at {Current}",
                        oldState, target, oldState);
                    return;
                }
            }

            var newState = _fsm.CurrentState;
            if (newState != oldState)
            {
                _eventBus.Publish(new StateChangedEvent(oldState, newState));
            }
        }

        // ───────────────────────────── Utils ─────────────────────────────

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}