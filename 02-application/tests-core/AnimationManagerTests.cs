using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MochiV2.Core.Animation;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-021: AnimationManager tests — caching, finish event publication,
    /// auto-transition to Idle on terminal states.
    /// </summary>
    public class AnimationManagerTests
    {
        //---- helpers -------------------------------------------------------

        /// <summary>
        /// Create a temp assets dir with a sprite folder containing N dummy PNGs.
        /// Returns (assetsBasePath, manifest).
        /// </summary>
        private static async Task<(string assetsPath, AssetManifest manifest)> MakeAssetsAsync(
            string stateName, SpriteMode mode, int frameCount, double speed = 1.0)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_anim_test_" + Guid.NewGuid().ToString("N")[..8]);
            string spriteDir = Path.Combine(tempDir, "Sprite", "test_sprite");
            Directory.CreateDirectory(spriteDir);
            for (int i = 0; i < frameCount; i++)
                await File.WriteAllTextAsync(Path.Combine(spriteDir, $"frame_{i}.png"), "fake");

            var manifest = new AssetManifest();
            manifest.Sprites[stateName] = new SpriteEntry
            {
                Folder = "Sprite/test_sprite",
                Mode = mode,
                SpeedMultiplier = speed
            };
            // Idle fallback
            manifest.Sprites["Idle"] = new SpriteEntry
            {
                Folder = "Sprite/test_sprite",
                Mode = SpriteMode.HoldFirstFrame,
                SpeedMultiplier = 1.0
            };

            return (tempDir, manifest);
        }

        //------------------------------------------------------------------
        // TransitionTo / caching
        //------------------------------------------------------------------

        [Fact]
        public async Task TransitionTo_Loads_Controller_For_State()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("JumpVar1", SpriteMode.PlayOnce, 4);
            try
            {
                var loader = new AssetManifestLoader();
                var mgr = new AnimationManager(loader);

                mgr.TransitionTo(FSMState.JumpVar1, manifest, assetsPath);

                Assert.Equal(FSMState.JumpVar1, mgr.ActiveState);
                Assert.NotNull(mgr.ActiveController);
                Assert.Equal(4, mgr.ActiveController!.TotalFrames);
                Assert.Equal(SpriteMode.PlayOnce, mgr.ActiveController.Mode);
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public async Task TransitionTo_Caches_Controller_And_Reuses_On_Second_Visit()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("JumpVar1", SpriteMode.PlayOnce, 4);
            try
            {
                var loader = new AssetManifestLoader();
                var mgr = new AnimationManager(loader);

                mgr.TransitionTo(FSMState.JumpVar1, manifest, assetsPath);
                var firstController = mgr.ActiveController;
                Assert.NotNull(firstController);

                // Transition to Idle, then back to JumpVar1
                mgr.TransitionTo(FSMState.Idle, manifest, assetsPath);
                mgr.TransitionTo(FSMState.JumpVar1, manifest, assetsPath);

                // Should reuse cached controller (same reference) and reset it
                Assert.Same(firstController, mgr.ActiveController);
                Assert.Equal(0, mgr.ActiveController!.CurrentFrameIndex); // reset
                Assert.True(mgr.Cache.ContainsKey(FSMState.JumpVar1));
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public async Task TransitionTo_Null_Manifest_Throws()
        {
            var loader = new AssetManifestLoader();
            var mgr = new AnimationManager(loader);
            Assert.Throws<ArgumentNullException>(() =>
                mgr.TransitionTo(FSMState.Idle, null!, ""));
        }

        [Fact]
        public async Task TransitionTo_Missing_Sprite_Entry_Defaults_To_HoldFirstFrame()
        {
            // State not in manifest → no entry, empty frames
            var tempDir = Path.Combine(Path.GetTempPath(), "mochi_anim_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                var manifest = new AssetManifest();
                var loader = new AssetManifestLoader();
                var mgr = new AnimationManager(loader);

                // UnknownState has no sprite entry
                mgr.TransitionTo(FSMState.Angry, manifest, tempDir);

                Assert.NotNull(mgr.ActiveController);
                Assert.Equal(0, mgr.ActiveController!.TotalFrames); // no frames
                Assert.Equal(SpriteMode.HoldFirstFrame, mgr.ActiveController.Mode);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }

        //------------------------------------------------------------------
        // AnimationFinishedEvent publication
        //------------------------------------------------------------------

        [Fact]
        public async Task Update_Publishes_AnimationFinishedEvent_On_Terminal_State()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("JumpVar1", SpriteMode.PlayOnce, 2);
            try
            {
                var loader = new AssetManifestLoader();
                var bus = new EventBus();
                var finishedEvents = new List<AnimationFinishedEvent>();
                bus.Subscribe<AnimationFinishedEvent>(e => finishedEvents.Add(e));

                var mgr = new AnimationManager(loader, bus);
                mgr.TransitionTo(FSMState.JumpVar1, manifest, assetsPath);

                // Fps=10 → 100ms per frame. 2 frames: 100ms reach last, 200ms finish.
                mgr.ActiveController!.Fps = 10;
                mgr.Update(200); //advance + finish

                Assert.Single(finishedEvents);
                Assert.Equal(FSMState.JumpVar1, finishedEvents[0].State);
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public async Task Update_Auto_Transitions_To_Idle_After_Finish()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("JumpVar1", SpriteMode.PlayOnce, 2);
            try
            {
                var loader = new AssetManifestLoader();
                var bus = new EventBus();
                var mgr = new AnimationManager(loader, bus);

                mgr.TransitionTo(FSMState.JumpVar1, manifest, assetsPath);
                mgr.ActiveController!.Fps = 10;

                mgr.Update(300); // finish

                // Should auto-transition to Idle
                Assert.Equal(FSMState.Idle, mgr.ActiveState);
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public async Task Update_Loop_Does_Not_Publish_FinishedEvent()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("WalkLeft", SpriteMode.Loop, 3);
            try
            {
                var loader = new AssetManifestLoader();
                var bus = new EventBus();
                var finishedEvents = new List<AnimationFinishedEvent>();
                bus.Subscribe<AnimationFinishedEvent>(e => finishedEvents.Add(e));

                var mgr = new AnimationManager(loader, bus);
                mgr.TransitionTo(FSMState.WalkLeft, manifest, assetsPath);
                mgr.ActiveController!.Fps = 10;

                // Advance many frames — loop never finishes
                mgr.Update(1000);
                Assert.False(mgr.ActiveController!.IsFinished);
                Assert.Empty(finishedEvents);
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public async Task Update_HoldFirstFrame_Does_Not_Publish_FinishedEvent()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("Idle", SpriteMode.HoldFirstFrame, 3);
            try
            {
                var loader = new AssetManifestLoader();
                var bus = new EventBus();
                var finishedEvents = new List<AnimationFinishedEvent>();
                bus.Subscribe<AnimationFinishedEvent>(e => finishedEvents.Add(e));

                var mgr = new AnimationManager(loader, bus);
                mgr.TransitionTo(FSMState.Idle, manifest, assetsPath);
                mgr.Update(5000);

                Assert.Empty(finishedEvents);
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public async Task Update_No_Active_Controller_Is_NoOp()
        {
            var loader = new AssetManifestLoader();
            var bus = new EventBus();
            var mgr = new AnimationManager(loader, bus);

            // No TransitionTo called — should not throw
            mgr.Update(100);
            Assert.Null(mgr.ActiveController);
        }

        //------------------------------------------------------------------
        // ClearCache
        //------------------------------------------------------------------

        [Fact]
        public async Task ClearCache_Removes_All_Cached_Controllers()
        {
            var (assetsPath, manifest) = await MakeAssetsAsync("JumpVar1", SpriteMode.PlayOnce, 4);
            try
            {
                var loader = new AssetManifestLoader();
                var mgr = new AnimationManager(loader);

                mgr.TransitionTo(FSMState.JumpVar1, manifest, assetsPath);
                Assert.NotEmpty(mgr.Cache);
                Assert.NotNull(mgr.ActiveController);

                mgr.ClearCache();

                Assert.Empty(mgr.Cache);
                Assert.Null(mgr.ActiveController);
            }
            finally
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath, recursive: true);
            }
        }

        [Fact]
        public void Constructor_Null_Loader_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AnimationManager(null!));
        }
    }
}