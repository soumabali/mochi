using System;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Ambient purring service. Post-MVP Phase G-2.
    /// When cat is being petted (hover interaction) for 3+ seconds,
    /// plays a looping purr sound. Stops when petting ends.
    /// </summary>
    public sealed class PurrService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(PurrService));

        private double _pettingTimer;
        private bool _isPurring;

        /// <summary>Seconds of petting required before purring starts.</summary>
        public double PurrThreshold { get; set; } = 3.0;

        /// <summary>True when cat is currently purring.</summary>
        public bool IsPurring => _isPurring;

        /// <summary>Fired when purring should start (play loop sound).</summary>
        public event Action? PurrStarted;

        /// <summary>Fired when purring should stop.</summary>
        public event Action? PurrStopped;

        /// <summary>Call every frame while cat is being petted.</summary>
        /// <param name="deltaSeconds">Elapsed time since last tick.</param>
        public void TickPetting(double deltaSeconds)
        {
            _pettingTimer += deltaSeconds;
            if (_pettingTimer >= PurrThreshold && !_isPurring)
            {
                _isPurring = true;
                PurrStarted?.Invoke();
                Logger.Debug("Purr started (petted {Timer:F1}s)", _pettingTimer);
            }
        }

        /// <summary>Call when petting stops (mouse leaves cat).</summary>
        public void StopPetting()
        {
            _pettingTimer = 0;
            if (_isPurring)
            {
                _isPurring = false;
                PurrStopped?.Invoke();
                Logger.Debug("Purr stopped");
            }
        }
    }
}