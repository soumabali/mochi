using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Animation
{
    /// <summary>
    /// Loads <c>manifest.json</c> and enumerates sprite frames at runtime.
    /// Asset lock compliant: never hardcodes frame counts, never auto-corrects
    /// the <c>cat_surpised</c> typo folder, fails loud on missing assets
    /// (publishes <see cref="AssetMissing"/> + logs + returns fallback).
    /// </summary>
    public sealed class AssetManifestLoader
    {
        /// <summary>
        /// Published when a manifest-referenced folder or file is missing on disk.
        /// Parameters: (stateName, missingPath).
        /// </summary>
        public event Action<string, string>? AssetMissing;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Deserialize <c>manifest.json</c> from the given path.
        /// </summary>
        /// <param name="path">Absolute path to manifest.json.</param>
        /// <returns>The parsed <see cref="AssetManifest"/>.</returns>
        /// <exception cref="FileNotFoundException">manifest.json not found.</exception>
        /// <exception cref="JsonException">manifest.json is malformed.</exception>
        public async Task<AssetManifest> LoadAsync(string path)
        {
            if (!File.Exists(path))
            {
                Log.Error("Asset manifest not found at {ManifestPath}", path);
                throw new FileNotFoundException("manifest.json not found", path);
            }

            string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<AssetManifest>(json, JsonOptions);

            if (manifest is null)
            {
                Log.Error("Asset manifest at {ManifestPath} deserialized to null", path);
                throw new JsonException("manifest.json deserialized to null");
            }

            Log.Information(
                "Asset manifest loaded: {SpriteCount} sprites, {SoundCount} sounds, {NoSoundCount} statesWithoutSound",
                manifest.Sprites.Count,
                manifest.Sounds.Count,
                manifest.StatesWithoutSound.Count);

            return manifest;
        }

        /// <summary>
        /// Enumerate all PNG files in a sprite folder, sorted by filename.
        /// NEVER hardcodes frame counts — discovers what is actually on disk.
        /// Returns an empty list (and publishes <see cref="AssetMissing"/>) if
        /// the folder does not exist or contains no PNGs.
        /// </summary>
        /// <param name="folderPath">Absolute path to the sprite folder.</param>
        /// <param name="stateName">State name for AssetMissing reporting (optional).</param>
        /// <returns>Sorted list of full PNG file paths. Empty if folder missing.</returns>
        public List<string> EnumerateFrames(string folderPath, string? stateName = null)
        {
            if (!Directory.Exists(folderPath))
            {
                Log.Warning("Sprite folder missing: {FolderPath}", folderPath);
                AssetMissing?.Invoke(stateName ?? string.Empty, folderPath);
                return new List<string>();
            }

            var files = Directory.GetFiles(folderPath, "*.png")
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                Log.Warning("Sprite folder has no PNGs: {FolderPath}", folderPath);
                AssetMissing?.Invoke(stateName ?? string.Empty, folderPath);
            }

            return files;
        }

        /// <summary>
        /// Validate that all sprite folders and sound files referenced by the
        /// manifest exist on disk. Missing assets are logged and collected,
        /// and <see cref="AssetMissing"/> is published for each. Never crashes.
        /// </summary>
        /// <param name="manifest">The loaded manifest.</param>
        /// <param name="assetsBasePath">
        /// Absolute path to the Assets/ directory (manifest paths are relative to this).
        /// </param>
        /// <returns>List of missing paths (empty if all assets present).</returns>
        public List<string> ValidateManifest(AssetManifest manifest, string assetsBasePath)
        {
            var missing = new List<string>();

            // --- Check sprite folders ---
            foreach (var kvp in manifest.Sprites)
            {
                string stateName = kvp.Key;
                string relativeFolder = kvp.Value.Folder;
                string fullPath = Path.Combine(assetsBasePath, relativeFolder);

                if (!Directory.Exists(fullPath))
                {
                    Log.Warning(
                        "Missing sprite folder for state {State}: {Path}",
                        stateName, fullPath);
                    missing.Add(fullPath);
                    AssetMissing?.Invoke(stateName, fullPath);
                }
            }

            // --- Check sound files ---
            foreach (var kvp in manifest.Sounds)
            {
                string soundKey = kvp.Key;
                string relativeFile = kvp.Value;
                string fullPath = Path.Combine(assetsBasePath, relativeFile);

                if (!File.Exists(fullPath))
                {
                    Log.Warning(
                        "Missing sound file for key {Key}: {Path}",
                        soundKey, fullPath);
                    missing.Add(fullPath);
                    AssetMissing?.Invoke(soundKey, fullPath);
                }
            }

            if (missing.Count == 0)
            {
                Log.Information("Asset manifest validation passed — all assets present");
            }
            else
            {
                Log.Warning("Asset manifest validation found {Count} missing assets", missing.Count);
            }

            return missing;
        }
    }
}