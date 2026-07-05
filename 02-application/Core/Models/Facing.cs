namespace MochiV2.Core.Models
{
    /// <summary>
    /// Horizontal facing direction for sprite selection and walk logic.
    /// PRD §7.2: horizontal flip is FORBIDDEN (asymmetric sprites), so
    /// facing drives which sprite folder is used (cat_*_left vs cat_*_right).
    /// </summary>
    public enum Facing
    {
        /// <summary>Looking/moving left.</summary>
        Left,

        /// <summary>Looking/moving right.</summary>
        Right,

        /// <summary>Looking forward (toward viewer), idle.</summary>
        Forward
    }
}