using System;
using System.IO;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    public class ScenarioPlayerTests
    {
        private static FSM CreateFSM()
        {
            return FSMBuilder.CreateDefault();
        }

        [Fact]
        public void StartScenarioById_Returns_False_For_Unknown_Id()
        {
            var fsm = CreateFSM();
            var rng = new StandardRandom();
            var player = new ScenarioPlayer(fsm, rng);

            Assert.False(player.StartScenarioById("nonexistent"));
        }

        [Fact]
        public void Stop_Clears_Active_Scenario()
        {
            var fsm = CreateFSM();
            var rng = new StandardRandom();
            var player = new ScenarioPlayer(fsm, rng);

            player.Stop();
            Assert.False(player.IsActive);
            Assert.Equal(-1, player.CurrentStepIndex);
        }

        [Fact]
        public void Update_Without_Scenario_Returns_False()
        {
            var fsm = CreateFSM();
            var rng = new StandardRandom();
            var player = new ScenarioPlayer(fsm, rng);

            Assert.False(player.Update(100));
        }

        [Fact]
        public void StartRandomIdleScenario_No_Candidates_Does_Not_Crash()
        {
            var fsm = CreateFSM();
            var rng = new StandardRandom();
            var player = new ScenarioPlayer(fsm, rng);

            // No scenarios loaded - should not crash
            player.StartRandomIdleScenario();
        }
    }
}
