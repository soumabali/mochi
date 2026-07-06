using MochiV2.Core.Models;

namespace MochiV2.Core.Animation
{
    /// <summary>
    /// Published on the <see cref="Events.EventBus"/> when an animation
    /// controller reaches its terminal frame (PlayOnce / PlayOnceReversed /
    /// PlayOnceThenHoldLast).  HoldFirstFrame and Loop never finish.
    /// </summary>
    public sealed record AnimationFinishedEvent(FSMState State);
}