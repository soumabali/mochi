using System;
using System.Collections.Generic;
using System.Linq;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-021: Additional FSM transition tests — valid/invalid transitions,
    /// terminal transitions for every PlayOnce state, interrupt/restore,
    /// PreviousState tracking, and builder validation.
    /// </summary>
    public class FSMExtendedTests
    {
        //------------------------------------------------------------------
        // Valid transitions
        //------------------------------------------------------------------

        [Fact]
        public void Transition_Idle_To_WalkRight_On_WalkRight_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_right");
            Assert.Equal(FSMState.WalkRight, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_WalkForward_On_WalkForward_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_forward");
            Assert.Equal(FSMState.WalkForward, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_RunVar1_On_Run1_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("run_1");
            Assert.Equal(FSMState.RunVar1, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_RunVar2_On_Run2_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("run_2");
            Assert.Equal(FSMState.RunVar2, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_JumpVar1_On_Jump1_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("jump_1");
            Assert.Equal(FSMState.JumpVar1, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_Sleeping_On_Sleep_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("sleep");
            Assert.Equal(FSMState.Sleeping, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_Playful_On_Playful_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("playful");
            Assert.Equal(FSMState.Playful, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_HungryCritical_On_HungryCrit_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("hungry_crit");
            Assert.Equal(FSMState.HungryCritical, fsm.CurrentState);
        }

        [Fact]
        public void Transition_Idle_To_Eating_On_Eat_Trigger()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("eat");
            Assert.Equal(FSMState.Eating, fsm.CurrentState);
        }

        [Fact]
        public void WalkForward_Stop_Returns_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_forward");
            fsm.Fire("stop");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void RunVar1_Stop_Returns_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("run_1");
            fsm.Fire("stop");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void Playful_Stop_Returns_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("playful");
            fsm.Fire("stop");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        //------------------------------------------------------------------
        // PlayOnce terminal transitions
        //------------------------------------------------------------------

        [Theory]
        [InlineData(FSMState.JumpVar1, "jump_1")]
        [InlineData(FSMState.JumpVar2, "jump_2")]
        [InlineData(FSMState.MeowLeft, "meow_left")]
        [InlineData(FSMState.MeowRight, "meow_right")]
        [InlineData(FSMState.ScratchLeft, "scratch_left")]
        [InlineData(FSMState.ScratchRight, "scratch_right")]
        [InlineData(FSMState.Blink, "blink")]
        [InlineData(FSMState.Surprised, null)]        // interrupt-only
        [InlineData(FSMState.Fall, null)]              // from Angry/Drag
        [InlineData(FSMState.WakeUp, "wake_up")]
        public void Every_PlayOnce_State_Can_Return_To_Idle_Via_Done(FSMState state, string? enterTrigger)
        {
            var fsm = FSMBuilder.CreateDefault();

            // Enter the state (via trigger or interrupt for interrupt-only states)
            if (enterTrigger != null)
                fsm.Fire(enterTrigger);
            else
                fsm.Interrupt(state);

            Assert.Equal(state, fsm.CurrentState);

            // "done" should always transition to Idle
            fsm.Fire("done");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void Sleeping_Wake_Goes_To_WakeUp_Then_Done_Goes_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("sleep");
            Assert.Equal(FSMState.Sleeping, fsm.CurrentState);

            fsm.Fire("wake");
            Assert.Equal(FSMState.WakeUp, fsm.CurrentState);

            fsm.Fire("done");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void HungryStandard_Fed_Goes_To_Eating_Then_Done_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("hungry_std");
            Assert.Equal(FSMState.HungryStandard, fsm.CurrentState);

            fsm.Fire("fed");
            Assert.Equal(FSMState.Eating, fsm.CurrentState);

            fsm.Fire("done");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void HungryCritical_Fed_Goes_To_Eating_Then_Done_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("hungry_crit");
            Assert.Equal(FSMState.HungryCritical, fsm.CurrentState);

            fsm.Fire("fed");
            Assert.Equal(FSMState.Eating, fsm.CurrentState);

            fsm.Fire("done");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void Angry_DragEnd_Goes_To_Fall_Then_Done_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            // Angry is reached via interrupt (drag_start → Drag, but Angry via direct interrupt)
            fsm.Interrupt(FSMState.Angry);
            Assert.Equal(FSMState.Angry, fsm.CurrentState);

            fsm.Fire("drag_end");
            Assert.Equal(FSMState.Fall, fsm.CurrentState);

            fsm.Fire("done");
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
        }

        //------------------------------------------------------------------
        // Invalid transitions
        //------------------------------------------------------------------

        [Fact]
        public void Fire_Stop_From_Idle_Throws()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Throws<InvalidOperationException>(() => fsm.Fire("stop"));
        }

        [Fact]
        public void Fire_Done_From_Idle_Throws()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Throws<InvalidOperationException>(() => fsm.Fire("done"));
        }

        [Fact]
        public void Fire_WalkLeft_From_WalkLeft_Throws()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");
            Assert.Throws<InvalidOperationException>(() => fsm.Fire("walk_left"));
        }

        [Fact]
        public void Fire_Fed_From_Idle_Throws()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Throws<InvalidOperationException>(() => fsm.Fire("fed"));
        }

        [Fact]
        public void CanTransition_False_For_Undefined_Edge()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.False(fsm.CanTransition(FSMState.Idle, "nonexistent"));
            Assert.False(fsm.CanTransition(FSMState.WalkLeft, "jump_1"));
        }

        [Fact]
        public void CanTransition_True_For_Interrupt_Edges()
        {
            var fsm = FSMBuilder.CreateDefault();
            // Interrupts are recorded from Idle
            Assert.True(fsm.CanTransition(FSMState.Idle, "cursor_near"));
            Assert.True(fsm.CanTransition(FSMState.Idle, "drag_start"));
        }

        //------------------------------------------------------------------
        // PreviousState tracking
        //------------------------------------------------------------------

        [Fact]
        public void PreviousState_Is_Null_Initially()
        {
            var fsm = FSMBuilder.CreateDefault();
            Assert.Null(fsm.PreviousState);
        }

        [Fact]
        public void PreviousState_Set_To_Prior_State_On_Interrupt()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");

            fsm.Interrupt(FSMState.Surprised);
            Assert.Equal(FSMState.WalkLeft, fsm.PreviousState);
        }

        [Fact]
        public void PreviousState_Cleared_After_Restore()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_right");
            fsm.Interrupt(FSMState.Blink);

            fsm.RestoreFromInterrupt();
            Assert.Null(fsm.PreviousState);
        }

        [Fact]
        public void PreviousState_Not_Set_On_Normal_Transition()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");
            Assert.Null(fsm.PreviousState); // normal transition doesn't set PreviousState
        }

        //------------------------------------------------------------------
        // Interrupt / RestoreFromInterrupt
        //------------------------------------------------------------------

        [Fact]
        public void Interrupt_From_PlayOnce_State_Restores_Correctly()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("jump_1");
            Assert.Equal(FSMState.JumpVar1, fsm.CurrentState);

            fsm.Interrupt(FSMState.Surprised);
            Assert.Equal(FSMState.Surprised, fsm.CurrentState);
            Assert.Equal(FSMState.JumpVar1, fsm.PreviousState);
            Assert.True(fsm.IsInterruptActive);

            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.JumpVar1, fsm.CurrentState);
            Assert.False(fsm.IsInterruptActive);
        }

        [Fact]
        public void Interrupt_From_Sleeping_Restores_To_Sleeping()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("sleep");
            fsm.Interrupt(FSMState.MeowLeft);
            Assert.Equal(FSMState.Sleeping, fsm.PreviousState);

            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.Sleeping, fsm.CurrentState);
        }

        [Fact]
        public void Double_Interrupt_Keeps_Only_Latest_PreviousState()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");

            fsm.Interrupt(FSMState.Surprised);
            Assert.Equal(FSMState.WalkLeft, fsm.PreviousState);

            fsm.Interrupt(FSMState.Blink);
            // Second interrupt overwrites PreviousState with current (Surprised)
            Assert.Equal(FSMState.Surprised, fsm.PreviousState);
        }

        [Fact]
        public void RestoreFromInterrupt_Without_Interrupt_Goes_To_Idle()
        {
            var fsm = FSMBuilder.CreateDefault();
            // No interrupt active, PreviousState is null → should go to Idle
            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.Idle, fsm.CurrentState);
            Assert.Null(fsm.PreviousState);
        }

        //------------------------------------------------------------------
        // StateChanged event
        //------------------------------------------------------------------

        [Fact]
        public void StateChanged_Fires_On_Interrupt()
        {
            var fsm = FSMBuilder.CreateDefault();
            FSMState? oldState = null, newState = null;
            fsm.StateChanged += (o, n) => { oldState = o; newState = n; };

            fsm.Interrupt(FSMState.Surprised);
            Assert.Equal(FSMState.Idle, oldState);
            Assert.Equal(FSMState.Surprised, newState);
        }

        [Fact]
        public void StateChanged_Fires_On_Restore()
        {
            var fsm = FSMBuilder.CreateDefault();
            fsm.Fire("walk_left");
            fsm.Interrupt(FSMState.Surprised);

            FSMState? oldState = null, newState = null;
            fsm.StateChanged += (o, n) => { oldState = o; newState = n; };

            fsm.RestoreFromInterrupt();
            Assert.Equal(FSMState.Surprised, oldState);
            Assert.Equal(FSMState.WalkLeft, newState);
        }

        [Fact]
        public void StateChanged_Does_Not_Fire_On_Self_Transition()
        {
            var fsm = FSMBuilder.CreateDefault();
            int fireCount = 0;
            fsm.StateChanged += (_, _) => fireCount++;

            // TransitionTo to same state is a no-op
            fsm.TransitionTo(FSMState.Idle, bypassValidation: true);
            Assert.Equal(0, fireCount);
        }

        //------------------------------------------------------------------
        // Transitions enumeration
        //------------------------------------------------------------------

        [Fact]
        public void Transitions_Contains_All_Default_Edges()
        {
            var fsm = FSMBuilder.CreateDefault();
            var transitions = fsm.Transitions.ToList();

            // Should have a substantial number of transitions
            Assert.True(transitions.Count > 20);

            // Spot check some edges
            Assert.Contains(transitions, t => t.From == FSMState.Idle && t.Trigger == "walk_left" && t.To == FSMState.WalkLeft);
            Assert.Contains(transitions, t => t.From == FSMState.WalkLeft && t.Trigger == "stop" && t.To == FSMState.Idle);
            Assert.Contains(transitions, t => t.From == FSMState.JumpVar1 && t.Trigger == "done" && t.To == FSMState.Idle);
        }

        //------------------------------------------------------------------
        // Builder validation
        //------------------------------------------------------------------

        [Fact]
        public void Builder_AddTransition_Null_Trigger_Throws()
        {
            var b = new FSMBuilder();
            Assert.Throws<ArgumentException>(() => b.AddTransition(FSMState.Idle, null!, FSMState.WalkLeft));
        }

        [Fact]
        public void Builder_AddTransition_Empty_Trigger_Throws()
        {
            var b = new FSMBuilder();
            Assert.Throws<ArgumentException>(() => b.AddTransition(FSMState.Idle, "", FSMState.WalkLeft));
        }

        [Fact]
        public void Builder_AddTransition_Whitespace_Trigger_Throws()
        {
            var b = new FSMBuilder();
            Assert.Throws<ArgumentException>(() => b.AddTransition(FSMState.Idle, "   ", FSMState.WalkLeft));
        }

        [Fact]
        public void Builder_AddInterrupt_Null_Trigger_Throws()
        {
            var b = new FSMBuilder();
            Assert.Throws<ArgumentException>(() => b.AddInterrupt(null!, FSMState.Surprised));
        }

        [Fact]
        public void Builder_Empty_Table_Throws_Due_To_PlayOnce_DeadEnds()
        {
            // Empty table has no terminal transitions for PlayOnce states → must throw
            var b = new FSMBuilder();
            Assert.Throws<InvalidOperationException>(() => b.Build());
        }

        [Fact]
        public void Constructor_Null_Transitions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FSM(null!));
        }
    }
}