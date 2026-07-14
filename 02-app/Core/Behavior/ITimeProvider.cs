namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Provides elapsed seconds for time-based procedural motion. Inject a
    /// deterministic provider in tests to assert sinusoidal breathing values
    /// and fidget scheduling.
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>Seconds since an arbitrary epoch, monotonically increasing.</summary>
        double GetElapsedSeconds();
    }

    /// <summary>
    /// Uses <see cref="System.Diagnostics.Stopwatch"/> for elapsed time.
    /// </summary>
    public sealed class StopwatchTimeProvider : ITimeProvider
    {
        private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        public double GetElapsedSeconds() => _sw.Elapsed.TotalSeconds;
    }
}