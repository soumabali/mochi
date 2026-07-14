using System;
using MochiV2.Core.Behavior;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Hydration reminder service. Post-MVP Phase G-1.
    /// Reminds user to drink water every 60 minutes via speech bubble + meow.
    /// </summary>
    public sealed class HydrationReminderService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(HydrationReminderService));

        private readonly ITimeProvider _timeProvider;
        private readonly SpeechBubbleService _speechBubble;
        private double _lastReminderTime;
        private double _intervalSeconds = 3600; // 60 min

        /// <summary>Reminder interval in seconds (default 3600 = 60 min).</summary>
        public double IntervalSeconds
        {
            get => _intervalSeconds;
            set => _intervalSeconds = Math.Max(60, value);
        }

        /// <summary>Fired when it's time for a hydration reminder. Caller plays meow sound.</summary>
        public event Action? ReminderTriggered;

        public bool Enabled { get; set; } = true;

        public HydrationReminderService(ITimeProvider timeProvider, SpeechBubbleService speechBubble)
        {
            _timeProvider = timeProvider;
            _speechBubble = speechBubble;
            _lastReminderTime = _timeProvider.GetElapsedSeconds();
        }

        /// <summary>Tick — call every frame.</summary>
        public void Tick()
        {
            if (!Enabled) return;
            double now = _timeProvider.GetElapsedSeconds();
            if (now - _lastReminderTime >= _intervalSeconds)
            {
                _lastReminderTime = now;
                _speechBubble.Show("Minum yuk! 💧", 5.0);
                ReminderTriggered?.Invoke();
                Logger.Information("Hydration reminder triggered");
            }
        }

        /// <summary>Reset the timer (e.g. after user acknowledges).</summary>
        public void Reset()
        {
            _lastReminderTime = _timeProvider.GetElapsedSeconds();
        }
    }
}