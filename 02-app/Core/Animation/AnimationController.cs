using System;
using System.Collections.Generic;
using MochiV2.Core.Models;

namespace MochiV2.Core.Animation
{
    /// <summary>
    /// Manages playback of a single sprite animation. T-005.
    /// Advances frames based on FPS, speed multiplier, and <see cref="SpriteMode"/>.
    /// </summary>
    public sealed class AnimationController
    {
        private readonly string _folderPath;
        private readonly SpriteMode _mode;
        private readonly List<string> _frames;
        private readonly double _speedMultiplier;
        private double _accumulator;
        private double _elapsedMs;
        private readonly double _minDurationMs;
        private bool _naturalFinish;

        /// <summary>
        /// Create a controller.
        /// </summary>
        /// <param name="folderPath">Sprite folder path (for reference/debugging).</param>
        /// <param name="mode">Playback mode.</param>
        /// <param name="frames">Ordered list of full frame file paths.</param>
        /// <param name="speedMultiplier">Speed multiplier (1.0 = normal). Default 1.0.</param>
        public AnimationController(
            string folderPath,
            SpriteMode mode,
            List<string> frames,
            double speedMultiplier = 1.0,
            double fps = 10.0,
            double minDurationMs = 0.0)
        {
            _folderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
            _mode = mode;
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));
            _speedMultiplier = speedMultiplier <= 0 ? 1.0 : speedMultiplier;
            Fps = fps <= 0 ? 10.0 : fps;
            _minDurationMs = minDurationMs < 0 ? 0.0 : minDurationMs;
            Reset();
        }

        /// <summary>Frames per second. Default 10. Settable.</summary>
        public double Fps { get; set; }

        /// <summary>Minimum duration in ms before IsFinished can be signalled.</summary>
        public double MinDurationMs => _minDurationMs;

        /// <summary>Elapsed time in ms since animation started.</summary>
        public double ElapsedMs => _elapsedMs;

        /// <summary>Current zero-based frame index.</summary>
        public int CurrentFrameIndex { get; private set; }

        /// <summary>Total number of frames.</summary>
        public int TotalFrames => _frames.Count;

        /// <summary>True when a play-once animation has reached its terminal frame.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Full path of the current frame file.</summary>
        public string CurrentFramePath =>
            _frames.Count > 0 ? _frames[CurrentFrameIndex] : string.Empty;

        /// <summary>The playback mode.</summary>
        public SpriteMode Mode => _mode;

        /// <summary>The speed multiplier in effect.</summary>
        public double SpeedMultiplier => _speedMultiplier;

        /// <summary>
        /// Advance the animation by <paramref name="deltaTimeMs"/> milliseconds.
        /// Frame interval = 1000 / Fps / SpeedMultiplier.
        /// </summary>
        public void Update(double deltaTimeMs)
        {
            if (IsFinished) return;
            if (_frames.Count <= 1) return;
            if (_mode == SpriteMode.HoldFirstFrame) return;

            _elapsedMs += deltaTimeMs;

            double interval = 1000.0 / Fps / _speedMultiplier;
            _accumulator += deltaTimeMs;

            while (_accumulator >= interval && !IsFinished)
            {
                _accumulator -= interval;
                AdvanceOneFrame();
            }

            // For loop mode: never signal IsFinished (loops run indefinitely).
            // The minDurationMs for loops is handled at the AnimationManager level
            // via the natural loop behavior.

            // For playOnce / playOnceReversed / playOnceThenHoldLast:
            // if the animation reached its natural end but minDurationMs hasn't
            // elapsed yet, hold the last frame and wait.
            if (_naturalFinish && _minDurationMs > 0 && _elapsedMs < _minDurationMs)
            {
                // Un-finish: we need to keep holding until minDurationMs
                IsFinished = false;
            }

            // Prevent accumulator from growing unboundedly when paused at terminal
            if (IsFinished)
                _accumulator = 0;
        }

        private void AdvanceOneFrame()
        {
            switch (_mode)
            {
                case SpriteMode.HoldFirstFrame:
                    CurrentFrameIndex = 0;
                    break;

                case SpriteMode.PlayOnce:
                    if (CurrentFrameIndex < _frames.Count - 1)
                        CurrentFrameIndex++;
                    else { _naturalFinish = true; IsFinished = true; }
                    break;

                case SpriteMode.Loop:
                    CurrentFrameIndex = (CurrentFrameIndex + 1) % _frames.Count;
                    break;

                case SpriteMode.PlayOnceReversed:
                    if (CurrentFrameIndex > 0)
                        CurrentFrameIndex--;
                    else { _naturalFinish = true; IsFinished = true; }
                    break;

                case SpriteMode.PlayOnceThenHoldLast:
                    if (CurrentFrameIndex < _frames.Count - 1)
                        CurrentFrameIndex++;
                    else { _naturalFinish = true; IsFinished = true; }
                    break;

                default: { _naturalFinish = true; IsFinished = true; }
                    break;
            }
        }

        /// <summary>
        /// Reset to the start: frame 0 for forward modes, last frame for
        /// <see cref="SpriteMode.PlayOnceReversed"/>. Clears <see cref="IsFinished"/>.
        /// </summary>
        public void Reset()
        {
            IsFinished = false;
            _naturalFinish = false;
            _accumulator = 0;
            _elapsedMs = 0;
            _naturalFinish = false;
            if (_frames.Count == 0)
            {
                CurrentFrameIndex = 0;
                return;
            }
            CurrentFrameIndex = _mode == SpriteMode.PlayOnceReversed
                ? _frames.Count - 1
                : 0;
        }
    }
}