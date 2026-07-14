using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Services;
using Xunit;

namespace MochiV2.Tests.Core
{
    public class G3FeatureTests
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

        //---- KeyboardReactionService ----

        [Fact]
        public void Keyboard_OnKeyPress_SetsTyping()
        {
            var time = new FakeTimeProvider();
            var svc = new KeyboardReactionService(time);
            svc.OnKeyPress();
            Assert.True(svc.IsTyping);
        }

        [Fact]
        public void Keyboard_TypingStops_After5s()
        {
            var time = new FakeTimeProvider();
            var svc = new KeyboardReactionService(time);
            svc.OnKeyPress();
            time.Advance(6);
            Assert.False(svc.IsTyping);
        }

        [Fact]
        public void Keyboard_TypingStarted_FiresOnce()
        {
            var time = new FakeTimeProvider();
            var svc = new KeyboardReactionService(time);
            int fired = 0;
            svc.TypingStarted += () => fired++;
            svc.OnKeyPress();
            svc.Tick();
            Assert.Equal(1, fired);
            svc.Tick(); // still typing, no re-fire
            Assert.Equal(1, fired);
        }

        [Fact]
        public void Keyboard_LongIdle_FiresAfter5Min()
        {
            var time = new FakeTimeProvider();
            var svc = new KeyboardReactionService(time);
            bool fired = false;
            svc.LongIdleDetected += () => fired = true;
            time.Advance(301);
            svc.Tick();
            Assert.True(fired);
        }

        [Fact]
        public void Keyboard_KeysPerMinute_CalculatedCorrectly()
        {
            var time = new FakeTimeProvider();
            var svc = new KeyboardReactionService(time);
            for (int i = 0; i < 100; i++)
                svc.OnKeyPress();
            time.Advance(60);
            Assert.True(svc.KeysPerMinute > 0);
        }

        //---- MiniBallGameService ----

        [Fact]
        public void BallGame_ThrowRandom_ActivatesBall()
        {
            var svc = new MiniBallGameService(new FakeRandom(new[] { 0.5 }));
            double chaseX = 0;
            svc.ChaseBall += x => chaseX = x;
            svc.ThrowRandom(500, 500);
            Assert.True(svc.IsBallActive);
            Assert.Equal(500, chaseX);
        }

        [Fact]
        public void BallGame_ThrowBall_SetsVelocity()
        {
            var svc = new MiniBallGameService(new FakeRandom(new[] { 0.5 }));
            svc.ThrowBall(100, 200, 300, -400);
            Assert.True(svc.IsBallActive);
            Assert.Equal(300, svc.BallVelocity.VelX);
            Assert.Equal(-400, svc.BallVelocity.VelY);
        }

        [Fact]
        public void BallGame_Tick_ApplyPhysics()
        {
            var svc = new MiniBallGameService(new FakeRandom(new[] { 0.5 }))
            {
                GroundY = 1000,
                BallGravity = 2000
            };
            svc.ThrowBall(500, 100, 0, 0);
            svc.Tick(0.1);
            // Gravity should have pulled ball down
            Assert.True(svc.BallVelocity.VelY > 0);
        }

        [Fact]
        public void BallGame_BallStops_OnGround()
        {
            var svc = new MiniBallGameService(new FakeRandom(new[] { 0.5 }))
            {
                GroundY = 500,
                BallGravity = 2000
            };
            bool caught = false;
            svc.BallCaught += () => caught = true;
            svc.ThrowBall(500, 400, 10, 0);
            // Tick many times to let ball settle
            for (int i = 0; i < 200; i++)
                svc.Tick(0.05);
            Assert.False(svc.IsBallActive);
            Assert.True(caught);
        }

        [Fact]
        public void BallGame_IsCatNearBall_DetectsProximity()
        {
            var svc = new MiniBallGameService(new FakeRandom(new[] { 0.5 }));
            svc.ThrowBall(500, 500, 0, -100);
            Assert.True(svc.IsCatNearBall(510, 510, 50));
            Assert.False(svc.IsCatNearBall(600, 600, 50));
        }

        [Fact]
        public void BallGame_RemoveBall_Deactivates()
        {
            var svc = new MiniBallGameService(new FakeRandom(new[] { 0.5 }));
            svc.ThrowBall(500, 500, 0, 0);
            svc.RemoveBall();
            Assert.False(svc.IsBallActive);
        }

        //---- WeatherService ----

        [Fact]
        public void Weather_CodeToDescription_KnownCodes()
        {
            Assert.Equal("Clear sky", WeatherService.CodeToDescription(0));
            Assert.Equal("Rain", WeatherService.CodeToDescription(61));
            Assert.Equal("Thunderstorm", WeatherService.CodeToDescription(95));
        }

        [Fact]
        public void Weather_CodeToMood_KnownCodes()
        {
            Assert.Equal("playful", WeatherService.CodeToMood(0));     // sunny
            Assert.Equal("tired", WeatherService.CodeToMood(61));     // rain
            Assert.Equal("sad", WeatherService.CodeToMood(95));       // storm
        }

        [Fact]
        public void Weather_CodeToDescription_UnknownCode()
        {
            Assert.Equal("Unknown", WeatherService.CodeToDescription(999));
        }

        [Fact]
        public void Weather_CodeToMood_UnknownCode()
        {
            Assert.Equal("neutral", WeatherService.CodeToMood(999));
        }
    }
}