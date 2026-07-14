using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Keyboard reaction service. Post-MVP Phase G-3.
    /// Cat reacts to keyboard activity — looks toward keyboard side,
    /// or falls asleep when keyboard is idle for long.
    /// Reuses existing sprites (Idle/WalkLeft/WalkRight) — no new sprites.
    /// </summary>
    public sealed class KeyboardReactionService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(KeyboardReactionService));

        private readonly ITimeProvider _timeProvider;
        private double _lastKeyTime;
        private int _keyCount;
        private double _keyWindowStart;

        /// <summary>True when user is actively typing.</summary>
        public bool IsTyping => _keyCount > 0 && (_timeProvider.GetElapsedSeconds() - _lastKeyTime) < 5.0;

        /// <summary>True when keyboard has been idle for extended period.</summary>
        public bool IsLongIdle => (_timeProvider.GetElapsedSeconds() - _lastKeyTime) > 300; // 5 min

        /// <summary>Fired when typing starts (first key after idle).</summary>
        public event Action? TypingStarted;

        /// <summary>Fired when typing stops (5s after last key).</summary>
        public event Action? TypingStopped;

        /// <summary>Fired when long idle detected (5 min no keys).</summary>
        public event Action? LongIdleDetected;

        private bool _wasTyping;
        private bool _longIdleFired;

        public KeyboardReactionService(ITimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
            _lastKeyTime = timeProvider.GetElapsedSeconds();
            _keyWindowStart = _lastKeyTime;
        }

        /// <summary>Call when a key is pressed.</summary>
        public void OnKeyPress()
        {
            double now = _timeProvider.GetElapsedSeconds();
            if (now - _keyWindowStart > 60)
            {
                _keyCount = 0;
                _keyWindowStart = now;
            }
            _keyCount++;
            _lastKeyTime = now;
            _longIdleFired = false;
        }

        /// <summary>Tick — call every frame.</summary>
        public void Tick()
        {
            bool typing = IsTyping;
            if (typing && !_wasTyping)
            {
                TypingStarted?.Invoke();
                Logger.Debug("Typing started");
            }
            if (!typing && _wasTyping)
            {
                TypingStopped?.Invoke();
                Logger.Debug("Typing stopped");
            }
            _wasTyping = typing;

            if (IsLongIdle && !_longIdleFired)
            {
                _longIdleFired = true;
                LongIdleDetected?.Invoke();
                Logger.Information("Long idle detected (5+ min no keyboard)");
            }
        }

        /// <summary>Keys per minute in current window.</summary>
        public int KeysPerMinute
        {
            get
            {
                double elapsed = _timeProvider.GetElapsedSeconds() - _keyWindowStart;
                if (elapsed < 1) return 0;
                return (int)(_keyCount / elapsed * 60);
            }
        }
    }
}