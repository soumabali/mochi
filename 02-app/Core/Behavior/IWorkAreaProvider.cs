namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Provides the usable desktop work-area rectangle (taskbar-aware).
    /// On Windows this wraps <c>SystemParameters.WorkArea</c>; in tests
    /// a fixed rectangle is injected so MovementService is deterministic.
    /// Coordinates are in logical (DPI-aware) pixels.
    /// </summary>
    public interface IWorkAreaProvider
    {
        /// <summary>Left edge X of the work area.</summary>
        double Left { get; }

        /// <summary>Top edge Y of the work area.</summary>
        double Top { get; }

        /// <summary>Right edge X (= Left + Width).</summary>
        double Right { get; }

        /// <summary>Bottom edge Y (= Top + Height).</summary>
        double Bottom { get; }

        /// <summary>Width of the work area.</summary>
        double Width { get; }

        /// <summary>Height of the work area.</summary>
        double Height { get; }
    }

    /// <summary>
    /// Configurable in-process work-area used by tests and headless runs.
    /// </summary>
    public sealed class WorkAreaRect : IWorkAreaProvider
    {
        public double Left { get; init; }
        public double Top { get; init; }
        public double Right { get; init; }
        public double Bottom { get; init; }
        public double Width => Right - Left;
        public double Height => Bottom - Top;

        public WorkAreaRect(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>Common test default: 1920x1080, no taskbar.</summary>
        public static WorkAreaRect Default1920x1080 => new(0, 0, 1920, 1080);
    }
}