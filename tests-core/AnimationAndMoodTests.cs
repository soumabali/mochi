using System;
using System.Collections.Generic;
using System.IO;
using MochiV2.Core.Animation;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-021: Animation playback modes, mood resolution, and AnimationManager tests.
    /// </summary>
    public class AnimationAndMoodTests
    {
        //------------------------------------------------------------------
        // Test fakes
        //------------------------------------------------------------------

        private sealed class FakeTimeProvider : ITimeProvider
        {
            public double Now { get; set; }
            public double GetElapsedSeconds() => Now;
        }

        /// <summary>
        /// Creates a list of N fake frame paths for use with AnimationController.
        /// </summary>
        private static List<string> MakeFrames(int count)
        {
            var frames = new List<string>(count);
            for (int i = 0; i < count; i++)
                frames.Add($"frame_{i}.png");
            return frames;
        }

        //------------------------------------------------------------------
        // 1. Animation playback modes (AnimationController)
        //------------------------------------------------------------------

        [Fact]
        public void HoldFirstFrame_StaysAtFrame0_AfterUpdate()
        {
            var ctrl = new AnimationController("test", SpriteMode.HoldFirstFrame, MakeFrames(5));
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // Update with a large delta — should not advance.
            ctrl.Update(500);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);
        }

        [Fact]
        public void PlayOnce_AdvancesFrames_AndFinishesAtEnd()
        {
            var ctrl = new AnimationController("test", SpriteMode.PlayOnce, MakeFrames(5));
            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);

            // Fps=10 → 100ms per frame. 100ms advances one frame.
            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(3, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(4, ctrl.CurrentFrameIndex);

            // Next update should finish (already at last frame).
            ctrl.Update(100);
            Assert.True(ctrl.IsFinished);
            Assert.Equal(4, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void Loop_WrapsAroundToFrame0()
        {
            var ctrl = new AnimationController("test", SpriteMode.Loop, MakeFrames(5));
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // Advance to last frame.
            ctrl.Update(400);
            Assert.Equal(4, ctrl.CurrentFrameIndex);

            // Next advance should wrap to 0.
            ctrl.Update(100);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);
        }

        [Fact]
        public void PlayOnceReversed_PlaysBackwardFromLastFrameTo0()
        {
            var frames = MakeFrames(5);
            var ctrl = new AnimationController("test", SpriteMode.PlayOnceReversed, frames);

            // Reset for reversed mode starts at last frame.
            Assert.Equal(4, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);

            ctrl.Update(100);
            Assert.Equal(3, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // Next update should finish.
            ctrl.Update(100);
            Assert.True(ctrl.IsFinished);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void PlayOnceThenHoldLast_PlaysForwardThenHoldsLastFrame()
        {
            var ctrl = new AnimationController("test", SpriteMode.PlayOnceThenHoldLast, MakeFrames(5));
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // Advance through all frames.
            ctrl.Update(400);
            Assert.Equal(4, ctrl.CurrentFrameIndex);

            // Next update should mark finished but hold at last frame.
            ctrl.Update(100);
            Assert.True(ctrl.IsFinished);
            Assert.Equal(4, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void SpeedMultiplier_2_MakesAnimationAdvanceTwiceAsFast()
        {
            var ctrl = new AnimationController("test", SpriteMode.PlayOnce, MakeFrames(5), 2.0);
            // Fps=10, speed=2.0 → interval = 1000/10/2.0 = 50ms per frame.

            // 100ms should advance 2 frames (instead of 1 at speed 1.0).
            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            // Another 100ms → frame 4.
            ctrl.Update(100);
            Assert.Equal(4, ctrl.CurrentFrameIndex);

            // Another 50ms → finished.
            ctrl.Update(50);
            Assert.True(ctrl.IsFinished);
        }

        //------------------------------------------------------------------
        // 2. Mood resolution (MoodResolver / ResolveMood)
        //------------------------------------------------------------------

        [Fact]
        public void ResolveMood_FoodBelow20_ReturnsHungryCritical()
        {
            Assert.Equal(MoodResolver.MoodHungryCritical,
                MoodResolver.ResolveMood(19, 80, 80));
        }

        [Fact]
        public void ResolveMood_FoodBelow40_ReturnsHungryStandard()
        {
            Assert.Equal(MoodResolver.MoodHungryStandard,
                MoodResolver.ResolveMood(39, 80, 80));
        }

        [Fact]
        public void ResolveMood_EnergyBelow20_ReturnsTired()
        {
            Assert.Equal(MoodResolver.MoodTired,
                MoodResolver.ResolveMood(80, 19, 80));
        }

        [Fact]
        public void ResolveMood_HappinessBelow30_ReturnsSad()
        {
            Assert.Equal(MoodResolver.MoodSad,
                MoodResolver.ResolveMood(80, 80, 29));
        }

        [Fact]
        public void ResolveMood_AllAboveThreshold_ReturnsContent()
        {
            Assert.Equal(MoodResolver.MoodHappy,
                MoodResolver.ResolveMood(80, 80, 80));
        }

        [Fact]
        public void MoodResolver_Hysteresis_PreventsChangeWithin60Seconds()
        {
            var time = new FakeTimeProvider { Now = 0 };
            var bus = new EventBus();
            using var resolver = new MoodResolver(time, bus);

            // Force an initial mood to Content.
            resolver.Recalculate(50, 25, 50);
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);

            // Track mood change events.
            MoodChangedEvent? received = null;
            bus.Subscribe<MoodChangedEvent>(e => received = e);

            // Now drop food below 40 — candidate is HungryStandard.
            // Publish a NeedsTickEvent so the resolver processes it.
            time.Now = 10; // Only 10s since last change.
            bus.Publish(new NeedsTickEvent(30, 80, 80));

            // Within hysteresis window — mood should NOT change.
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
            Assert.Null(received);

            // Tick before hysteresis expires — still no change.
            time.Now = 50;
            resolver.Tick();
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
            Assert.Null(received);

            // After 60s hysteresis — Tick should apply pending change.
            time.Now = 61;
            resolver.Tick();
            Assert.Equal(MoodResolver.MoodHungryStandard, resolver.CurrentMood);
            Assert.NotNull(received);
            Assert.Equal(MoodResolver.MoodContent, received!.OldMood);
            Assert.Equal(MoodResolver.MoodHungryStandard, received.NewMood);
        }

        //------------------------------------------------------------------
        // 3. AnimationManager
        //------------------------------------------------------------------

        /// <summary>
        /// Creates a temp directory with PNG files for AssetManifestLoader
        /// to enumerate, and returns the manifest + base path.
        /// </summary>
        private static (AssetManifest manifest, string basePath, string tempDir)
            MakeManifestWithFrames(string stateName, int frameCount, SpriteMode mode)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "mochi_test_" + Guid.NewGuid().ToString("N"));
            var folder = Path.Combine(tempDir, stateName);
            Directory.CreateDirectory(folder);
            for (int i = 0; i < frameCount; i++)
                File.WriteAllBytes(Path.Combine(folder, $"frame_{i}.png"), new byte[] { 0x89, 0x50 });

            var manifest = new AssetManifest();
            manifest.Sprites[stateName] = new SpriteEntry
            {
                Folder = stateName,
                Mode = mode,
                SpeedMultiplier = 1.0
            };

            // Always include an Idle entry so AnimationManager can auto-transition
            // back to Idle after a PlayOnce finishes.
            var idleFolder = Path.Combine(tempDir, nameof(FSMState.Idle));
            Directory.CreateDirectory(idleFolder);
            for (int i = 0; i < 2; i++)
                File.WriteAllBytes(Path.Combine(idleFolder, $"idle_{i}.png"), new byte[] { 0x89, 0x50 });
            manifest.Sprites[nameof(FSMState.Idle)] = new SpriteEntry
            {
                Folder = nameof(FSMState.Idle),
                Mode = SpriteMode.HoldFirstFrame,
                SpeedMultiplier = 1.0
            };

            return (manifest, tempDir, tempDir);
        }

        [Fact]
        public void AnimationManager_TransitionTo_CreatesControllerForNewState()
        {
            var (manifest, basePath, tempDir) =
                MakeManifestWithFrames(nameof(FSMState.Blink), 5, SpriteMode.PlayOnce);
            try
            {
                var loader = new AssetManifestLoader();
                var mgr = new AnimationManager(loader);

                mgr.TransitionTo(FSMState.Blink, manifest, basePath);

                Assert.Equal(FSMState.Blink, mgr.ActiveState);
                Assert.NotNull(mgr.ActiveController);
                Assert.Equal(5, mgr.ActiveController!.TotalFrames);
                Assert.True(mgr.Cache.ContainsKey(FSMState.Blink));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AnimationManager_TransitionTo_SameState_ReusesCachedController()
        {
            var (manifest, basePath, tempDir) =
                MakeManifestWithFrames(nameof(FSMState.Blink), 5, SpriteMode.PlayOnce);
            try
            {
                var loader = new AssetManifestLoader();
                var mgr = new AnimationManager(loader);

                mgr.TransitionTo(FSMState.Blink, manifest, basePath);
                var firstController = mgr.ActiveController;
                Assert.NotNull(firstController);

                // Advance the animation a bit.
                firstController!.Update(200);
                Assert.Equal(2, firstController.CurrentFrameIndex);

                // Transition to the same state again — should reuse cached controller
                // (reset back to frame 0).
                mgr.TransitionTo(FSMState.Blink, manifest, basePath);

                Assert.Same(firstController, mgr.ActiveController);
                Assert.Equal(0, mgr.ActiveController!.CurrentFrameIndex);
                Assert.Single(mgr.Cache); // Still only one entry.
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AnimationManager_Update_PublishesAnimationFinishedEvent_WhenPlayOnceFinishes()
        {
            var (manifest, basePath, tempDir) =
                MakeManifestWithFrames(nameof(FSMState.Blink), 3, SpriteMode.PlayOnce);
            try
            {
                var loader = new AssetManifestLoader();
                var bus = new EventBus();
                var mgr = new AnimationManager(loader, bus);

                AnimationFinishedEvent? received = null;
                bus.Subscribe<AnimationFinishedEvent>(e => received = e);

                mgr.TransitionTo(FSMState.Blink, manifest, basePath);
                Assert.Null(received);

                // 3 frames, Fps=10 → 100ms per frame.
                // 200ms advances 2 frames (0→1→2), at last frame.
                mgr.Update(200);
                Assert.False(mgr.ActiveController!.IsFinished);
                Assert.Null(received);

                // Next update finishes the animation — AnimationManager publishes
                // AnimationFinishedEvent and auto-transitions to Idle.
                mgr.Update(100);
                Assert.NotNull(received);
                Assert.Equal(FSMState.Blink, received!.State);

                // After auto-transition, active state should be Idle.
                Assert.Equal(FSMState.Idle, mgr.ActiveState);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}