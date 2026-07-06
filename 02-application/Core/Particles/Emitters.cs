using System;
using System.Collections.Generic;
using SkiaSharp;

namespace MochiV2.Core.Particles
{
    /// <summary>
    /// Shape drawn for a particle. PRD §7.4 / FR-18.
    /// All shapes are vector-drawn via SkiaSharp — no image assets.
    /// </summary>
    public enum ParticleShape
    {
        /// <summary>Heart ❤ — emitted when fed or petted.</summary>
        Heart,

        /// <summary>Zzz 💤 — emitted while sleeping.</summary>
        Zzz,

        /// <summary>Exclamation "!" — emitted when surprised.</summary>
        Exclamation,

        /// <summary>Dust puff — emitted on landing after a fall.</summary>
        Dust
    }

    /// <summary>
    /// Emitter type enum mirroring the four PRD §7.4 emitter kinds.
    /// </summary>
    public enum EmitterType
    {
        Hearts,
        Zzz,
        Exclamation,
        Dust
    }

    /// <summary>
    /// Configuration for one emitter kind. Values chosen to match the
    /// visual behaviour described in PRD §7.4 / FR-18 and DESIGN §S-1.
    /// </summary>
    public readonly struct EmitterConfig
    {
        /// <summary>Shape rendered for particles from this emitter.</summary>
        public ParticleShape Shape { get; }

        /// <summary>Base color (alpha overridden per-particle by lifetime fade).</summary>
        public SKColor Color { get; }

        /// <summary>Base radius / half-size in logical pixels before jitter.</summary>
        public float Size { get; }

        /// <summary>Size jitter ± (0..1) fraction applied per particle.</summary>
        public float SizeJitter { get; }

        /// <summary>Mean lifetime in seconds.</summary>
        public double Lifetime { get; }

        /// <summary>Lifetime jitter ± fraction (0..1).</summary>
        public double LifetimeJitter { get; }

        /// <summary>Base velocity (px/s) in screen space (y+ = down).</summary>
        public SKPoint Velocity { get; }

        /// <summary>Velocity jitter magnitude (px/s) applied per axis.</summary>
        public SKPoint VelocityJitter { get; }

        /// <summary>Gravity / acceleration (px/s²). Hearts/Zzz use slight negative (float up).</summary>
        public SKPoint Acceleration { get; }

        /// <summary>
        /// Periodic emit interval in seconds (≤0 = one-shot).
        /// Only <see cref="EmitterType.Zzz"/> is periodic while sleeping.
        /// </summary>
        public double EmitInterval { get; }

        public EmitterConfig(
            ParticleShape shape,
            SKColor color,
            float size,
            float sizeJitter,
            double lifetime,
            double lifetimeJitter,
            SKPoint velocity,
            SKPoint velocityJitter,
            SKPoint acceleration,
            double emitInterval)
        {
            Shape = shape;
            Color = color;
            Size = size;
            SizeJitter = sizeJitter;
            Lifetime = lifetime;
            LifetimeJitter = lifetimeJitter;
            Velocity = velocity;
            VelocityJitter = velocityJitter;
            Acceleration = acceleration;
            EmitInterval = emitInterval;
        }
    }

    /// <summary>
    /// Static factory of tuned <see cref="EmitterConfig"/> presets for the four
    /// PRD §7.4 emitter types. Centralises the "feel" so <see cref="ParticleSystem"/>
    /// stays orchestration-only.
    /// </summary>
    public static class EmitterPresets
    {
        // PRD design tokens: hearts warm pink (#E8A0BF placeholder family),
        // Zzz lavender (#9575CD — DESIGN color-sleep), dust neutral grey,
        // exclamation alert red-orange. All alpha handled at draw time.

        /// <summary>Hearts: float upward, fade out. Fed/petted.</summary>
        public static EmitterConfig Hearts { get; } = new(
            shape: ParticleShape.Heart,
            color: new SKColor(0xE8, 0x6A, 0x92),
            size: 10f,
            sizeJitter: 0.3f,
            lifetime: 1.6,
            lifetimeJitter: 0.2,
            velocity: new SKPoint(0f, -60f),
            velocityJitter: new SKPoint(30f, 20f),
            acceleration: new SKPoint(0f, -15f),
            emitInterval: 0.0);

        /// <summary>Zzz: float up-right, fade. Periodic while sleeping.</summary>
        public static EmitterConfig Zzz { get; } = new(
            shape: ParticleShape.Zzz,
            color: new SKColor(0x95, 0x75, 0xCD),
            size: 12f,
            sizeJitter: 0.25f,
            lifetime: 2.0,
            lifetimeJitter: 0.25,
            velocity: new SKPoint(25f, -45f),
            velocityJitter: new SKPoint(10f, 10f),
            acceleration: new SKPoint(0f, -5f),
            emitInterval: 1.6);

        /// <summary>Exclamation: brief, sharp pop. Surprised.</summary>
        public static EmitterConfig Exclamation { get; } = new(
            shape: ParticleShape.Exclamation,
            color: new SKColor(0xFF, 0x6B, 0x35),
            size: 18f,
            sizeJitter: 0.0f,
            lifetime: 0.45,
            lifetimeJitter: 0.0,
            velocity: new SKPoint(0f, -10f),
            velocityJitter: new SKPoint(0f, 0f),
            acceleration: new SKPoint(0f, 0f),
            emitInterval: 0.0);

        /// <summary>Dust: small puffs at ground level. Landing after fall.</summary>
        public static EmitterConfig Dust { get; } = new(
            shape: ParticleShape.Dust,
            color: new SKColor(0xC8, 0xC2, 0xB6),
            size: 6f,
            sizeJitter: 0.4f,
            lifetime: 0.6,
            lifetimeJitter: 0.2,
            velocity: new SKPoint(0f, -20f),
            velocityJitter: new SKPoint(50f, 10f),
            acceleration: new SKPoint(0f, 60f),
            emitInterval: 0.0);

        /// <summary>Lookup by emitter type.</summary>
        public static EmitterConfig For(EmitterType type) => type switch
        {
            EmitterType.Hearts => Hearts,
            EmitterType.Zzz => Zzz,
            EmitterType.Exclamation => Exclamation,
            EmitterType.Dust => Dust,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown emitter type")
        };
    }
}