namespace MochiV2.Core.Models
{
    /// <summary>
    /// Mochi's position on the desktop work area (logical pixels, DPI-aware).
    /// PRD §7.2 / §5: MVP is bottom-edge only — X varies, Y is anchored to
    /// the bottom of the work area (taskbar-aware).
    /// </summary>
    public readonly struct Position
    {
        /// <summary>Horizontal coordinate (left = 0, increases rightward).</summary>
        public double X { get; }

        /// <summary>Vertical coordinate (top = 0, increases downward).</summary>
        public double Y { get; }

        /// <summary>Current facing direction.</summary>
        public Facing Facing { get; }

        public Position(double x, double y, Facing facing)
        {
            X = x;
            Y = y;
            Facing = facing;
        }

        public Position WithX(double x) => new(x, Y, Facing);
        public Position WithY(double y) => new(X, y, Facing);
        public Position WithFacing(Facing facing) => new(X, Y, facing);

        public static Position Zero => new(0, 0, Facing.Right);

        public override string ToString() => $"({X:F1}, {Y:F1}, {Facing})";
    }
}