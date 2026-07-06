using System;
using System.Collections.Generic;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-021: MoodResolver tests — priority-based mood resolution,
    /// hysteresis (60 s), and MoodChangedEvent publication.
    /// PRD §6.3, AC-8.
    /// </summary>
    public class MoodResolverTests
    {
        //---- helpers / fakes -------------------------------------------------

        private sealed class FakeTime : ITimeProvider
        {
            public double Now { get; set; }
            public double GetElapsedSeconds() => Now;
        }

        private static (MoodResolver resolver, FakeTime time, EventBus bus, List<MoodChangedEvent> events) Make()
        {
            var time = new FakeTime();
            var bus = new EventBus();
            var events = new List<MoodChangedEvent>();
            bus.Subscribe<MoodChangedEvent>(e => events.Add(e));
            var resolver = new MoodResolver(time, bus);
            return (resolver, time, bus, events);
        }

        //------------------------------------------------------------------
        // ResolveMood static — threshold tests (pure function)
        //------------------------------------------------------------------

        [Fact]
        public void ResolveMood_HungryCritical_When_Food_Below_20()
        {
            Assert.Equal(MoodResolver.MoodHungryCritical,
                MoodResolver.ResolveMood(food: 19, energy: 100, happiness: 100));
            Assert.Equal(MoodResolver.MoodHungryCritical,
                MoodResolver.ResolveMood(food: 0, energy: 100, happiness: 100));
        }

        [Fact]
        public void ResolveMood_HungryStandard_When_Food_Below_40()
        {
            Assert.Equal(MoodResolver.MoodHungryStandard,
                MoodResolver.ResolveMood(food: 39, energy: 100, happiness: 100));
            Assert.Equal(MoodResolver.MoodHungryStandard,
                MoodResolver.ResolveMood(food: 20, energy: 100, happiness: 100));
        }

        [Fact]
        public void ResolveMood_Tired_When_Energy_Below_20()
        {
            Assert.Equal(MoodResolver.MoodTired,
                MoodResolver.ResolveMood(food: 50, energy: 19, happiness: 100));
            Assert.Equal(MoodResolver.MoodTired,
                MoodResolver.ResolveMood(food: 50, energy: 0, happiness: 100));
        }

        [Fact]
        public void ResolveMood_Sad_When_Happiness_Below_30()
        {
            Assert.Equal(MoodResolver.MoodSad,
                MoodResolver.ResolveMood(food: 50, energy: 50, happiness: 29));
            Assert.Equal(MoodResolver.MoodSad,
                MoodResolver.ResolveMood(food: 50, energy: 50, happiness: 0));
        }

        [Fact]
        public void ResolveMood_Content_When_All_Needs_Sufficient()
        {
            Assert.Equal(MoodResolver.MoodContent,
                MoodResolver.ResolveMood(food: 40, energy: 20, happiness: 30));
            Assert.Equal(MoodResolver.MoodContent,
                MoodResolver.ResolveMood(food: 100, energy: 100, happiness: 100));
        }

        //------------------------------------------------------------------
        // Priority order
        //------------------------------------------------------------------

        [Fact]
        public void ResolveMood_HungryCritical_Has_Higher_Priority_Than_Tired()
        {
            // food<20 AND energy<20 → HungryCritical wins
            Assert.Equal(MoodResolver.MoodHungryCritical,
                MoodResolver.ResolveMood(food: 10, energy: 10, happiness: 100));
        }

        [Fact]
        public void ResolveMood_HungryStandard_Has_Higher_Priority_Than_Tired()
        {
            // food<40 (but >=20) AND energy<20 → HungryStandard wins
            Assert.Equal(MoodResolver.MoodHungryStandard,
                MoodResolver.ResolveMood(food: 25, energy: 10, happiness: 100));
        }

        [Fact]
        public void ResolveMood_Tired_Has_Higher_Priority_Than_Sad()
        {
            // food ok, energy<20 AND happiness<30 → Tired wins
            Assert.Equal(MoodResolver.MoodTired,
                MoodResolver.ResolveMood(food: 50, energy: 10, happiness: 10));
        }

        [Fact]
        public void ResolveMood_Boundary_Food_20_Is_Standard_Not_Critical()
        {
            // food==20 is NOT < 20, so falls to HungryStandard (< 40)
            Assert.Equal(MoodResolver.MoodHungryStandard,
                MoodResolver.ResolveMood(food: 20, energy: 100, happiness: 100));
        }

        [Fact]
        public void ResolveMood_Boundary_Food_40_Is_Content()
        {
            // food==40 is NOT < 40, so falls through to Tired/Sad/Content
            Assert.Equal(MoodResolver.MoodContent,
                MoodResolver.ResolveMood(food: 40, energy: 100, happiness: 100));
        }

        [Fact]
        public void ResolveMood_Boundary_Energy_20_Is_Content()
        {
            // energy==20 is NOT < 20
            Assert.Equal(MoodResolver.MoodContent,
                MoodResolver.ResolveMood(food: 50, energy: 20, happiness: 100));
        }

        [Fact]
        public void ResolveMood_Boundary_Happiness_30_Is_Content()
        {
            // happiness==30 is NOT < 30
            Assert.Equal(MoodResolver.MoodContent,
                MoodResolver.ResolveMood(food: 50, energy: 50, happiness: 30));
        }

        //------------------------------------------------------------------
        // Hysteresis (60 s)
        //------------------------------------------------------------------

        [Fact]
        public void Hysteresis_Prevents_Rapid_Mood_Change_Within_60s()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 0;
            // Start at Content (default)
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);

            // Publish a needs tick that should resolve to Sad
            time.Now = 1;
            bus.Publish(new NeedsTickEvent(50, 50, 10));

            // Mood should NOT change immediately (within 60s window)
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
            Assert.Empty(events); // no MoodChangedEvent published
        }

        [Fact]
        public void Hysteresis_Allows_Mood_Change_After_60s()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 0;

            // Publish needs tick that should resolve to Sad
            time.Now = 1;
            bus.Publish(new NeedsTickEvent(50, 50, 10));
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);

            // Advance past 60s and tick
            time.Now = 62;
            resolver.Tick();

            Assert.Equal(MoodResolver.MoodSad, resolver.CurrentMood);
            Assert.Single(events);
            Assert.Equal(MoodResolver.MoodContent, events[0].OldMood);
            Assert.Equal(MoodResolver.MoodSad, events[0].NewMood);
        }

        [Fact]
        public void Hysteresis_Pending_Mood_Cleared_When_Needs_Stabilize()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 0;

            // Trigger a pending mood change
            time.Now = 1;
            bus.Publish(new NeedsTickEvent(50, 50, 10)); // → Sad pending
            Assert.Empty(events);

            // Needs go back to Content before hysteresis elapses
            time.Now = 10;
            bus.Publish(new NeedsTickEvent(50, 50, 80)); // → Content (same as current)

            // Even after 60s, no change should occur (pending was cleared)
            time.Now = 100;
            resolver.Tick();
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
            Assert.Empty(events);
        }

        [Fact]
        public void Hysteresis_Change_After_60s_Via_NeedsTick()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 0;

            // First tick at t=1 → pending
            time.Now = 1;
            bus.Publish(new NeedsTickEvent(50, 50, 10));

            // Second tick at t=61 → hysteresis satisfied, change applied
            time.Now = 61;
            bus.Publish(new NeedsTickEvent(50, 50, 10));

            Assert.Equal(MoodResolver.MoodSad, resolver.CurrentMood);
            Assert.Single(events);
        }

        //------------------------------------------------------------------
        // Recalculate (bypasses hysteresis)
        //------------------------------------------------------------------

        [Fact]
        public void Recalculate_Bypasses_Hysteresis_And_Changes_Immediately()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 0;

            // Recalculate should change immediately without waiting 60s
            resolver.Recalculate(10, 100, 100);

            Assert.Equal(MoodResolver.MoodHungryCritical, resolver.CurrentMood);
            Assert.Single(events);
        }

        [Fact]
        public void Recalculate_No_Event_When_Mood_Unchanged()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 0;

            resolver.Recalculate(80, 80, 80); // Content == current

            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
            Assert.Empty(events);
        }

        //------------------------------------------------------------------
        // Constructor / disposal
        //------------------------------------------------------------------

        [Fact]
        public void Constructor_Null_Time_Throws()
        {
            var bus = new EventBus();
            Assert.Throws<ArgumentNullException>(() => new MoodResolver(null!, bus));
        }

        [Fact]
        public void Constructor_Null_Bus_Throws()
        {
            var time = new FakeTime();
            Assert.Throws<ArgumentNullException>(() => new MoodResolver(time, null!));
        }

        [Fact]
        public void Default_Mood_Is_Content()
        {
            var (resolver, _, _, _) = Make();
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
        }

        [Fact]
        public void Dispose_Unsubscribes_From_NeedsTickEvent()
        {
            var (resolver, time, bus, events) = Make();
            time.Now = 100; // past hysteresis

            resolver.Dispose();

            // After dispose, publishing NeedsTickEvent should not produce mood change
            bus.Publish(new NeedsTickEvent(0, 0, 0));
            Assert.Equal(MoodResolver.MoodContent, resolver.CurrentMood);
            Assert.Empty(events);
        }
    }
}