using System;
using MochiV2.Core.Behavior;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Mood check-in service. Post-MVP Phase G-1.
    /// Every 2 hours, asks user how they're feeling via speech bubble.
    /// User responds via tray menu (Good/Okay/Bad) which adjusts cat behavior.
    /// </summary>
    public sealed class MoodCheckInService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(MoodCheckInService));

        private readonly ITimeProvider _timeProvider;
        private readonly SpeechBubbleService _speechBubble;
        private double _lastCheckInTime;
        private double _intervalSeconds = 7200; // 2 hours

        public double IntervalSeconds
        {
            get => _intervalSeconds;
            set => _intervalSeconds = Math.Max(60, value);
        }

        public bool Enabled { get; set; } = true;

        /// <summary>Last user-reported mood: "good", "okay", "bad", or null.</summary>
        public string? LastMood { get; private set; }

        /// <summary>Fired when check-in is triggered. UI shows tray notification.</summary>
        public event Action? CheckInTriggered;

        public MoodCheckInService(ITimeProvider timeProvider, SpeechBubbleService speechBubble)
        {
            _timeProvider = timeProvider;
            _speechBubble = speechBubble;
            _lastCheckInTime = _timeProvider.GetElapsedSeconds();
        }

        public void Tick()
        {
            if (!Enabled) return;
            double now = _timeProvider.GetElapsedSeconds();
            if (now - _lastCheckInTime >= _intervalSeconds)
            {
                _lastCheckInTime = now;
                _speechBubble.Show("Gimana mood kamu? 😊", 10.0);
                CheckInTriggered?.Invoke();
                Logger.Information("Mood check-in triggered");
            }
        }

        /// <summary>User responds with mood. Adjusts cat accordingly.</summary>
        public void Respond(string mood)
        {
            LastMood = mood;
            var response = mood switch
            {
                "good" => "Yeay! Ame senang! 💚",
                "okay" => "Hmm, semoga membaik ya 🐱",
                "bad" => "Ame di sini buat kamu 🐾",
                _ => "Hmm... 🤔",
            };
            _speechBubble.Show(response, 5.0);
            Logger.Information("Mood check-in response: {Mood}", mood);
        }

        public void Reset()
        {
            _lastCheckInTime = _timeProvider.GetElapsedSeconds();
        }
    }
}