using System;
using System.Linq;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-006: FSM core tests — transitions, interrupts, dead-end validation.
    /// </summary>
    public class FSMTests
    {
        // ------------------------------------------------------------------
        // Transition tests
        // ------------------------------------------------------------------

        [Fact]
        public void Transition_Idle_To_WalkLeft_On_WalkLeft_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Equal(FSMState.Idle, fsm.CurrentState);

            fsm.Fire("walk_left");

            Assert.Equal(FSMState.WalkLeft, fsm.CurrentState);
        }

        [Fact]
        public void CanTransition_Returns_True_For_Defined_Edge()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.True(fsm.CanTransition(FSMState.Idle, "walk_left"));
            Assert.True(fsm.CanTransition(FSMState.Idle, "blink"));
        }

        [Fact]
        public void Fire_Invalid_Trigger_Throws()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Throws<InvalidOperationException>(() => fsm.Fire("nonexistent_trigger"));
        }

        [Fact]
        public void TransitionTo_Without_Edge_Throws()
        {
            var fsm = FSMBuilder.CreateDefault();
            // Idle → Angry has no direct edge in the table (Angry is only
            // reachable via the drag_start interrupt path which bypasses
            // validation at runtime, and from Drag via drag_end).
            Assert.Throws<InvalidOperationException>(
                () => fsm.TransitionTo(FSMState.Angry));
        }

        [Fact]
        public void StateChanged_Event_Fires_On_Transition()
        {
            var fsm = FSMBuilder.CreateDefault();
            FSMState? old = null, newS = null;
            fsm.StateChanged += (o, n) => { old = o; newS = n; };

            fsm.Fire("walk_right");

            Assert.Equal(FSMState.Idle, old);
            Assert.Equal(FSMState.WalkRight, newS);
        }

        [Fact]
        public void WalkLeft_Stop_Returns_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");
            fsm.Fire("stop");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        // ------------------------------------------------------------------
        // Interrupt / restore tests
        // ------------------------------------------------------------------

        [Fact]
        public void Interrupt_Saves_Previous_State_And_Restore_Returns_To_It()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");
            Assert.Equal(FSMState.WalkLeft, fsm.CurrentState);

            fsm.Interrupt(FSMState.Surprised);
            Assert.Equal(FSMState.Surprised, fsm.CurrentState);
            Assert.Equal(FSMState.WalkLeft, fsm.PreviousState);
            Assert.True(fsm.IsInterruptActive);

            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.WalkLeft, fsm.CurrentState);
            Assert.Null(fsm.PreviousState);
            Assert.False(fsm.IsInterruptActive);
        }

        [Fact]
        public void RestoreFromInterrupt_Without_Interrupt_Goes_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_right");
            // No interrupt was triggered; restore should go to Idle (safe default).
            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void Interrupt_Can_Be_Nested_Only_One_Level()
        {
            // Second interrupt overwrites PreviousState — only one level of
            // restore is supported (PRD §10: "return to previous state OR Idle").
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_forward");

            fsm.Interrupt(FSMState.Surprised);
            Assert.Equal(FSMState.WalkForward, fsm.PreviousState);

            fsm.Interrupt(FSMState.MeowLeft);
            Assert.Equal(FSMState.Surprised, fsm.PreviousState);

            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.Surprised, fsm.CurrentState);
        }

        // ------------------------------------------------------------------
        // Dead-end validation tests (PRD §10 RULES)
        // ------------------------------------------------------------------

        [Fact]
        public void Every_PlayOnce_State_Has_Terminal_Transition_In_Default_FSM()
        {
            var fsm = FSMBuilder.CreateDefault();
            var transitions = fsm.Transitions.ToList();

            foreach (var playOnce in FSM.PlayOnceStates)
            {
                var hasOutgoing = transitions.Any(t => t.From == playOnce);
                Assert.True(hasOutgoing,
                    $"PlayOnce state {playOnce} has no terminal transition (dead end).");
            }
        }

        [Fact]
        public void Build_Throws_When_PlayOnce_State_Has_No_Outgoing_Edge()
        {
            // Build a table where JumpVar1 (playOnce) has no outgoing edge.
            var b = new FSMBuilder()
                .AddTransition(FSMState.Idle, "jump", FSMState.JumpVar1);
            // JumpVar1 has no "done" → dead end.
            Assert.Throws<InvalidOperationException>(() => b.Build());
        }

        [Fact]
        public void Build_Succeeds_When_All_PlayOnce_States_Have_Terminal()
        {
            // Build a minimal table where every PlayOnce state (per the global
            // FSM.PlayOnceStates set) has a terminal "done" → Idle edge.
            var b = new FSMBuilder();
            foreach (var s in FSM.PlayOnceStates)
            {
                b.AddTransition(FSMState.Idle, $"enter_{s}", s);
                b.AddTransition(s, "done", FSMState.Idle);
            }
            var fsm = b.Build(); // should not throw
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        // ------------------------------------------------------------------
        // Default FSM sanity
        // ------------------------------------------------------------------

        [Fact]
        public void Default_FSM_Starts_In_Idle_With_No_Interrupt()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
            Assert.False(fsm.IsInterruptActive);
        }

        [Fact]
        public void Reset_Returns_To_Idle_And_Clears_Interrupt()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");
            fsm.Interrupt(FSMState.Surprised);
            fsm.Reset();
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
            Assert.False(fsm.IsInterruptActive);
        }
    }
}