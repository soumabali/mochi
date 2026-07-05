using System;
using System.Collections.Generic;
using MochiV2.Core.Models;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Fluent builder for the FSM transition table (PRD §10). Produces a
    /// validated <see cref="FSM"/>. Use <see cref="CreateDefault"/> to get the
    /// canonical Mochi FSM; use the fluent API for tests/custom setups.
    /// </summary>
    public sealed class FSMBuilder
    {
        private readonly Dictionary<(FSMState, string), FSMState> _transitions = new();

        /// <summary>Add a single transition edge.</summary>
        public FSMBuilder AddTransition(FSMState from, string trigger, FSMState to)
        {
            if (string.IsNullOrWhiteSpace(trigger))
                throw new ArgumentException("Trigger must be non-empty.", nameof(trigger));
            _transitions[(from, trigger)] = to;
            return this;
        }

        /// <summary>
        /// Declare an interrupt edge. Interrupts bypass the from-state check at
        /// runtime (see <see cref="FSM.Interrupt"/>), but recording them in the
        /// table lets <see cref="FSM.CanTransition"/> answer queries and keeps
        /// the model self-documenting. The <paramref name="trigger"/> is the
        /// interrupt name (e.g. "cursor_near", "drag_start").
        /// </summary>
        public FSMBuilder AddInterrupt(string trigger, FSMState to)
        {
            if (string.IsNullOrWhiteSpace(trigger))
                throw new ArgumentException("Trigger must be non-empty.", nameof(trigger));
            // Interrupts are reachable from any state; record from Idle as a
            // canonical representative so the table carries the edge without
            // spamming 23× entries. Runtime Interrupt() bypasses validation.
            _transitions[(FSMState.Idle, trigger)] = to;
            return this;
        }

        /// <summary>Build and validate the FSM.</summary>
        public FSM Build() => new(_transitions);

        /// <summary>
        /// The canonical Mochi FSM transition table per PRD §10.
        /// Every playOnce state has a terminal "done"→Idle edge.
        /// Loops return to Idle on "stop". Interrupts return to Idle on "resume".
        /// </summary>
        public static FSM CreateDefault()
        {
            var b = new FSMBuilder();

            // --- Idle → behaviors (planner tick picks one) ---
            b.AddTransition(FSMState.Idle, "walk_left",      FSMState.WalkLeft);
            b.AddTransition(FSMState.Idle, "walk_right",     FSMState.WalkRight);
            b.AddTransition(FSMState.Idle, "walk_forward",   FSMState.WalkForward);
            b.AddTransition(FSMState.Idle, "run_1",          FSMState.RunVar1);
            b.AddTransition(FSMState.Idle, "run_2",          FSMState.RunVar2);
            b.AddTransition(FSMState.Idle, "jump_1",         FSMState.JumpVar1);
            b.AddTransition(FSMState.Idle, "jump_2",         FSMState.JumpVar2);
            b.AddTransition(FSMState.Idle, "sleep",          FSMState.Sleeping);
            b.AddTransition(FSMState.Idle, "playful",        FSMState.Playful);
            b.AddTransition(FSMState.Idle, "hungry_std",     FSMState.HungryStandard);
            b.AddTransition(FSMState.Idle, "hungry_crit",    FSMState.HungryCritical);
            b.AddTransition(FSMState.Idle, "scratch_left",   FSMState.ScratchLeft);
            b.AddTransition(FSMState.Idle, "scratch_right",  FSMState.ScratchRight);
            b.AddTransition(FSMState.Idle, "meow_left",      FSMState.MeowLeft);
            b.AddTransition(FSMState.Idle, "meow_right",     FSMState.MeowRight);
            b.AddTransition(FSMState.Idle, "blink",          FSMState.Blink);
            b.AddTransition(FSMState.Idle, "eat",            FSMState.Eating);
            b.AddTransition(FSMState.Idle, "wake_up",        FSMState.WakeUp);

            // --- Walking / running / forward: loops, return to Idle on "stop" ---
            foreach (var s in new[] { FSMState.WalkLeft, FSMState.WalkRight, FSMState.WalkForward,
                                      FSMState.RunVar1, FSMState.RunVar2 })
            {
                b.AddTransition(s, "stop", FSMState.Idle);
            }

            // --- PlayOnce states: terminal "done" → Idle (PRD §10: no dead ends) ---
            foreach (var s in FSM.PlayOnceStates)
            {
                b.AddTransition(s, "done", FSMState.Idle);
            }

            // --- Sleeping: wake → WakeUp (reversed yawn) → done → Idle ---
            b.AddTransition(FSMState.Sleeping, "wake",  FSMState.WakeUp);
            // WakeUp already has done→Idle from the PlayOnce loop above.

            // --- Hungry states: fed → Eating; done → Idle ---
            b.AddTransition(FSMState.HungryStandard,  "fed", FSMState.Eating);
            b.AddTransition(FSMState.HungryCritical,   "fed", FSMState.Eating);
            b.AddTransition(FSMState.HungryStandard,  "done", FSMState.Idle);
            b.AddTransition(FSMState.HungryCritical,   "done", FSMState.Idle);
            b.AddTransition(FSMState.Eating,           "done", FSMState.Idle);

            // --- Playful: loop, stop → Idle ---
            b.AddTransition(FSMState.Playful, "stop", FSMState.Idle);

            // --- Angry (drag reaction, loop): drag_end → Fall ---
            b.AddTransition(FSMState.Angry, "drag_end", FSMState.Fall);
            // Fall is playOnce → done → Idle (covered by PlayOnce loop).

            // --- Drag (interrupt, follows cursor): drag_end → Fall ---
            b.AddTransition(FSMState.Drag, "drag_end", FSMState.Fall);

            // --- Interrupt edges (canonical from Idle; runtime bypasses from-check) ---
            b.AddInterrupt("cursor_near",    FSMState.Surprised); // glance
            b.AddInterrupt("click_on_cat",   FSMState.MeowLeft);
            b.AddInterrupt("drag_start",     FSMState.Drag);
            b.AddInterrupt("energy_floor",   FSMState.Sleeping);
            b.AddInterrupt("fullscreen",     FSMState.Idle);      // hide handled outside FSM

            return b.Build();
        }
    }
}