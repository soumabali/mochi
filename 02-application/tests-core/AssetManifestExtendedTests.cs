using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MochiV2.Core.Animation;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-021: Additional manifest loading and asset fallback tests.
    /// Validates valid manifest loading, missing asset fallback,
    /// cat_surpised typo handling, frame enumeration sorting.
    /// </summary>
    public class AssetManifestExtendedTests
    {
        private static readonly string AppRoot = GetAppRoot();

        private static string GetAppRoot()
        {
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

        //------------------------------------------------------------------
        // Valid manifest loading
        //------------------------------------------------------------------

        [Fact]
        public async Task Manifest_Loads_All_Sprite_Entries()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            Assert.Equal(34, manifest.Sprites.Count);

            // Every sprite entry should have a non-empty folder
            Assert.All(manifest.Sprites, kvp =>
            {
                Assert.False(string.IsNullOrWhiteSpace(kvp.Value.Folder),
                    $"Sprite '{kvp.Key}' has empty folder");
            });
        }

        [Fact]
        public async Task Manifest_Loads_All_Sound_Entries()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            Assert.Equal(16, manifest.Sounds.Count);
            Assert.All(manifest.Sounds, kvp =>
            {
                Assert.False(string.IsNullOrWhiteSpace(kvp.Value),
                    $"Sound '{kvp.Key}' has empty path");
            });
        }

        [Fact]
        public async Task Manifest_StatesWithoutSound_Loaded()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            Assert.Equal(9, manifest.StatesWithoutSound.Count);
        }

        [Fact]
        public async Task Manifest_Sprite_Modes_Are_Valid_Enum_Values()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            Assert.All(manifest.Sprites, kvp =>
            {
                Assert.True(Enum.IsDefined(typeof(SpriteMode), kvp.Value.Mode),
                    $"Sprite '{kvp.Key}' has invalid mode {kvp.Value.Mode}");
            });
        }

        [Fact]
        public async Task Manifest_SpeedMultipliers_Are_Positive()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            Assert.All(manifest.Sprites, kvp =>
            {
                Assert.True(kvp.Value.SpeedMultiplier > 0,
                    $"Sprite '{kvp.Key}' has non-positive speed {kvp.Value.SpeedMultiplier}");
            });
        }

        [Fact]
        public async Task Manifest_Contains_Expected_State_Keys()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            // Spot-check critical state entries
            Assert.Contains("IdleLeft", manifest.Sprites);
            Assert.Contains("IdleRight", manifest.Sprites);
            Assert.Contains("WalkLeft", manifest.Sprites);
            Assert.Contains("WalkRight", manifest.Sprites);
            Assert.Contains("Surprised", manifest.Sprites);
            Assert.Contains("Eating", manifest.Sprites);
            Assert.Contains("SleepYawn", manifest.Sprites);
            Assert.Contains("WakeUp", manifest.Sprites);
        }

        [Fact]
        public async Task Manifest_SoundSettings_Have_Correct_Defaults()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            Assert.Equal(0.35, manifest.SoundSettings.MasterVolumeDefault);
            Assert.Equal(0.1, manifest.SoundSettings.BlinkSoundProbability);
            Assert.False(manifest.SoundSettings.WalkSoundLoop);
            Assert.Equal(4000, manifest.SoundSettings.WalkSoundIntervalMs);
            Assert.Equal(8000, manifest.SoundSettings.CooldownPerSoundMs);
        }

        //------------------------------------------------------------------
        // Missing file / fallback
        //------------------------------------------------------------------

        [Fact]
        public async Task LoadAsync_Missing_Manifest_File_Throws_FileNotFoundException()
        {
            var loader = new AssetManifestLoader();
            string badPath = Path.Combine(Path.GetTempPath(), "no_such_manifest_" + Guid.NewGuid().ToString("N") + ".json");
            await Assert.ThrowsAsync<FileNotFoundException>(() => loader.LoadAsync(badPath));
        }

        [Fact]
        public async Task LoadAsync_Malformed_Json_Throws_JsonException()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_manifest_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                string manifestPath = Path.Combine(tempDir, "manifest.json");
                await File.WriteAllTextAsync(manifestPath, "{ invalid json }}}");

                var loader = new AssetManifestLoader();
                await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => loader.LoadAsync(manifestPath));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void EnumerateFrames_Empty_Folder_TriggersAssetMissingEvent()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_frames_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                var loader = new AssetManifestLoader();
                string? eventState = null, eventPath = null;
                loader.AssetMissing += (state, path) => { eventState = state; eventPath = path; };

                // Folder exists but has no PNGs
                var frames = loader.EnumerateFrames(tempDir, "EmptyState");

                Assert.Empty(frames);
                Assert.Equal("EmptyState", eventState);
                Assert.Equal(tempDir, eventPath);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void EnumerateFrames_Returns_Sorted_Png_Files()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_sort_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create PNGs in non-sorted order
                string[] fileNames = { "frame_03.png", "frame_01.png", "frame_10.png", "frame_02.png" };
                foreach (var name in fileNames)
                    File.WriteAllText(Path.Combine(tempDir, name), "fake");

                var loader = new AssetManifestLoader();
                var frames = loader.EnumerateFrames(tempDir, "TestState");

                Assert.Equal(4, frames.Count);
                // Should be sorted by filename
                var expected = new[] { "frame_01.png", "frame_02.png", "frame_03.png", "frame_10.png" };
                for (int i = 0; i < expected.Length; i++)
                    Assert.EndsWith(expected[i], frames[i]);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void EnumerateFrames_Ignores_Non_Png_Files()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_png_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "frame_01.png"), "fake");
                File.WriteAllText(Path.Combine(tempDir, "frame_02.txt"), "fake");
                File.WriteAllText(Path.Combine(tempDir, "readme.md"), "fake");
                File.WriteAllText(Path.Combine(tempDir, "frame_03.png"), "fake");

                var loader = new AssetManifestLoader();
                var frames = loader.EnumerateFrames(tempDir, "TestState");

                Assert.Equal(2, frames.Count);
                Assert.All(frames, f => Assert.EndsWith(".png", f, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }

        //------------------------------------------------------------------
        // cat_surpised typo
        //------------------------------------------------------------------

        [Fact]
        public async Task Manifest_Surprised_Uses_Typo_Folder_Name()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            var surprised = manifest.Sprites["Surprised"];
            Assert.Equal("Sprite_optimized/cat_surpised", surprised.Folder);
            // Should NOT be the corrected spelling
            Assert.DoesNotContain("cat_surprised", surprised.Folder);
        }

        [Fact]
        public async Task Manifest_Surprised_Typo_Folder_Exists_On_Disk()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            string typoFolder = Path.Combine(AssetsPath, manifest.Sprites["Surprised"].Folder);
            Assert.True(Directory.Exists(typoFolder),
                $"Typo folder {typoFolder} must exist on disk");
        }

        [Fact]
        public async Task Manifest_Surprised_Typo_Folder_Has_PNGs()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            string typoFolder = Path.Combine(AssetsPath, manifest.Sprites["Surprised"].Folder);
            var frames = loader.EnumerateFrames(typoFolder, "Surprised");

            Assert.NotEmpty(frames);
        }

        //------------------------------------------------------------------
        // ValidateManifest
        //------------------------------------------------------------------

        [Fact]
        public async Task ValidateManifest_All_Present_Returns_Empty()
        {
            var loader = new AssetManifestLoader();
            var manifest = await loader.LoadAsync(ManifestPath);

            var missing = loader.ValidateManifest(manifest, AssetsPath);
            Assert.Empty(missing);
        }

        [Fact]
        public async Task ValidateManifest_Missing_Sprite_Folder_Reported()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "mochi_validate_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create a valid sound file so only the sprite is missing
                Directory.CreateDirectory(Path.Combine(tempDir, "Sound"));
                await File.WriteAllTextAsync(Path.Combine(tempDir, "Sound", "ok.ogg"), "fake");

                string manifestJson = """
                {
                    "sprites": {
                        "TestState": { "folder": "Sprite/missing_folder", "mode": "loop" }
                    },
                    "sounds": {
                        "TestSound": "Sound/ok.ogg"
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

                var missing = loader.ValidateManifest(manifest, tempDir);
                Assert.Single(missing);
                Assert.Contains(missing, p => p.Contains("missing_folder"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}