using System;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Abstraction over <see cref="Random"/> for testability.
    /// Inject a deterministic implementation in tests to assert
    /// probability-gated branches (e.g. 15% sit+blink at screen edge).
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a non-negative double in [0, 1).</summary>
        double NextDouble();

        /// <summary>Returns a non-negative integer in [0, <paramref name="maxExclusive"/>).</summary>
        int Next(int maxExclusive);

        /// <summary>Returns an integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>
    /// Default <see cref="IRandom"/> backed by <see cref="System.Random"/>.
    /// </summary>
    public sealed class StandardRandom : IRandom
    {
        private readonly Random _rng;

        public StandardRandom() => _rng = new();
        public StandardRandom(int seed) => _rng = new(seed);

        public double NextDouble() => _rng.NextDouble();
        public int Next(int maxExclusive) => _rng.Next(maxExclusive);
        public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
    }
}