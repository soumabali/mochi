using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    public class G2FeatureTests
    {
        private class FakeTimeProvider : ITimeProvider
        {
            private double _elapsed;
            public double GetElapsedSeconds() => _elapsed;
            public void Advance(double s) => _elapsed += s;
        }

        private class FakeRandom : IRandom
        {
            private readonly double[] _vals;
            private int _i;
            public FakeRandom(double[] v) => _vals = v;
            public double NextDouble() => _vals[_i++ % _vals.Length];
            public int Next(int max) => (int)(_vals[_i++ % _vals.Length] * max);
            public int Next(int min, int max) => min + (int)(_vals[_i++ % _vals.Length] * (max - min));
        }

        //---- ScreenEdgePeekService ----

        [Fact]
        public void Peek_DoesNotTrigger_BeforeInterval()
        {
            var time = new FakeTimeProvider();
            var svc = new ScreenEdgePeekService(time, new FakeRandom(new[] { 0.5 }));
            svc.PeekIntervalSeconds = 100;
            time.Advance(50);
            svc.Tick();
            Assert.False(svc.IsPeeking);
        }

        [Fact]
        public void Peek_Triggers_AfterInterval()
        {
            var time = new FakeTimeProvider();
            var svc = new ScreenEdgePeekService(time, new FakeRandom(new[] { 0.3 }));
            svc.PeekIntervalSeconds = 100;
            bool started = false;
            svc.PeekStarted += _ => started = true;
            time.Advance(101);
            svc.Tick();
            Assert.True(started);
            Assert.True(svc.IsPeeking);
        }

        [Fact]
        public void Peek_Ends_AfterDuration()
        {
            var time = new FakeTimeProvider();
            var svc = new ScreenEdgePeekService(time, new FakeRandom(new[] { 0.3 }));
            svc.PeekIntervalSeconds = 100;
            bool ended = false;
            svc.PeekEnded += () => ended = true;
            time.Advance(101);
            svc.Tick(); // start peek
            Assert.True(svc.IsPeeking);
            time.Advance(6); // > 5s duration
            svc.Tick();
            Assert.False(svc.IsPeeking);
            Assert.True(ended);
        }

        [Fact]
        public void Peek_Cancel_StopsPeeking()
        {
            var time = new FakeTimeProvider();
            var svc = new ScreenEdgePeekService(time, new FakeRandom(new[] { 0.3 }));
            svc.PeekIntervalSeconds = 100;
            time.Advance(101);
            svc.Tick();
            Assert.True(svc.IsPeeking);
            svc.Cancel();
            Assert.False(svc.IsPeeking);
        }

        [Fact]
        public void Peek_Disabled_DoesNotTrigger()
        {
            var time = new FakeTimeProvider();
            var svc = new ScreenEdgePeekService(time, new FakeRandom(new[] { 0.5 })) { Enabled = false };
            svc.PeekIntervalSeconds = 10;
            time.Advance(100);
            svc.Tick();
            Assert.False(svc.IsPeeking);
        }

        //---- PurrService ----

        [Fact]
        public void Purr_Starts_AfterThreshold()
        {
            var svc = new PurrService { PurrThreshold = 3.0 };
            bool started = false;
            svc.PurrStarted += () => started = true;
            svc.TickPetting(2.0);
            Assert.False(started);
            svc.TickPetting(1.5);
            Assert.True(started);
            Assert.True(svc.IsPurring);
        }

        [Fact]
        public void Purr_Stops_WhenPettingStops()
        {
            var svc = new PurrService { PurrThreshold = 1.0 };
            bool stopped = false;
            svc.PurrStopped += () => stopped = true;
            svc.TickPetting(2.0);
            Assert.True(svc.IsPurring);
            svc.StopPetting();
            Assert.False(svc.IsPurring);
            Assert.True(stopped);
        }

        [Fact]
        public void Purr_DoesNotStart_BelowThreshold()
        {
            var svc = new PurrService { PurrThreshold = 5.0 };
            svc.TickPetting(2.0);
            Assert.False(svc.IsPurring);
        }

        //---- ItemDropService ----

        [Fact]
        public void ItemDrop_DoesNotTrigger_BeforeInterval()
        {
            var time = new FakeTimeProvider();
            var svc = new ItemDropService(time, new FakeRandom(new[] { 0.5 }));
            svc.DropIntervalSeconds = 100;
            time.Advance(50);
            svc.Tick(500, 500);
            // no event expected
        }

        [Fact]
        public void ItemDrop_Triggers_AfterInterval()
        {
            var time = new FakeTimeProvider();
            var svc = new ItemDropService(time, new FakeRandom(new[] { 0.1, 0.5 }));
            svc.DropIntervalSeconds = 100;
            ItemDropService.ItemType? dropped = null;
            svc.ItemDropped += (type, _, _) => dropped = type;
            time.Advance(101);
            svc.Tick(500, 500);
            Assert.NotNull(dropped);
        }

        [Fact]
        public void ItemDrop_Disabled_DoesNotTrigger()
        {
            var time = new FakeTimeProvider();
            var svc = new ItemDropService(time, new FakeRandom(new[] { 0.1 })) { Enabled = false };
            svc.DropIntervalSeconds = 10;
            time.Advance(100);
            bool dropped = false;
            svc.ItemDropped += (_, _, _) => dropped = true;
            svc.Tick(500, 500);
            Assert.False(dropped);
        }

        [Fact]
        public void ItemDrop_GetItemXP_ReturnsPositive()
        {
            Assert.True(ItemDropService.GetItemXP(ItemDropService.ItemType.Fish) > 0);
            Assert.True(ItemDropService.GetItemXP(ItemDropService.ItemType.Star) > 0);
            Assert.True(ItemDropService.GetItemXP(ItemDropService.ItemType.Star) > ItemDropService.GetItemXP(ItemDropService.ItemType.Coin));
        }

        [Fact]
        public void ItemDrop_DoesNotTrigger_WhenRandomMisses()
        {
            var time = new FakeTimeProvider();
            var svc = new ItemDropService(time, new FakeRandom(new[] { 0.9 })); // > 0.3 chance = no drop
            svc.DropIntervalSeconds = 100;
            bool dropped = false;
            svc.ItemDropped += (_, _, _) => dropped = true;
            time.Advance(101);
            svc.Tick(500, 500);
            Assert.False(dropped);
        }
    }
}