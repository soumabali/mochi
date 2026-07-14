using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-008: Walk/idle/blink + screen-edge movement + procedural micro-motion tests.
    /// </summary>
    public class MovementTests
    {
        // ---- helpers / fakes -------------------------------------------------

        private sealed class FakeTime : ITimeProvider
        {
            public double Now { get; set; }
            public double GetElapsedSeconds() => Now;
        }

        private sealed class FakeRandom : IRandom
        {
            private readonly double[] _doubles;
            private readonly int[] _ints;
            private int _dIdx;
            private int _iIdx;

            public FakeRandom(double[] doubles, int[]? ints = null)
            {
                _doubles = doubles;
                _ints = ints ?? Array.Empty<int>();
            }

            public double NextDouble()
            {
                if (_dIdx < _doubles.Length) return _doubles[_dIdx++];
                return 0.5;
            }

            public int Next(int maxExclusive)
            {
                if (_iIdx < _ints.Length) return _ints[_iIdx++];
                return 0;
            }

            public int Next(int minInclusive, int maxExclusive)
            {
                if (_iIdx < _ints.Length) return _ints[_iIdx++];
                return minInclusive;
            }
        }

        private static (MovementService svc, WorkAreaRect area) Make(
            IRandom rng, double spriteW = 128, double? initialX = null,
            double width = 1920, double height = 1080)
        {
            var area = new WorkAreaRect(0, 0, width, height);
            var svc = new MovementService(area, rng, spriteW, initialX);
            return (svc, area);
        }

        // ---- walk tests ------------------------------------------------------

        [Fact]
        public void Walk_Right_Increases_X()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 100);
            Assert.Equal(Facing.Right, svc.Facing);

            svc.Walk(100, 1.0); // 100 px/s for 1s → +100
            Assert.Equal(200, svc.Position.X, precision: 1);
            Assert.Equal(Facing.Right, svc.Facing);
        }

        [Fact]
        public void Walk_Left_Decreases_X()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 500);
            // turn to left
            svc.Walk(0, 0, Facing.Left);
            Assert.Equal(Facing.Left, svc.Facing);

            svc.Walk(100, 2.0); // 100 px/s for 2s → -200
            Assert.Equal(300, svc.Position.X, precision: 1);
            Assert.Equal(Facing.Left, svc.Facing);
        }

        [Fact]
        public void Reaching_Right_Edge_TurnsAround_FacingBecomesLeft()
        {
            // WorkArea 0..1920, sprite 128 → maxX = 1792. Start near edge.
            var (svc, _) = Make(new FakeRandom(new[] { 0.99 }), initialX: 1750);

            // random=0.99 >= 0.15 → no sit+blink, just turn around
            var outcome = svc.Walk(100, 1.0); // would go to 1850 > 1792 → clamp + turn
            Assert.Equal(EdgeOutcome.TurnedAround, outcome);
            Assert.Equal(Facing.Left, svc.Facing);
            Assert.True(svc.Position.X <= 1920 - 128);
            Assert.True(svc.IsAtScreenEdge);
        }

        [Fact]
        public void Reaching_Left_Edge_TurnsAround_FacingBecomesRight()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.99 }), initialX: 20);
            svc.Walk(0, 0, Facing.Left);

            var outcome = svc.Walk(100, 1.0); // would go to -80 < 0 → clamp + turn
            Assert.Equal(EdgeOutcome.TurnedAround, outcome);
            Assert.Equal(Facing.Right, svc.Facing);
        }

        [Fact]
        public void Edge_WithLowRoll_SitAndBlink_NoTurnYet()
        {
            // random < 0.15 → SitAndBlink; facing should NOT flip yet
            var (svc, _) = Make(new FakeRandom(new[] { 0.05 }), initialX: 1750);

            var outcome = svc.Walk(100, 1.0);
            Assert.Equal(EdgeOutcome.SitAndBlink, outcome);
            Assert.Equal(Facing.Right, svc.Facing); // not turned yet

            // Caller later turns around explicitly
            svc.TurnAround();
            Assert.Equal(Facing.Left, svc.Facing);
        }

        [Fact]
        public void Edge_WithHighRoll_TurnsImmediately()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.14 }), initialX: 1750);
            // 0.14 < 0.15 → sit+blink
            Assert.Equal(EdgeOutcome.SitAndBlink, svc.Walk(100, 1.0));
        }

        [Fact]
        public void IsAtScreenEdge_TrueAtRightEdge()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.99 }), initialX: 1920 - 128);
            Assert.True(svc.IsAtScreenEdge);
        }

        [Fact]
        public void IsAtScreenEdge_TrueAtLeftEdge()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.99 }), initialX: 0);
            Assert.True(svc.IsAtScreenEdge);
        }

        [Fact]
        public void IsAtScreenEdge_FalseInMiddle()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.99 }), initialX: 500);
            Assert.False(svc.IsAtScreenEdge);
        }

        // ---- micro-motion: breathing ----------------------------------------

        [Fact]
        public void Breathing_ProducesSinusoidalScale_WithinAmplitude()
        {
            // At t=0, sin(0)=0 → scale=1.0
            double s0 = 1.0;
            MicroMotionService.ApplyBreathing(0.0, ref s0);
            Assert.Equal(1.0, s0, precision: 6);

            // Peak: at phase pi/2 → sin=1 → scale = 1+0.015
            // 2*pi*f*t = pi/2 → t = 1/(4f) = 1/(4*0.4) = 0.625s
            double sPeak = 1.0;
            MicroMotionService.ApplyBreathing(0.625, ref sPeak);
            Assert.Equal(1.0 + MicroMotionService.BreathingAmplitude, sPeak, precision: 4);

            // Trough: at phase 3pi/2 → sin=-1 → scale = 1-0.015
            // t = 3/(4f) = 3/(4*0.4) = 1.875s
            double sTrough = 1.0;
            MicroMotionService.ApplyBreathing(1.875, ref sTrough);
            Assert.Equal(1.0 - MicroMotionService.BreathingAmplitude, sTrough, precision: 4);
        }

        [Fact]
        public void Breathing_OscillatesOverTime()
        {
            // Sample multiple points; ensure we see values above and below 1.0
            double above = 0, below = 0;
            for (int i = 0; i < 100; i++)
            {
                double s = 1.0;
                MicroMotionService.ApplyBreathing(i * 0.1, ref s);
                if (s > 1.0) above++;
                else if (s < 1.0) below++;
            }
            Assert.True(above > 0, "Expected some samples above 1.0");
            Assert.True(below > 0, "Expected some samples below 1.0");
        }

        [Fact]
        public void Breathing_Amplitude_IsOneAndHalfPercent()
        {
            Assert.Equal(0.015, MicroMotionService.BreathingAmplitude);
        }

        [Fact]
        public void Breathing_Frequency_IsApproxFourTenthsHz()
        {
            Assert.Equal(0.4, MicroMotionService.BreathingFrequencyHz);
        }

        // ---- micro-motion: fidget interval ----------------------------------

        [Fact]
        public void FidgetInterval_IsWithinSixToTwentySeconds()
        {
            var time = new FakeTime { Now = 0 };
            // FakeRandom.NextDouble returns a value in [0,1). Use 0.5 → midpoint.
            var rng = new FakeRandom(new[] { 0.5 });
            var svc = new MicroMotionService(time, rng);

            double interval = svc.SampleFidgetInterval();
            Assert.InRange(interval,
                MicroMotionService.FidgetIntervalMin,
                MicroMotionService.FidgetIntervalMax);
        }

        [Fact]
        public void FidgetInterval_RangeBoundaries()
        {
            // The constructor consumes one double for the initial schedule,
            // so provide an extra. roll=0 → Min.
            var time = new FakeTime { Now = 0 };
            var rng0 = new FakeRandom(new[] { 0.0, 0.0 });
            var svc0 = new MicroMotionService(time, rng0);
            Assert.Equal(MicroMotionService.FidgetIntervalMin, svc0.SampleFidgetInterval(), precision: 6);

            // roll → 1 (not quite achievable; use 0.999...) → near Max
            var rng1 = new FakeRandom(new[] { 0.0, 0.999 });
            var svc1 = new MicroMotionService(time, rng1);
            Assert.InRange(svc1.SampleFidgetInterval(),
                MicroMotionService.FidgetIntervalMin,
                MicroMotionService.FidgetIntervalMax);
        }

        [Fact]
        public void GetFidgetEvent_ReturnsNullBeforeInterval()
        {
            var time = new FakeTime { Now = 0 };
            var rng = new FakeRandom(new[] { 0.5, 0 }); // interval ~13s, fidget index 0
            var svc = new MicroMotionService(time, rng);

            // Before interval elapses → null
            time.Now = 1.0;
            Assert.Null(svc.GetFidgetEvent());
        }

        [Fact]
        public void GetFidgetEvent_ReturnsFidgetAfterInterval()
        {
            var time = new FakeTime { Now = 0 };
            var rng = new FakeRandom(new[] { 0.0, 0 }); // interval = 6s, fidget=Blink
            var svc = new MicroMotionService(time, rng);

            time.Now = 7.0; // past 6s
            var fidget = svc.GetFidgetEvent();
            Assert.NotNull(fidget);
            Assert.Equal(FidgetType.Blink, fidget!.Value);
        }

        [Fact]
        public void GetFidgetEvent_ReturnsAllPoolTypes()
        {
            // Verify each index maps to the expected FidgetType
            foreach (FidgetType expected in Enum.GetValues<FidgetType>())
            {
                var time = new FakeTime { Now = 0 };
                // roll=0 → interval=6s, then Next(4) returns expected index
                var rng = new FakeRandom(new[] { 0.0 }, new[] { (int)expected });
                var svc = new MicroMotionService(time, rng);
                time.Now = 10.0;
                var f = svc.GetFidgetEvent();
                Assert.Equal(expected, f);
            }
        }

        [Fact]
        public void Fidget_ReschedulesAfterEmission()
        {
            var time = new FakeTime { Now = 0 };
            var rng = new FakeRandom(new[] { 0.0, 0, 0.0, 0 }); // two cycles
            var svc = new MicroMotionService(time, rng);

            time.Now = 7.0;
            Assert.NotNull(svc.GetFidgetEvent());

            // Immediately after, next fidget should be scheduled ~6s later
            Assert.True(svc.PeekNextFidgetAt() >= 7.0 + MicroMotionService.FidgetIntervalMin);
        }

        // ---- happy hop -------------------------------------------------------

        [Fact]
        public void HappyHop_StartAndEnd_AreGrounded()
        {
            var (dY0, _, _) = MicroMotionService.HappyHop(0.0);
            Assert.Equal(0, dY0, precision: 4);

            var (dY1, _, _) = MicroMotionService.HappyHop(1.0);
            Assert.Equal(0, dY1, precision: 4);
        }

        [Fact]
        public void HappyHop_Midpoint_IsAirborne()
        {
            var (dY, _, _) = MicroMotionService.HappyHop(0.5);
            Assert.True(dY < 0, "Expected dY < 0 (upward) at midpoint");
        }

        [Fact]
        public void HappyHop_AppliesSquashAtLaunch()
        {
            // At progress=0: apex=0 → squash=0.12 → scaleY = 1 - 0.12 = 0.88, scaleX = 1.12
            var (_, scaleX, scaleY) = MicroMotionService.HappyHop(0.0);
            Assert.True(scaleY < 1.0, "Expected squash (scaleY<1) at launch");
            Assert.True(scaleX > 1.0, "Expected stretch (scaleX>1) at launch");
        }
    }
}