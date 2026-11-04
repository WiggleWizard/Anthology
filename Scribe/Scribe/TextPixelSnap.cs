using System;

namespace Prowl.Scribe
{
    /// <summary>
    /// Which parts of drawn text <see cref="FontSystem"/> rounds to whole device pixels. Distance-field glyphs
    /// cannot be hinted, so a glyph that lands on a half pixel smears its stems and baseline across two pixel
    /// rows. Snapping trades a little positional accuracy (at most half a pixel per snapped value) for text
    /// that sits on the pixel grid and looks the same wherever it is drawn.
    /// </summary>
    [Flags]
    public enum TextPixelSnap
    {
        /// <summary>Glyphs are placed exactly where layout puts them.</summary>
        None = 0,

        /// <summary>
        /// Rounds the draw position of a whole layout, so the same text draws with the same subpixel phase
        /// wherever it is placed. Renderers that add their own translation should round it too.
        /// </summary>
        Origin = 1 << 0,

        /// <summary>
        /// Rounds each glyph's baseline (and underline/strikethrough bars) to a whole pixel row, which is
        /// what makes horizontal strokes and the bottom of the text crisp.
        /// </summary>
        Baseline = 1 << 1,

        /// <summary>
        /// Rounds each glyph's pen position horizontally. Every copy of a letter then renders identically,
        /// but spacing between letters can drift by up to half a pixel, which reads as uneven kerning.
        /// </summary>
        GlyphX = 1 << 2,
    }
}
