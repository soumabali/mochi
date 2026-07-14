using System;
using MochiV2.Core.Models;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// Tests for MoodFSMCompatibility: ensures displayed mood never contradicts active FSM state.
    /// </summary>
    public class MoodFSMCompatibilityTests
    {
        [Fact]
        public void Sleeping_Forces_Sleeping_Mood_Regardless_Of_Resolved()
        {
            var mood = MoodFSMCompatibility.GetDisplayedMood(FSMState.Sleeping, "happy");
            Assert.Equal("Sleeping", mood);
        }

        [Fact]
        public void Eating_Forces_Content_Mood()
        {
            var mood = MoodFSMCompatibility.GetDisplayedMood(FSMState.Eating, "hungry");
            Assert.Equal("Content", mood);
        }

        [Fact]
        public void Playful_Forces_Happy_Mood()
        {
            var mood = MoodFSMCompatibility.GetDisplayedMood(FSMState.Playful, "sad");
            Assert.Equal("happy", mood);
        }

        [Fact]
        public void Idle_Allows_All_Moods()
        {
            Assert.Equal("Content", MoodFSMCompatibility.GetDisplayedMood(FSMState.Idle, "Content"));
            Assert.Equal("happy", MoodFSMCompatibility.GetDisplayedMood(FSMState.Idle, "happy"));
            Assert.Equal("hungry", MoodFSMCompatibility.GetDisplayedMood(FSMState.Idle, "hungry"));
        }

        [Fact]
        public void Run_Does_Not_Allow_Sad_Mood()
        {
            // Sad is not compatible with running — should fall back to Content
            var mood = MoodFSMCompatibility.GetDisplayedMood(FSMState.RunVar1, "sad");
            Assert.Equal("Content", mood);
        }

        [Fact]
        public void Walking_Allows_Content_And_Happy()
        {
            Assert.Equal("Content", MoodFSMCompatibility.GetDisplayedMood(FSMState.WalkLeft, "Content"));
            Assert.Equal("happy", MoodFSMCompatibility.GetDisplayedMood(FSMState.WalkLeft, "happy"));
        }

        [Fact]
        public void Walking_Rejects_Sad_FallsBack_To_Content()
        {
            var mood = MoodFSMCompatibility.GetDisplayedMood(FSMState.WalkLeft, "sad");
            Assert.Equal("Content", mood);
        }

        [Fact]
        public void IsCompatible_Returns_True_For_Valid_Pairs()
        {
            Assert.True(MoodFSMCompatibility.IsCompatible(FSMState.Idle, "Content"));
            Assert.True(MoodFSMCompatibility.IsCompatible(FSMState.Sleeping, "Sleeping"));
        }

        [Fact]
        public void IsCompatible_Returns_False_For_Invalid_Pairs()
        {
            Assert.False(MoodFSMCompatibility.IsCompatible(FSMState.RunVar1, "sad"));
            Assert.False(MoodFSMCompatibility.IsCompatible(FSMState.Sleeping, "happy"));
        }

        [Fact]
        public void HungryStandard_Forces_HungryStandard_Mood()
        {
            var mood = MoodFSMCompatibility.GetDisplayedMood(FSMState.HungryStandard, "Content");
            Assert.Equal("HungryStandard", mood);
        }
    }
}
