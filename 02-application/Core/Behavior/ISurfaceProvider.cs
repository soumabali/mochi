using System;
using MochiV2.Core.Models;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Enumerates walkable surfaces (visible window title bars) the cat can
    /// traverse. Post-MVP Phase E (PRD §5: window-top walking).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations poll the desktop for visible top-level windows, filter to
    /// those with usable title bars, and return <see cref="WalkableSurface"/>[]
    /// ordered left-to-right. The <see cref="SurfacesChanged"/> event fires when
    /// the set of surfaces changes (window opened/closed/moved); subscribers
    /// (MovementService, SurfaceClimber) react accordingly.
    /// </para>
    /// <para>
    /// Platform note: the interface lives in Core (platform-agnostic) so it can
    /// be mocked in tests on Linux. The Win32-backed implementation lives in
    /// Infrastructure/Window behind <c>#if WINDOWS</c>.
    /// </para>
    /// </remarks>
    public interface ISurfaceProvider
    {
        /// <summary>
        /// Current snapshot of walkable surfaces, ordered left-to-right by
        /// <see cref="WalkableSurface.Left"/>. Returns an empty array when no
        /// suitable windows exist.
        /// </summary>
        WalkableSurface[] GetSurfaces();

        /// <summary>
        /// Raised when the surface set changes (window opened, closed, moved, or
        /// display settings changed). Subscribers should re-query
        /// <see cref="GetSurfaces"/>.
        /// </summary>
        event Action? SurfacesChanged;
    }
}