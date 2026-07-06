using System;

namespace MochiV2.Core.Models
{
    /// <summary>
    /// A horizontal surface Mochi can walk on — typically the title-bar top edge
    /// of a visible desktop window (Post-MVP Phase E).
    /// Coordinates are logical (DPI-aware) pixels, matching <see cref="Position"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Left"/> and <see cref="Right"/> delimit the horizontal extent of
    /// the surface; <see cref="Top"/> is the Y of the top edge (the cat's feet
    /// rest here, so the sprite's top = Top - spriteHeight). <see cref="SurfaceHandle"/>
    /// is the native window handle (HWND) for tracking/sticky updates; on
    /// non-Windows platforms it is <see cref="IntPtr.Zero"/>.
    /// </para>
    /// <para>
    /// Immutable value type; equality is structural so cached lists compare
    /// correctly when polling for changes.
    /// </para>
    /// </remarks>
    public readonly struct WalkableSurface : IEquatable<WalkableSurface>
    {
        /// <summary>Left edge X (sprite clamps ≥ this when walking the surface).</summary>
        public double Left { get; }

        /// <summary>Top edge Y — sprite feet rest here (sprite top = Top - spriteHeight).</summary>
        public double Top { get; }

        /// <summary>Right edge X (sprite clamps ≤ Right - spriteWidth).</summary>
        public double Right { get; }

        /// <summary>Native window handle (HWND); <see cref="IntPtr.Zero"/> on non-Windows.</summary>
        public IntPtr SurfaceHandle { get; }

        /// <summary>Horizontal width of the surface (Right - Left).</summary>
        public double Width => Right - Left;

        /// <summary>Create a walkable surface.</summary>
        public WalkableSurface(double left, double top, double right, IntPtr surfaceHandle)
        {
            Left = left;
            Top = top;
            Right = right;
            SurfaceHandle = surfaceHandle;
        }

        /// <summary>True when <paramref name="x"/> is within [Left, Right - spriteWidth].</summary>
        public bool ContainsX(double x, double spriteWidth) =>
            x >= Left && x <= Right - spriteWidth;

        /// <summary>Structural equality.</summary>
        public bool Equals(WalkableSurface other) =>
            Left == other.Left && Top == other.Top && Right == other.Right &&
            SurfaceHandle == other.SurfaceHandle;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is WalkableSurface s && Equals(s);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, SurfaceHandle);

        public static bool operator ==(WalkableSurface a, WalkableSurface b) => a.Equals(b);
        public static bool operator !=(WalkableSurface a, WalkableSurface b) => !a.Equals(b);

        /// <inheritdoc/>
        public override string ToString() =>
            $"Surface[Left={Left:F1}, Top={Top:F1}, Right={Right:F1}, HWND={SurfaceHandle}]";
    }
}