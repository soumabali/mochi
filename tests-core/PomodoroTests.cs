using System;
using System.Collections.Generic;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// Tests for Post-MVP Phase F: Pomodoro timer and SpeechBubbleService.
    /// </summary>
    public class PomodoroTests
    {
        private class FakeTimeProvider : ITimeProvider
        {
            private double _elapsed;
            public double GetElapsedSeconds() => _elapsed;
            public void Advance(double seconds) => _elapsed += seconds;
        }

        private static (PomodoroService svc, FakeTimeProvider time, EventBus bus, List<PomodoroEvent> events) Make()
        {
            var time = new FakeTimeProvider();
            var bus = new EventBus();
            var events = new List<PomodoroEvent>();
            bus.Subscribe<PomodoroEvent>(e => events.Add(e));
            var svc = new PomodoroService(time, bus);
            return (svc, time, bus, events);
        }

        //---- PomodoroService basic ----

        [Fact]
        public void Start_FromIdle_EntersFocus()
        {
            var (svc, _, _, events) = Make();
            svc.Start();
            Assert.Equal(PomodoroState.Focus, svc.State);
            Assert.Equal(1, svc.Round);
            Assert.Single(events);
            Assert.Equal(PomodoroState.Focus, events[0].State);
        }

        [Fact]
        public void Reset_ReturnsToIdle()
        {
            var (svc, _, _, _) = Make();
            svc.Start();
            svc.Reset();
            Assert.Equal(PomodoroState.Idle, svc.State);
            Assert.Equal(0, svc.Round);
        }

        [Fact]
        public void Pause_ThenStart_Resumes()
        {
            var (svc, time, _, _) = Make();
            svc.Start();
            time.Advance(600); // 10 min into focus
            svc.Pause();
            Assert.Equal(PomodoroState.Paused, svc.State);
            time.Advance(300); // 5 min paused
            svc.Start();
            Assert.Equal(PomodoroState.Focus, svc.State);
            // Elapsed should still be ~600 (paused time excluded)
            Assert.True(svc.ElapsedSeconds < 650);
        }

        [Fact]
        public void Tick_CompletesFocus_TransitionsToShortBreak()
        {
            var (svc, time, _, events) = Make();
            svc.Start();
            // Advance past focus duration (25 min = 1500s)
            time.Advance(1501);
            svc.Tick();
            Assert.Equal(PomodoroState.ShortBreak, svc.State);
        }

        [Fact]
        public void Tick_CompletesFourRounds_TransitionsToLongBreak()
        {
            var (svc, time, _, _) = Make();
            svc.Start();
            var settings = svc.Settings;
            double focusDur = settings.FocusMinutes * 60;
            double shortDur = settings.ShortBreakMinutes * 60;

            for (int i = 0; i < 4; i++)
            {
                time.Advance(focusDur + 1);
                svc.Tick(); // Focus → ShortBreak (or LongBreak on round 4)
                if (i < 3)
                {
                    Assert.Equal(PomodoroState.ShortBreak, svc.State);
                    time.Advance(shortDur + 1);
                    svc.Tick(); // ShortBreak → Focus
                    Assert.Equal(PomodoroState.Focus, svc.State);
                }
            }
            Assert.Equal(PomodoroState.LongBreak, svc.State);
        }

        [Fact]
        public void RemainingSeconds_Decreases_OverTime()
        {
            var (svc, time, _, _) = Make();
            svc.Start();
            double initial = svc.RemainingSeconds;
            time.Advance(60);
            double after = svc.RemainingSeconds;
            Assert.True(after < initial);
            Assert.True(initial - after > 55 && initial - after < 65);
        }

        [Fact]
        public void ElapsedSeconds_Increases_OverTime()
        {
            var (svc, time, _, _) = Make();
            svc.Start();
            time.Advance(120);
            Assert.True(svc.ElapsedSeconds > 115 && svc.ElapsedSeconds < 125);
        }

        [Fact]
        public void Start_AlreadyRunning_DoesNothing()
        {
            var (svc, _, _, events) = Make();
            svc.Start();
            events.Clear();
            svc.Start(); // should not re-trigger
            Assert.Empty(events);
        }

        [Fact]
        public void Pause_WhenIdle_DoesNothing()
        {
            var (svc, _, _, events) = Make();
            svc.Pause();
            Assert.Equal(PomodoroState.Idle, svc.State);
            Assert.Empty(events);
        }

        [Fact]
        public void UpdateSettings_ChangesDurations()
        {
            var (svc, _, _, _) = Make();
            svc.UpdateSettings(new PomodoroSettings { FocusMinutes = 10, ShortBreakMinutes = 2, LongBreakMinutes = 5 });
            svc.Start();
            Assert.Equal(600, svc.CurrentPhaseDurationSeconds); // 10 min
        }

        [Fact]
        public void AutoContinue_False_StopsAfterBreak()
        {
            var (svc, time, _, _) = Make();
            svc.UpdateSettings(new PomodoroSettings
            {
                FocusMinutes = 1,
                ShortBreakMinutes = 1,
                LongBreakMinutes = 1,
                RoundsBeforeLongBreak = 1,
                AutoContinue = false
            });
            svc.Start();
            time.Advance(61); // focus done
            svc.Tick();
            Assert.Equal(PomodoroState.LongBreak, svc.State);
            time.Advance(61); // break done
            svc.Tick();
            Assert.Equal(PomodoroState.Idle, svc.State);
        }

        [Fact]
        public void Tick_WhenIdle_DoesNothing()
        {
            var (svc, _, _, events) = Make();
            svc.Tick();
            Assert.Equal(PomodoroState.Idle, svc.State);
            Assert.Empty(events);
        }

        [Fact]
        public void Tick_WhenPaused_DoesNothing()
        {
            var (svc, time, _, _) = Make();
            svc.Start();
            svc.Pause();
            time.Advance(9999);
            svc.Tick();
            Assert.Equal(PomodoroState.Paused, svc.State);
        }

        [Fact]
        public void Round_Increments_AfterEachFocus()
        {
            var (svc, time, _, _) = Make();
            svc.UpdateSettings(new PomodoroSettings
            {
                FocusMinutes = 0.1, // 6 seconds
                ShortBreakMinutes = 0.1,
                LongBreakMinutes = 0.1,
                RoundsBeforeLongBreak = 99
            });
            svc.Start();
            Assert.Equal(1, svc.Round);
            time.Advance(7);
            svc.Tick(); // focus → short break
            Assert.Equal(2, svc.Round);
        }
    }

    /// <summary>
    /// Tests for SpeechBubbleService.
    /// </summary>
    public class SpeechBubbleTests
    {
        [Fact]
        public void Show_SetsCurrentText()
        {
            var svc = new SpeechBubbleService();
            svc.Show("Focus time!", 3.0);
            Assert.Equal("Focus time!", svc.CurrentText);
            Assert.True(svc.IsVisible);
        }

        [Fact]
        public void Hide_ClearsCurrentText()
        {
            var svc = new SpeechBubbleService();
            svc.Show("Break time!", 3.0);
            svc.Hide();
            Assert.Null(svc.CurrentText);
            Assert.False(svc.IsVisible);
        }

        [Fact]
        public void Tick_AutoHides_AfterDuration()
        {
            var svc = new SpeechBubbleService();
            svc.Show("Hello!", 2.0);
            svc.Tick(1.5);
            Assert.True(svc.IsVisible);
            svc.Tick(0.6);
            Assert.False(svc.IsVisible);
        }

        [Fact]
        public void Show_FiresShowRequestedEvent()
        {
            var svc = new SpeechBubbleService();
            string? receivedText = null;
            double receivedDur = 0;
            svc.ShowRequested += (text, dur) => { receivedText = text; receivedDur = dur; };
            svc.Show("Test", 5.0);
            Assert.Equal("Test", receivedText);
            Assert.Equal(5.0, receivedDur);
        }

        [Fact]
        public void Hide_FiresHideRequestedEvent()
        {
            var svc = new SpeechBubbleService();
            bool fired = false;
            svc.HideRequested += () => fired = true;
            svc.Show("Test", 1.0);
            svc.Hide();
            Assert.True(fired);
        }

        [Fact]
        public void Show_EmptyText_Throws()
        {
            var svc = new SpeechBubbleService();
            Assert.Throws<ArgumentException>(() => svc.Show(""));
            Assert.Throws<ArgumentException>(() => svc.Show("   "));
        }
    }
}