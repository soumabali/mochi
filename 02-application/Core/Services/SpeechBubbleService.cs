using System;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Shows text speech bubbles above Mochi for reminders.
    /// Post-MVP Phase F. The actual WPF window is handled by the UI layer;
    /// this service manages the lifecycle (show/hide/fade) and scheduling.
    /// </summary>
    public sealed class SpeechBubbleService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(SpeechBubbleService));

        /// <summary>Fired when a bubble should be shown. UI layer subscribes.</summary>
        public event Action<string, double>? ShowRequested;

        /// <summary>Fired when the bubble should be hidden.</summary>
        public event Action? HideRequested;

        private string? _currentText;
        private double _remainingSeconds;

        /// <summary>Current visible text, or null if no bubble visible.</summary>
        public string? CurrentText => _currentText;

        /// <summary>True when a speech bubble is currently visible.</summary>
        public bool IsVisible => _currentText != null;

        /// <summary>
        /// Show a speech bubble with the given text for a duration.
        /// </summary>
        /// <param name="text">Text to display.</param>
        /// <param name="durationSeconds">How long to show (default 3s).</param>
        public void Show(string text, double durationSeconds = 3.0)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be empty.", nameof(text));

            _currentText = text;
            _remainingSeconds = durationSeconds;
            ShowRequested?.Invoke(text, durationSeconds);
            Logger.Debug("Speech bubble: \"{Text}\" for {Dur:F1}s", text, durationSeconds);
        }

        /// <summary>Hide the current speech bubble immediately.</summary>
        public void Hide()
        {
            if (_currentText == null) return;
            _currentText = null;
            _remainingSeconds = 0;
            HideRequested?.Invoke();
            Logger.Debug("Speech bubble hidden");
        }

        /// <summary>Tick — call every frame to auto-hide expired bubbles.</summary>
        /// <param name="deltaSeconds">Elapsed time since last tick.</param>
        public void Tick(double deltaSeconds)
        {
            if (_currentText == null) return;
            _remainingSeconds -= deltaSeconds;
            if (_remainingSeconds <= 0)
            {
                Hide();
            }
        }
    }
}