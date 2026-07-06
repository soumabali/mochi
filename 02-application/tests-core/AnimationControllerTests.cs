using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MochiV2.Core.Animation;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-021: AnimationController playback mode tests.
    /// Validates holdFirstFrame, playOnce, loop, playOnceReversed,
    /// playOnceThenHoldLast, speed multiplier, and AnimationFinishedEvent
    /// publication via AnimationManager.
    /// </summary>
    public class AnimationControllerTests
    {
        //---- helpers -------------------------------------------------------

        /// <summary>
        /// Create frames list of N fake PNG paths: "frame_0.png" … "frame_{N-1}.png".
        /// </summary>
        private static List<string> MakeFrames(int count) =>
            Enumerable.Range(0, count)
                      .Select(i => $"frame_{i}.png")
                      .ToList();

        private const string FakeFolder = "/fake/sprite_folder";

        //------------------------------------------------------------------
        // holdFirstFrame
        //------------------------------------------------------------------

        [Fact]
        public void HoldFirstFrame_Stays_On_Frame_0()
        {
            var frames = MakeFrames(5);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.HoldFirstFrame, frames);

            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // Advance many seconds — should never leave frame 0
            ctrl.Update(1000);
            ctrl.Update(1000);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);
        }

        [Fact]
        public void HoldFirstFrame_Single_Frame_Never_Finishes()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.HoldFirstFrame, MakeFrames(1));
            ctrl.Update(5000);
            Assert.False(ctrl.IsFinished);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
        }

        //------------------------------------------------------------------
        // playOnce
        //------------------------------------------------------------------

        [Fact]
        public void PlayOnce_Advances_And_Finishes()
        {
            var frames = MakeFrames(4);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, frames);
            ctrl.Fps = 10; // 100 ms per frame

            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);

            // 100 ms → frame 1
            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            // 100 ms → frame 2
            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            // 100 ms → frame 3
            ctrl.Update(100);
            Assert.Equal(3, ctrl.CurrentFrameIndex);

            Assert.False(ctrl.IsFinished); // at last frame but not finished yet

            // 100 ms → finished
            ctrl.Update(100);
            Assert.True(ctrl.IsFinished);
            Assert.Equal(3, ctrl.CurrentFrameIndex); // stays on last
        }

        [Fact]
        public void PlayOnce_Finished_Controller_Stops_Advancing()
        {
            var frames = MakeFrames(3);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, frames);
            ctrl.Fps = 10;

            // Advance enough to finish
            ctrl.Update(1000);
            Assert.True(ctrl.IsFinished);

            int frameAtFinish = ctrl.CurrentFrameIndex;
            ctrl.Update(1000);
            Assert.Equal(frameAtFinish, ctrl.CurrentFrameIndex);
            Assert.True(ctrl.IsFinished);
        }

        //------------------------------------------------------------------
        // loop
        //------------------------------------------------------------------

        [Fact]
        public void Loop_Wraps_Around()
        {
            var frames = MakeFrames(3);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.Loop, frames);
            ctrl.Fps = 10; // 100 ms per frame

            Assert.Equal(0, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            // Wrap to 0
            ctrl.Update(100);
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // Continues wrapping
            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            Assert.False(ctrl.IsFinished); // loops never finish
        }

        [Fact]
        public void Loop_Single_Frame_Stays_On_Frame_0()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.Loop, MakeFrames(1));
            ctrl.Fps = 10;
            ctrl.Update(500);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);
        }

        //------------------------------------------------------------------
        // playOnceReversed
        //------------------------------------------------------------------

        [Fact]
        public void PlayOnceReversed_Plays_Backward_And_Finishes()
        {
            var frames = MakeFrames(4);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnceReversed, frames);
            ctrl.Fps = 10; // 100 ms per frame

            // Reversed mode starts at last frame
            Assert.Equal(3, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);

            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            Assert.False(ctrl.IsFinished); // at first frame, not finished yet

            ctrl.Update(100);
            Assert.True(ctrl.IsFinished);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void PlayOnceReversed_Reset_Goes_To_Last_Frame()
        {
            var frames = MakeFrames(5);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnceReversed, frames);
            ctrl.Fps = 10;

            //Advance partway: 200ms = 2 frame advances (4→3→2)
            ctrl.Update(200);
            Assert.Equal(2, ctrl.CurrentFrameIndex);

            ctrl.Reset();
            Assert.Equal(4, ctrl.CurrentFrameIndex); // last frame
            Assert.False(ctrl.IsFinished);
        }

        //------------------------------------------------------------------
        // playOnceThenHoldLast
        //------------------------------------------------------------------

        [Fact]
        public void PlayOnceThenHoldLast_Plays_Then_Holds_Last_Frame()
        {
            var frames = MakeFrames(3);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnceThenHoldLast, frames);
            ctrl.Fps = 10;

            Assert.Equal(0, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            ctrl.Update(100);
            Assert.Equal(2, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished); // at last but not finished

            ctrl.Update(100);
            Assert.True(ctrl.IsFinished);
            Assert.Equal(2, ctrl.CurrentFrameIndex); // holds last

            // Stays on last frame after finish
            ctrl.Update(1000);
            Assert.Equal(2, ctrl.CurrentFrameIndex);
            Assert.True(ctrl.IsFinished);
        }

        //------------------------------------------------------------------
        // Speed multiplier
        //------------------------------------------------------------------

        [Fact]
        public void Speed_Multiplier_2x_Advances_Twice_As_Fast()
        {
            var frames = MakeFrames(5);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, frames,
                                               speedMultiplier: 2.0);
            ctrl.Fps = 10; // base 100 ms, with 2x → 50 ms per frame

            // 50 ms should advance one frame
            ctrl.Update(50);
            Assert.Equal(1, ctrl.CurrentFrameIndex);

            // 100 ms should advance two frames
            ctrl.Update(100);
            Assert.Equal(3, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void Speed_Multiplier_HalfSpeed_Advances_Half_Rate()
        {
            var frames = MakeFrames(5);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, frames,
                                               speedMultiplier: 0.5);
            ctrl.Fps = 10; // base 100 ms, with 0.5x → 200 ms per frame

            // 100 ms should NOT advance a full frame
            ctrl.Update(100);
            Assert.Equal(0, ctrl.CurrentFrameIndex);

            // 200 ms total → one frame
            ctrl.Update(100);
            Assert.Equal(1, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void Default_Speed_Multiplier_Is_1()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, MakeFrames(3));
            Assert.Equal(1.0, ctrl.SpeedMultiplier);
        }

        [Fact]
        public void Negative_Speed_Multiplier_Clamped_To_1()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, MakeFrames(3),
                                               speedMultiplier: -5.0);
            Assert.Equal(1.0, ctrl.SpeedMultiplier);
        }

        [Fact]
        public void Zero_Speed_Multiplier_Clamped_To_1()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, MakeFrames(3),
                                               speedMultiplier: 0.0);
            Assert.Equal(1.0, ctrl.SpeedMultiplier);
        }

        //------------------------------------------------------------------
        // Reset
        //------------------------------------------------------------------

        [Fact]
        public void Reset_Forward_Modes_Goes_To_Frame_0()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, MakeFrames(5));
            ctrl.Fps = 10;
            ctrl.Update(300);
            Assert.Equal(3, ctrl.CurrentFrameIndex);

            ctrl.Reset();
            Assert.Equal(0, ctrl.CurrentFrameIndex);
            Assert.False(ctrl.IsFinished);
        }

        [Fact]
        public void Reset_Clears_IsFinished()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, MakeFrames(2));
            ctrl.Fps = 10;
            ctrl.Update(1000);
            Assert.True(ctrl.IsFinished);

            ctrl.Reset();
            Assert.False(ctrl.IsFinished);
        }

        //------------------------------------------------------------------
        // Edge cases
        //------------------------------------------------------------------

        [Fact]
        public void Empty_Frames_CurrentFramePath_Is_Empty()
        {
            var ctrl = new AnimationController(FakeFolder, SpriteMode.Loop, new List<string>());
            Assert.Equal(string.Empty, ctrl.CurrentFramePath);
            Assert.Equal(0, ctrl.TotalFrames);
        }

        [Fact]
        public void Single_Frame_PlayOnce_Never_Finishes()
        {
            // With only 1 frame, Update returns early (frames.Count <= 1)
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, MakeFrames(1));
            ctrl.Fps = 10;
            ctrl.Update(1000);
            Assert.False(ctrl.IsFinished);
            Assert.Equal(0, ctrl.CurrentFrameIndex);
        }

        [Fact]
        public void CurrentFramePath_Returns_Current_Frame_Path()
        {
            var frames = MakeFrames(3);
            var ctrl = new AnimationController(FakeFolder, SpriteMode.PlayOnce, frames);
            ctrl.Fps = 10;

            Assert.Equal("frame_0.png", ctrl.CurrentFramePath);

            ctrl.Update(100);
            Assert.Equal("frame_1.png", ctrl.CurrentFramePath);
        }

        [Fact]
        public void Constructor_Null_Folder_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AnimationController(null!, SpriteMode.Loop, MakeFrames(3)));
        }

        [Fact]
        public void Constructor_Null_Frames_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AnimationController(FakeFolder, SpriteMode.Loop, null!));
        }
    }
}