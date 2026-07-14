using System;
using System.Collections.Generic;
using MochiV2.Core.Behavior;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    public class G1FeatureTests
    {
        private class FakeTimeProvider : ITimeProvider
        {
            private double _elapsed;
            public double GetElapsedSeconds() => _elapsed;
            public void Advance(double seconds) => _elapsed += seconds;
        }

        //---- HydrationReminderService ----

        [Fact]
        public void Hydration_DoesNotTrigger_BeforeInterval()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new HydrationReminderService(time, bubble);
            svc.IntervalSeconds = 100;
            time.Advance(50);
            svc.Tick();
            Assert.False(bubble.IsVisible);
        }

        [Fact]
        public void Hydration_Triggers_AfterInterval()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new HydrationReminderService(time, bubble);
            svc.IntervalSeconds = 100;
            bool triggered = false;
            svc.ReminderTriggered += () => triggered = true;
            time.Advance(101);
            svc.Tick();
            Assert.True(triggered);
            Assert.True(bubble.IsVisible);
            Assert.Contains("Minum", bubble.CurrentText);
        }

        [Fact]
        public void Hydration_Disabled_DoesNotTrigger()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new HydrationReminderService(time, bubble) { Enabled = false };
            svc.IntervalSeconds = 10;
            time.Advance(100);
            svc.Tick();
            Assert.False(bubble.IsVisible);
        }

        [Fact]
        public void Hydration_Reset_PreventsNextTrigger()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new HydrationReminderService(time, bubble);
            svc.IntervalSeconds = 100;
            time.Advance(50);
            svc.Reset();
            time.Advance(50);
            svc.Tick();
            Assert.False(bubble.IsVisible);
        }

        //---- DailyQuoteService ----

        [Fact]
        public void DailyQuote_ShowRandomQuote_ReturnsText()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new DailyQuoteService(time, bubble);
            var quote = svc.ShowRandomQuote();
            Assert.False(string.IsNullOrEmpty(quote));
            Assert.True(bubble.IsVisible);
        }

        //---- MoodCheckInService ----

        [Fact]
        public void MoodCheckIn_Triggers_AfterInterval()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new MoodCheckInService(time, bubble);
            svc.IntervalSeconds = 100;
            bool triggered = false;
            svc.CheckInTriggered += () => triggered = true;
            time.Advance(101);
            svc.Tick();
            Assert.True(triggered);
            Assert.True(bubble.IsVisible);
            Assert.Contains("mood", bubble.CurrentText);
        }

        [Fact]
        public void MoodCheckIn_Respond_SetsLastMood()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new MoodCheckInService(time, bubble);
            svc.Respond("good");
            Assert.Equal("good", svc.LastMood);
            Assert.True(bubble.IsVisible);
        }

        [Fact]
        public void MoodCheckIn_Respond_Good_ShowsHappyMessage()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new MoodCheckInService(time, bubble);
            svc.Respond("good");
            Assert.Contains("senang", bubble.CurrentText);
        }

        [Fact]
        public void MoodCheckIn_Respond_Bad_ShowsComfortMessage()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new MoodCheckInService(time, bubble);
            svc.Respond("bad");
            Assert.Contains("sini", bubble.CurrentText);
        }

        [Fact]
        public void MoodCheckIn_Disabled_DoesNotTrigger()
        {
            var time = new FakeTimeProvider();
            var bubble = new SpeechBubbleService();
            var svc = new MoodCheckInService(time, bubble) { Enabled = false };
            svc.IntervalSeconds = 10;
            time.Advance(100);
            svc.Tick();
            Assert.False(bubble.IsVisible);
        }

        //---- QuickLauncherService ----

        [Fact]
        public void QuickLauncher_HasDefaultEntries()
        {
            var svc = new QuickLauncherService();
            Assert.True(svc.Entries.Count >= 3);
        }

        [Fact]
        public void QuickLauncher_LaunchByName_Invalid_ReturnsFalse()
        {
            var svc = new QuickLauncherService();
            Assert.False(svc.LaunchByName("Nonexistent App"));
        }

        [Fact]
        public void QuickLauncher_Launch_InvalidIndex_ReturnsFalse()
        {
            var svc = new QuickLauncherService();
            Assert.False(svc.Launch(-1));
            Assert.False(svc.Launch(999));
        }

        //---- HotkeyService ----

        [Fact]
        public void Hotkey_HasDefaultHotkeys()
        {
            var svc = new HotkeyService();
            var names = new List<string>(svc.GetRegisteredNames());
            Assert.Contains("teleport_cat", names);
            Assert.Contains("feed_cat", names);
            Assert.Contains("pet_cat", names);
            Assert.Contains("toggle_pomodoro", names);
        }

        [Fact]
        public void Hotkey_Simulate_FiresEvent()
        {
            var svc = new HotkeyService();
            string? fired = null;
            svc.HotkeyPressed += (name) => fired = name;
            svc.SimulateHotkey("teleport_cat");
            Assert.Equal("teleport_cat", fired);
        }

        [Fact]
        public void Hotkey_Simulate_Unknown_DoesNotFire()
        {
            var svc = new HotkeyService();
            bool fired = false;
            svc.HotkeyPressed += (_) => fired = true;
            svc.SimulateHotkey("nonexistent");
            Assert.False(fired);
        }

        [Fact]
        public void Hotkey_Register_AddsNewHotkey()
        {
            var svc = new HotkeyService();
            svc.Register("custom_key", 0x006, 0x41);
            var names = new List<string>(svc.GetRegisteredNames());
            Assert.Contains("custom_key", names);
        }
    }
}