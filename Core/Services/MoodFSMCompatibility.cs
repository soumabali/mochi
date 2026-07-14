using System;
using System.Collections.Generic;
using MochiV2.Core.Models;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Mood-FSM compatibility table. Ensures the displayed mood never contradicts
    /// the active FSM animation state. PRD §6.3: mood is derived from needs but
    /// displayed mood must be compatible with current animation.
    /// </summary>
    public static class MoodFSMCompatibility
    {
        /// <summary>
        /// FSM states that FORCE a specific mood display, overriding MoodResolver.
        /// E.g., when Sleeping, always show "Sleeping" regardless of needs.
        /// </summary>
        private static readonly Dictionary<FSMState, string> ForcedMood = new()
        {
            { FSMState.Sleeping, "Sleeping" },
            { FSMState.WakeUp, "Tired" },
            { FSMState.Eating, "Content" },
            { FSMState.Drinking, "Content" },
            { FSMState.HungryStandard, "HungryStandard" },
            { FSMState.HungryCritical, "HungryCritical" },
            { FSMState.Playful, "happy" },
            { FSMState.HappyHop, "happy" },
            { FSMState.Angry, "Sad" },
            { FSMState.Surprised, "Content" },
        };

        /// <summary>
        /// Moods compatible with each FSM state (for validation).
        /// If a mood is NOT in the compatible set, it should be queued until
        /// the FSM transitions to a compatible state.
        /// </summary>
        private static readonly Dictionary<FSMState, HashSet<string>> CompatibleMoods = new()
        {
            { FSMState.Idle, new() { "Content", "happy", "hungry", "tired", "sad", "HungryStandard", "HungryCritical" } },
            { FSMState.WalkLeft, new() { "Content", "happy", "hungry", "tired" } },
            { FSMState.WalkRight, new() { "Content", "happy", "hungry", "tired" } },
            { FSMState.WalkForward, new() { "Content", "happy", "hungry", "tired" } },
            { FSMState.RunVar1, new() { "Content", "happy" } },
            { FSMState.RunVar2, new() { "Content", "happy" } },
            { FSMState.JumpVar1, new() { "Content", "happy" } },
            { FSMState.JumpVar2, new() { "Content", "happy" } },
            { FSMState.Blink, new() { "Content", "happy", "tired" } },
            { FSMState.Sleeping, new() { "Sleeping", "tired" } },
            { FSMState.WakeUp, new() { "tired", "Content" } },
            { FSMState.Eating, new() { "Content", "happy" } },
            { FSMState.Drinking, new() { "Content", "happy" } },
            { FSMState.Playful, new() { "happy", "Content" } },
            { FSMState.HappyHop, new() { "happy", "Content" } },
            { FSMState.Angry, new() { "sad", "Content" } },
            { FSMState.Surprised, new() { "Content" } },
            { FSMState.Drag, new() { "Content", "sad" } },
            { FSMState.Fall, new() { "Content", "sad" } },
            { FSMState.ScratchLeft, new() { "Content", "happy" } },
            { FSMState.ScratchRight, new() { "Content", "happy" } },
            { FSMState.MeowLeft, new() { "hungry", "Content", "happy" } },
            { FSMState.MeowRight, new() { "hungry", "Content", "happy" } },
            { FSMState.Stretching, new() { "Content", "tired", "happy" } },
            { FSMState.ClimbUp, new() { "Content", "happy" } },
            { FSMState.HungryStandard, new() { "HungryStandard", "hungry" } },
            { FSMState.HungryCritical, new() { "HungryCritical", "hungry" } },
        };

        /// <summary>
        /// Get the mood that should be DISPLAYED given the current FSM state and
        /// the mood resolved from needs. If the FSM state has a forced mood, use that.
        /// Otherwise, if the resolved mood is compatible with the FSM state, use it.
        /// If not compatible, fall back to a safe default.
        /// </summary>
        public static string GetDisplayedMood(FSMState fsmState, string resolvedMood)
        {
            // Forced mood always wins
            if (ForcedMood.TryGetValue(fsmState, out var forced))
                return forced;

            // Check compatibility
            if (CompatibleMoods.TryGetValue(fsmState, out var compatible))
            {
                if (compatible.Contains(resolvedMood))
                    return resolvedMood;
                // Fall back to a safe mood for this state
                if (compatible.Contains("Content"))
                    return "Content";
                return compatible.Count > 0 ? new List<string>(compatible)[0] : "Content";
            }

            return resolvedMood ?? "Content";
        }

        /// <summary>
        /// Check if a mood is compatible with an FSM state.
        /// </summary>
        public static bool IsCompatible(FSMState fsmState, string mood)
        {
            if (CompatibleMoods.TryGetValue(fsmState, out var compatible))
                return compatible.Contains(mood);
            return true; // Unknown states allow all moods
        }
    }
}
