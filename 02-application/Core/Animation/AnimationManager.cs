using System;
using System.Collections.Generic;
using System.IO;
using MochiV2.Core.Events;
using MochiV2.Core.Models;

namespace MochiV2.Core.Animation
{
    /// <summary>
    /// Manages the active animation for the current FSM state. T-005.
    /// Caches per-state <see cref="AnimationController"/> instances so frames
    /// are enumerated only once. Publishes <see cref="AnimationFinishedEvent"/>
    /// when the active controller finishes, then auto-transitions to
    /// <see cref="FSMState.Idle"/>.
    /// </summary>
    public sealed class AnimationManager
    {
        private readonly AssetManifestLoader _loader;
        private readonly EventBus? _eventBus;
        private readonly Dictionary<FSMState, AnimationController> _cache = new();
        private AssetManifest? _lastManifest;
        private string _lastAssetsBasePath = string.Empty;

        // Transition smoothing: minimum time before allowing animation switch
        private const double TransitionCooldownMs = 200.0;
        private double _timeSinceTransition = TransitionCooldownMs; // Allow first transition immediately
        private FSMState _pendingState;
        private bool _hasPendingTransition;

        /// <summary>
        /// Create the manager.
        /// </summary>
        /// <param name="loader">Frame enumerator (shared with caller).</param>
        /// <param name="eventBus">Optional event bus for finish notifications.</param>
        public AnimationManager(AssetManifestLoader loader, EventBus? eventBus = null)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _eventBus = eventBus;
        }

        /// <summary>The FSM state whose animation is currently active.</summary>
        public FSMState ActiveState { get; private set; }

        /// <summary>The controller driving the active animation.</summary>
        public AnimationController? ActiveController { get; private set; }

        /// <summary>Read-only view of the controller cache.</summary>
        public IReadOnlyDictionary<FSMState, AnimationController> Cache => _cache;

        /// <summary>
        /// Switch to <paramref name="newState"/>: load (or reuse cached) frames
        /// from <paramref name="manifest"/>, create/reuse an
        /// <see cref="AnimationController"/>, and set it active.
        /// </summary>
        public void TransitionTo(FSMState newState, AssetManifest manifest, string assetsBasePath)
        {
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));

            // Smooth transitions: only delay when switching FROM a loop animation
            // (walk/run/playful/angry) to another animation — this prevents jarring switches
            // in the middle of a walk cycle. Play-once animations finish naturally.
            if (_timeSinceTransition < TransitionCooldownMs
                && ActiveController is not null
                && ActiveController.Mode == SpriteMode.Loop
                && ActiveState != newState
                && newState != FSMState.Drag
                && newState != FSMState.Fall
                && newState != FSMState.Idle)
            {
                _pendingState = newState;
                _hasPendingTransition = true;
                return;
            }

            _lastManifest = manifest;
            _lastAssetsBasePath = assetsBasePath;
            _timeSinceTransition = 0;
            _hasPendingTransition = false;

            if (_cache.TryGetValue(newState, out var cached))
            {
                cached.Reset();
                ActiveController = cached;
                ActiveState = newState;
                return;
            }

            string stateName = newState.ToString();

            // Look up sprite entry; gracefully handle missing entry.
            SpriteEntry? entry = null;
            manifest.Sprites.TryGetValue(stateName, out entry);

            string folder = entry?.Folder ?? string.Empty;
            SpriteMode mode = entry?.Mode ?? SpriteMode.HoldFirstFrame;
            double speed = entry?.SpeedMultiplier ?? 1.0;

            string folderPath = Path.Combine(assetsBasePath, folder);
            List<string> frames = _loader.EnumerateFrames(folderPath, stateName);

            var controller = new AnimationController(folderPath, mode, frames, speed, entry?.Fps ?? 10, entry?.MinDurationMs ?? 0);
            _cache[newState] = controller;
            ActiveController = controller;
            ActiveState = newState;
        }

        /// <summary>
        /// Advance the active controller by <paramref name="deltaTimeMs"/>.
        /// When it finishes, publish <see cref="AnimationFinishedEvent"/> and
        /// auto-transition to <see cref="FSMState.Idle"/>.
        /// </summary>
        public void Update(double deltaTimeMs)
        {
            _timeSinceTransition += deltaTimeMs;

            // Process pending transition if cooldown has elapsed
            if (_hasPendingTransition && _timeSinceTransition >= TransitionCooldownMs
                && _lastManifest is not null)
            {
                var pending = _pendingState;
                _hasPendingTransition = false;
                TransitionTo(pending, _lastManifest, _lastAssetsBasePath);
                return;
            }

            if (ActiveController is null) return;

            bool wasFinished = ActiveController.IsFinished;
            ActiveController.Update(deltaTimeMs);

            if (!wasFinished && ActiveController.IsFinished)
            {
                _eventBus?.Publish(new AnimationFinishedEvent(ActiveState));

                // Auto-return to Idle unless we're already there.
                if (ActiveState != FSMState.Idle && _lastManifest is not null)
                {
                    TransitionTo(FSMState.Idle, _lastManifest, _lastAssetsBasePath);
                }
            }
        }

        /// <summary>Clear the cache and drop the active controller.</summary>
        public void ClearCache()
        {
            _cache.Clear();
            ActiveController = null;
        }
    }
}