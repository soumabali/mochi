using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Serilog;

namespace MochiV2.UI.SpeechBubble
{
    /// <summary>
    /// Transparent speech bubble window that appears above Mochi.
    /// Post-MVP Phase F/G: shows text reminders (hydration, quotes, pomodoro, etc).
    /// </summary>
    public partial class SpeechBubbleWindow : Window
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(SpeechBubbleWindow));

        public SpeechBubbleWindow()
        {
            InitializeComponent();
            Hide(); // Start hidden
        }

        /// <summary>
        /// Show the speech bubble at a position near the cat with text.
        /// </summary>
        /// <param name="text">Text to display.</param>
        /// <param name="catX">Cat X position (screen coords).</param>
        /// <param name="catY">Cat Y position (screen coords).</param>
        /// <param name="durationSeconds">How long to show before auto-hide.</param>
        public void ShowAt(string text, double catX, double catY, double durationSeconds = 3.0)
        {
            Dispatcher.Invoke(() =>
            {
                BubbleText.Text = text;

                // Position above cat, centered
                Left = catX - (Width / 2) + 100; // approximate sprite center offset
                Top = catY - Height - 10;

                // Clamp to screen
                if (Left < 0) Left = 10;
                if (Left + Width > SystemParameters.PrimaryScreenWidth)
                    Left = SystemParameters.PrimaryScreenWidth - Width - 10;
                if (Top < 0) Top = 10;

                Show();

                // Animate scale-in (pop effect)
                var scaleIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
                };
                BubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
                BubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

                Logger.Debug("Speech bubble shown: \"{Text}\" at ({X:F0},{Y:F0})", text, Left, Top);
            });

            // Auto-hide after duration
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                HideWithAnimation();
            };
            timer.Start();
        }

        /// <summary>Hide with fade-out animation.</summary>
        public void HideWithAnimation()
        {
            Dispatcher.Invoke(() =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, e) => Hide();
                BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        /// <summary>Instantly hide.</summary>
        public new void Hide()
        {
            Dispatcher.Invoke(() =>
            {
                Opacity = 1;
                base.Hide();
            });
        }
    }
}