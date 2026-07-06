using System;
using System.Collections.Generic;
using SkiaSharp;
using Serilog;

namespace MochiV2.Core.Particles
{
    /// <summary>
    /// A single active particle. PRD §7.4: each particle has position,
    /// velocity, lifetime, color, size, and shape type.
    /// </summary>
    public sealed class Particle
    {
        /// <summary>Current position in logical pixels (y+ = down).</summary>
        public SKPoint Position { get; set; }

        /// <summary>Current velocity in px/s.</summary>
        public SKPoint Velocity { get; set; }

        /// <summary>Constant acceleration in px/s² (e.g. gravity / float-up).</summary>
        public SKPoint Acceleration { get; set; }

        /// <summary>Remaining lifetime in seconds (counts down to 0).</summary>
        public double RemainingLifetime { get; set; }

        /// <summary>Initial lifetime — used to compute fade alpha.</summary>
        public double TotalLifetime { get; set; }

        /// <summary>Base color (alpha overridden by fade at draw time).</summary>
        public SKColor Color { get; set; }

        /// <summary>Radius / half-size in logical pixels.</summary>
        public float Size { get; set; }

        /// <summary>Shape to draw.</summary>
        public ParticleShape Shape { get; set; }

        /// <summary>Rotation in radians (used by Zzz / dust drift).</summary>
        public float Rotation { get; set; }

        /// <summary>Angular velocity in rad/s.</summary>
        public float AngularVelocity { get; set; }

        /// <summary>True once <see cref="RemainingLifetime"/> ≤ 0.</summary>
        public bool IsDead => RemainingLifetime <= 0;

        /// <summary>Normalised age (0 = just born, 1 = expired).</summary>
        public float AgeFraction =>
            TotalLifetime <= 0 ? 1f : (float)(1.0 - RemainingLifetime / TotalLifetime);
    }

    /// <summary>
    /// Single particle system managing the four PRD §7.4 emitter types
    /// (Hearts, Zzz, Exclamation, Dust). All particles are vector-drawn via
    /// SkiaSharp — no image assets (PRD §0 asset-lock, FR-18, DESIGN D-8).
    ///
    /// <para><b>Lifecycle:</b> <see cref="Update"/> advances every active
    /// particle by dt seconds; <see cref="Draw"/> renders all active particles
    /// onto a supplied <see cref="SKCanvas"/>. Emitters are triggered
    /// individually via <see cref="EmitHearts"/>, <see cref="EmitZzz"/>,
    /// <see cref="EmitSurprised"/>, <see cref="EmitDust"/>.</para>
    ///
    /// <para><b>Periodic Zzz:</b> while sleeping, the system auto-emits a Zzz
    /// particle every <see cref="EmitterPresets.Zzz"/>.EmitInterval seconds.
    /// Call <see cref="StartZzzEmitting"/> / <see cref="StopZzzEmitting"/> to
    /// gate the periodic emitter.</para>
    /// </summary>
    public sealed class ParticleSystem
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<ParticleSystem>();

        private readonly List<Particle> _particles = new(64);
        private readonly Random _rng;

        // Periodic Zzz emitter state.
        private bool _zzzEmitting;
        private double _zzzAccumulator;
        private SKPoint _zzzOrigin;

        /// <summary>
        /// Origin (top-centre of sprite) used for periodic Zzz emission.
        /// Updated by the caller (e.g. render loop) via <see cref="SetEmitOrigin"/>.
        /// </summary>
        private SKPoint _emitOrigin;

        /// <summary>
        /// Create a particle system with a deterministic RNG seed (for tests).
        /// </summary>
        public ParticleSystem(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Number of currently active particles.</summary>
        public int ActiveCount => _particles.Count;

        /// <summary>
        /// Set the global emit origin (typically the sprite's head/top-centre)
        /// used when an emitter call does not supply an explicit origin.
        /// </summary>
        public void SetEmitOrigin(SKPoint origin)
        {
            _emitOrigin = origin;
            _zzzOrigin = origin;
        }

        //───────────────────────── Emitters ─────────────────────────

        /// <summary>
        /// Emit <paramref name="count"/> heart particles (fed / petted).
        /// Float upward, fade out. PRD §7.4 / FR-18.
        /// </summary>
        public void EmitHearts(int count, SKPoint? origin = null)
        {
            if (count <= 0) return;
            var cfg = EmitterPresets.Hearts;
            var at = origin ?? _emitOrigin;
            for (int i = 0; i < count; i++)
                Spawn(cfg, at);
            Logger.Debug("Emitted {Count} heart particles at {Origin}", count, at);
        }

        /// <summary>
        /// Emit a single Zzz particle (sleeping). Also auto-called periodically
        /// while <see cref="StartZzzEmitting"/> is active.
        /// Float up-right, fade. PRD §7.4 / FR-18.
        /// </summary>
        public void EmitZzz(SKPoint? origin = null)
        {
            var cfg = EmitterPresets.Zzz;
            var at = origin ?? _zzzOrigin;
            Spawn(cfg, at);
        }

        /// <summary>
        /// Begin periodic Zzz emission while sleeping.
        /// A Zzz particle is emitted every <see cref="EmitterPresets.Zzz"/>.EmitInterval
        /// seconds from the current emit origin.
        /// </summary>
        public void StartZzzEmitting()
        {
            if (_zzzEmitting) return;
            _zzzEmitting = true;
            _zzzAccumulator = 0.0;
            Logger.Debug("Periodic Zzz emission started");
        }

        /// <summary>Stop periodic Zzz emission (on wake). Leaves active Zzz particles to fade.</summary>
        public void StopZzzEmitting()
        {
            if (!_zzzEmitting) return;
            _zzzEmitting = false;
            Logger.Debug("Periodic Zzz emission stopped");
        }

        /// <summary>
        /// Emit a brief, sharp exclamation "!" pop (surprised). PRD §7.4 / FR-18.
        /// </summary>
        public void EmitSurprised(SKPoint? origin = null)
        {
            var cfg = EmitterPresets.Exclamation;
            var at = origin ?? _emitOrigin;
            // Single particle — sharp, no jitter.
            Spawn(cfg, at);
            Logger.Debug("Emitted surprised '!' particle at {Origin}", at);
        }

        /// <summary>
        /// Emit small dust puffs at ground level on landing after a fall.
        /// PRD §7.4 / FR-18.
        /// </summary>
        /// <param name="count">Number of puffs (default 6).</param>
        /// <param name="origin">Ground contact point (defaults to emit origin).</param>
        public void EmitDust(int count = 6, SKPoint? origin = null)
        {
            if (count <= 0) return;
            var cfg = EmitterPresets.Dust;
            var at = origin ?? _emitOrigin;
            for (int i = 0; i < count; i++)
                Spawn(cfg, at);
            Logger.Debug("Emitted {Count} dust puffs at {Origin}", count, at);
        }

        /// <summary>Clear all active particles immediately.</summary>
        public void Clear()
        {
            _particles.Clear();
            _zzzEmitting = false;
        }

        //───────────────────────── Update / Draw ─────────────────────────

        /// <summary>
        /// Advance all active particles by <paramref name="dt"/> seconds and
        /// remove expired ones. Also ticks the periodic Zzz emitter.
        /// </summary>
        /// <param name="dt">Delta time in seconds (clamped to ≥0).</param>
        public void Update(double dt)
        {
            if (dt < 0) dt = 0;

            // Periodic Zzz emission.
            if (_zzzEmitting)
            {
                double interval = EmitterPresets.Zzz.EmitInterval;
                if (interval > 0)
                {
                    _zzzAccumulator += dt;
                    while (_zzzAccumulator >= interval)
                    {
                        _zzzAccumulator -= interval;
                        EmitZzz(_zzzOrigin);
                    }
                }
            }

            // Integrate. Iterate backwards so we can swap-remove dead particles.
            float fdt = (float)dt;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Velocity = new SKPoint(
                    p.Velocity.X + p.Acceleration.X * fdt,
                    p.Velocity.Y + p.Acceleration.Y * fdt);
                p.Position = new SKPoint(
                    p.Position.X + p.Velocity.X * fdt,
                    p.Position.Y + p.Velocity.Y * fdt);
                p.Rotation += p.AngularVelocity * fdt;
                p.RemainingLifetime -= dt;

                if (p.IsDead)
                {
                    // Swap-remove (order does not matter for particles).
                    _particles[i] = _particles[_particles.Count - 1];
                    _particles.RemoveAt(_particles.Count - 1);
                }
            }
        }

        /// <summary>
        /// Render all active particles onto <paramref name="canvas"/> using
        /// SkiaSharp vector drawing. No image assets. PRD §7.4 / FR-18.
        /// </summary>
        public void Draw(SKCanvas canvas)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            foreach (var p in _particles)
            {
                float alpha = ComputeAlpha(p);
                if (alpha <= 0f) continue;

                using var paint = new SKPaint
                {
                    Color = p.Color.WithAlpha((byte)(alpha * 255f)),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                DrawShape(canvas, paint, p);
            }
        }

        //───────────────────────── Internals ─────────────────────────

        private void Spawn(EmitterConfig cfg, SKPoint origin)
        {
            double lifetime = cfg.Lifetime * (1.0 + Jitter(cfg.LifetimeJitter));
            if (lifetime <= 0) lifetime = 0.01;

            float size = cfg.Size * (1f + JitterF(cfg.SizeJitter));
            if (size <= 0f) size = 0.5f;

            float vx = cfg.Velocity.X + JitterF(cfg.VelocityJitter.X);
            float vy = cfg.Velocity.Y + JitterF(cfg.VelocityJitter.Y);

            // Small positional scatter so bursts don't stack perfectly.
            float scatter = size * 0.5f;
            float px = origin.X + (float)(_rng.NextDouble() - 0.5) * scatter;
            float py = origin.Y + (float)(_rng.NextDouble() - 0.5) * scatter;

            var p = new Particle
            {
                Position = new SKPoint(px, py),
                Velocity = new SKPoint(vx, vy),
                Acceleration = cfg.Acceleration,
                RemainingLifetime = lifetime,
                TotalLifetime = lifetime,
                Color = cfg.Color,
                Size = size,
                Shape = cfg.Shape,
                Rotation = 0f,
                AngularVelocity = cfg.Shape == ParticleShape.Zzz
                    ? 0.4f * (float)(_rng.NextDouble() - 0.5)
                    : 0f
            };
            _particles.Add(p);
        }

        /// <summary>Symmetric jitter in [-fraction, +fraction].</summary>
        private double Jitter(double fraction) =>
            (_rng.NextDouble() * 2.0 - 1.0) * fraction;

        /// <summary>Float jitter in [-fraction, +fraction].</summary>
        private float JitterF(float fraction) =>
            (float)((_rng.NextDouble() * 2.0 - 1.0) * fraction);

        /// <summary>
        /// Fade alpha curve: fade-in over first 10%, hold, fade-out over last 60%.
        /// Surprised "!" uses a sharper pop (full opacity, fast fade).
        /// </summary>
        private static float ComputeAlpha(Particle p)
        {
            float age = p.AgeFraction;
            if (age >= 1f) return 0f;

            switch (p.Shape)
            {
                case ParticleShape.Exclamation:
                    // Sharp pop: full for first 30%, fade rest.
                    if (age < 0.3f) return 1f;
                    return 1f - ((age - 0.3f) / 0.7f);
                default:
                    // Gentle fade-in (0..0.1) then fade-out (0.4..1.0).
                    float fadeIn = Math.Min(1f, age / 0.1f);
                    float fadeOut = age < 0.4f ? 1f : 1f - ((age - 0.4f) / 0.6f);
                    return Math.Max(0f, Math.Min(1f, fadeIn * fadeOut));
            }
        }

        private static void DrawShape(SKCanvas canvas, SKPaint paint, Particle p)
        {
            switch (p.Shape)
            {
                case ParticleShape.Heart:
                    DrawHeart(canvas, paint, p);
                    break;
                case ParticleShape.Zzz:
                    DrawTextGlyph(canvas, paint, p, "z");
                    break;
                case ParticleShape.Exclamation:
                    DrawTextGlyph(canvas, paint, p, "!");
                    break;
                case ParticleShape.Dust:
                    DrawDust(canvas, paint, p);
                    break;
                default:
                    // Fail-loud (constitution): unknown shape is a programming error.
                    throw new InvalidOperationException(
                        $"Unknown particle shape: {p.Shape}");
            }
        }

        /// <summary>
        /// Draw a heart shape as two circles + triangle, centred at p.Position.
        /// Classic vector heart, scaled by p.Size.
        /// </summary>
        private static void DrawHeart(SKCanvas canvas, SKPaint paint, Particle p)
        {
            float s = p.Size;
            float cx = p.Position.X;
            float cy = p.Position.Y;

            // Save/restore so the translate+rotate doesn't leak.
            int save = canvas.Save();
            try
            {
                canvas.Translate(cx, cy);
                if (p.Rotation != 0f)
                    canvas.RotateRadians(p.Rotation);

                // Two lobes.
                float r = s * 0.5f;
                canvas.DrawCircle(-r * 0.5f, -r * 0.2f, r, paint);
                canvas.DrawCircle(r * 0.5f, -r * 0.2f, r, paint);

                // Bottom triangle using a path.
                using var path = new SKPath();
                path.MoveTo(-s, 0f);
                path.LineTo(s, 0f);
                path.LineTo(0f, s);
                path.Close();
                canvas.DrawPath(path, paint);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }

        /// <summary>
        /// Draw a text glyph ("z" for Zzz, "!" for surprised) using SkiaSharp
        /// text rendering. Size scales with p.Size.
        /// </summary>
        private static void DrawTextGlyph(SKCanvas canvas, SKPaint paint, Particle p, string glyph)
        {
            float fontSize = p.Size * 2f;

            // Use a dedicated text paint for measurement + drawing so the
            // caller's fill paint (used for shapes) is not mutated.
            using var typeface = SKTypeface.FromFamilyName(
                null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var textPaint = new SKPaint
            {
                Color = paint.Color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Typeface = typeface,
                TextSize = fontSize,
                TextAlign = SKTextAlign.Left
            };

            // Measure glyph bounds to centre it on p.Position.
            var bounds = new SKRect();
            float w = textPaint.MeasureText(glyph, ref bounds);
            float h = bounds.Height;

            int save = canvas.Save();
            try
            {
                canvas.Translate(
                    p.Position.X - w * 0.5f - bounds.Left,
                    p.Position.Y - h * 0.5f - bounds.Top);
                if (p.Rotation != 0f)
                    canvas.RotateRadians(p.Rotation, w * 0.5f + bounds.Left, h * 0.5f + bounds.Top);
                // DrawText(string, x, y, paint): y is the baseline.
                canvas.DrawText(glyph, 0f, 0f, textPaint);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }

        /// <summary>
        /// Draw a dust puff as a soft filled circle (low-alpha blob).
        /// </summary>
        private static void DrawDust(SKCanvas canvas, SKPaint paint, Particle p)
        {
            // Dust expands as it fades — grow radius with age.
            float radius = p.Size * (1f + p.AgeFraction * 0.8f);
            canvas.DrawCircle(p.Position.X, p.Position.Y, radius, paint);
        }
    }
}