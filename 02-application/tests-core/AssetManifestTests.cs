using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MochiV2.Core.Animation;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-004: Asset manifest loader + frame enumerator tests.
    /// Validates manifest loading, frame enumeration, AssetMissing events,
    /// and the cat_surpised typo folder handling.
    /// </summary>
    public class AssetManifestTests
    {
        /// <summary>
        /// Absolute path to the 02-application root (where Assets/ lives).
        /// </summary>
        private static readonly string AppRoot = GetAppRoot();

        private static string GetAppRoot()
        {
            // Walk up from test bin dir to find Assets/
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "Assets")))
                    return dir;
                var parent = Directory.GetParent(dir);
                if (parent is null) break;
                dir = parent.FullName;
            }
            return "/home/ubuntu/projects/mochi-v2/02-application";
        }

        private static string ManifestPath => Path.Combine(AppRoot, "Assets", "manifest.json");
        private static string AssetsPath => Path.Combine(AppRoot, "Assets");

        [Fact]
        public async Task Manifest_LoadsFromJson_Correctly()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            // PRD §5: 25 sprite entries (some alias same folder)
            Assert.Equal(25, manifest.Sprites.Count);
            // PRD §5: 11 sound entries
            Assert.Equal(11, manifest.Sounds.Count);
            // PRD §5: 9 statesWithoutSound
            Assert.Equal(9, manifest.StatesWithoutSound.Count);

            // Sound settings defaults from PRD §5
            Assert.Equal(0.35, manifest.SoundSettings.MasterVolumeDefault);
            Assert.Equal(0.1, manifest.SoundSettings.BlinkSoundProbability);
            Assert.False(manifest.SoundSettings.WalkSoundLoop);
            Assert.Equal(4000, manifest.SoundSettings.WalkSoundIntervalMs);
            Assert.Equal(8000, manifest.SoundSettings.CooldownPerSoundMs);

            // Spot-check a few sprite entries
            var idleLeft = manifest.Sprites["IdleLeft"];
            Assert.Equal("Sprite_optimized/cat_blinking_left", idleLeft.Folder);
            Assert.Equal(SpriteMode.HoldFirstFrame, idleLeft.Mode);
            Assert.Equal(1.0, idleLeft.SpeedMultiplier); // default when omitted

            // Eating has speedMultiplier 1.3
            var eating = manifest.Sprites["Eating"];
            Assert.Equal("Sprite_optimized/begging_food", eating.Folder);
            Assert.Equal(SpriteMode.Loop, eating.Mode);
            Assert.Equal(1.3, eating.SpeedMultiplier);

            // FallVar1 uses playOnceReversed
            Assert.Equal(SpriteMode.PlayOnceReversed, manifest.Sprites["FallVar1"].Mode);

            // SleepYawn uses playOnceThenHoldLast
            Assert.Equal(SpriteMode.PlayOnceThenHoldLast, manifest.Sprites["SleepYawn"].Mode);

            // Surprised uses the typo folder
            Assert.Equal("Sprite_optimized/cat_surpised", manifest.Sprites["Surprised"].Folder);

            // statesWithoutSound contents
            Assert.Contains("IdleLeft", manifest.StatesWithoutSound);
            Assert.Contains("WakeUp", manifest.StatesWithoutSound);
            Assert.Contains("FallVar2", manifest.StatesWithoutSound);
        }

        [Fact]
        public void EnumerateFrames_ReturnsSortedPngList()
        {
            var loader = new AssetManifestLoader();
            string folder = Path.Combine(AssetsPath, "Sprite", "cat_surpised");

            var frames = loader.EnumerateFrames(folder, "Surprised");

            // Should have PNGs (not hardcoded — discovered at runtime)
            Assert.NotEmpty(frames);

            // All should be .png
            Assert.All(frames, f => Assert.EndsWith(".png", f, StringComparison.OrdinalIgnoreCase));

            // Should be sorted by filename
            var sorted = frames
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.Equal(sorted, frames);
        }

        [Fact]
        public void EnumerateFrames_MissingFolder_TriggersAssetMissingEvent()
        {
            var loader = new AssetManifestLoader();
            string missingPath = Path.Combine(AssetsPath, "Sprite", "does_not_exist_xyz");

            string? eventState = null;
            string? eventPath = null;
            loader.AssetMissing += (state, path) =>
            {
                eventState = state;
                eventPath = path;
            };

            var frames = loader.EnumerateFrames(missingPath, "FakeState");

            Assert.Empty(frames); // fallback: empty list, no crash
            Assert.Equal("FakeState", eventState);
            Assert.Equal(missingPath, eventPath);
        }

        [Fact]
        public async Task Manifest_CatSurpisedTypoFolder_FoundViaManifest()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            // The manifest MUST use the typo folder name "cat_surpised" (with typo)
            // Never auto-corrected to "cat_surprised"
            var surprised = manifest.Sprites["Surprised"];
            Assert.Contains("cat_surpised", surprised.Folder);
            Assert.DoesNotContain("cat_surprised", surprised.Folder); // no corrected version

            // The folder must actually exist on disk with the typo
            string typoFolder = Path.Combine(AssetsPath, surprised.Folder);
            Assert.True(Directory.Exists(typoFolder),
                $"Typo folder {typoFolder} must exist on disk");

            // And it must contain PNGs
            var frames = loader.EnumerateFrames(typoFolder, "Surprised");
            Assert.NotEmpty(frames);
        }

        [Fact]
        public async Task ValidateManifest_AllAssetsPresent_ReturnsEmptyList()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            var missing = loader.ValidateManifest(manifest, AssetsPath);

            // All manifest-referenced assets should exist on disk
            Assert.Empty(missing);
        }

        [Fact]
        public async Task ValidateManifest_MissingAsset_TriggersEvent()
        {
            // Use a temp dir with a deliberately broken manifest
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                string manifestJson = """
                {
                  "sprites": {
                    "TestState": { "folder": "Sprite/missing_folder", "mode": "loop" }
                  },
                  "sounds": {
                    "TestSound": "Sound/missing.wav"
                  },
                  "statesWithoutSound": [],
                  "soundSettings": {
                    "masterVolumeDefault": 0.35,
                    "blinkSoundProbability": 0.1,
                    "walkSoundLoop": false,
                    "walkSoundIntervalMs": 4000,
                    "cooldownPerSoundMs": 8000
                  }
                }
                """;
                string manifestPath = Path.Combine(tempDir, "manifest.json");
                await File.WriteAllTextAsync(manifestPath, manifestJson);

                var loader = new AssetManifestLoader();
                var manifest = await loader.LoadAsync(manifestPath);

                var events = new System.Collections.Generic.List<(string state, string path)>();
                loader.AssetMissing += (state, path) => events.Add((state, path));

                var missing = loader.ValidateManifest(manifest, tempDir);

                Assert.Equal(2, missing.Count); // missing folder + missing sound
                Assert.Equal(2, events.Count);
                Assert.Contains(("TestState", Path.Combine(tempDir, "Sprite", "missing_folder")), events);
                Assert.Contains(("TestSound", Path.Combine(tempDir, "Sound", "missing.wav")), events);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}