using System;
using System.Windows;
using Serilog;

namespace MochiV2.UI.Stats
{
    /// <summary>
    /// Stats dashboard window showing needs, mood, level, pomodoro, weather.
    /// Post-MVP Phase H-2.
    /// </summary>
    public partial class StatsWindow : Window
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(StatsWindow));

        public StatsWindow()
        {
            InitializeComponent();
        }

        /// <summary>Update all stats at once.</summary>
        public void UpdateStats(int food, int energy, int happiness, string mood,
            int level, int xp, int xpForNext,
            string pomodoroState, string pomodoroRemaining,
            string weatherTemp, string weatherDesc)
        {
            Dispatcher.Invoke(() =>
            {
                FoodBar.Value = food;
                EnergyBar.Value = energy;
                HappinessBar.Value = happiness;
                MoodText.Text = mood;
                LevelText.Text = level.ToString();
                XpBar.Maximum = xpForNext;
                XpBar.Value = xp;
                PomodoroStateText.Text = pomodoroState;
                PomodoroTimeText.Text = pomodoroRemaining;
                WeatherTempText.Text = weatherTemp;
                WeatherDescText.Text = weatherDesc;
            });
        }

        /// <summary>Quick update with just needs + mood.</summary>
        public void UpdateNeeds(int food, int energy, int happiness, string mood)
        {
            Dispatcher.Invoke(() =>
            {
                FoodBar.Value = food;
                EnergyBar.Value = energy;
                HappinessBar.Value = happiness;
                MoodText.Text = mood;
            });
        }

        /// <summary>Update pomodoro display.</summary>
        public void UpdatePomodoro(string state, string remaining)
        {
            Dispatcher.Invoke(() =>
            {
                PomodoroStateText.Text = state;
                PomodoroTimeText.Text = remaining;
            });
        }

        /// <summary>Update weather display.</summary>
        public void UpdateWeather(string temp, string desc)
        {
            Dispatcher.Invoke(() =>
            {
                WeatherTempText.Text = temp;
                WeatherDescText.Text = desc;
            });
        }
    }
}