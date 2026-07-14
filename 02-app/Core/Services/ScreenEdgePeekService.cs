using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Screen-edge peek service. Post-MVP Phase G-2.
    /// When cat is idle for a long time, it peeks from the screen edge.
    /// Uses existing IdleLeft/IdleRight sprites — no new sprites needed.
    /// </summary>
    public sealed class ScreenEdgePeekService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ScreenEdgePeekService));

        private readonly ITimeProvider _timeProvider;
        private readonly IRandom _random;
        private double _idleStartTime;
        private double _peekInterval = 120; // 2 min idle → peek
        private bool _isPeeking;

        public double PeekIntervalSeconds
        {
            get => _peekInterval;
            set => _peekInterval = Math.Max(30, value);
        }

        public bool Enabled { get; set; } = true;
        public bool IsPeeking => _isPeeking;

        /// <summary>Fired when cat should peek from edge. Passes facing direction.</summary>
        public event Action<Facing>? PeekStarted;

        /// <summary>Fired when cat should stop peeking and return to normal.</summary>
        public event Action? PeekEnded;

        private double _peekDuration = 5.0; // seconds
        private double _peekStartedAt;

        public ScreenEdgePeekService(ITimeProvider timeProvider, IRandom random)
        {
            _timeProvider = timeProvider;
            _random = random;
            _idleStartTime = _timeProvider.GetElapsedSeconds();
        }

        /// <summary>Call when cat enters Idle state (resets idle timer).</summary>
        public void OnIdleStart()
        {
            _idleStartTime = _timeProvider.GetElapsedSeconds();
        }

        /// <summary>Tick — call every frame.</summary>
        public void Tick()
        {
            if (!Enabled) return;
            double now = _timeProvider.GetElapsedSeconds();

            if (_isPeeking)
            {
                if (now - _peekStartedAt >= _peekDuration)
                {
                    _isPeeking = false;
                    PeekEnded?.Invoke();
                    _idleStartTime = now;
                    Logger.Debug("Peek ended");
                }
                return;
            }

            if (now - _idleStartTime >= _peekInterval)
            {
                _isPeeking = true;
                _peekStartedAt = now;
                Facing dir = _random.NextDouble() < 0.5 ? Facing.Left : Facing.Right;
                PeekStarted?.Invoke(dir);
                Logger.Debug("Peek started: {Facing}", dir);
            }
        }

        /// <summary>Cancel any active peek.</summary>
        public void Cancel()
        {
            if (_isPeeking)
            {
                _isPeeking = false;
                PeekEnded?.Invoke();
            }
            _idleStartTime = _timeProvider.GetElapsedSeconds();
        }
    }
}