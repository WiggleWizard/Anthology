// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

namespace Prowl.Scribe;

/// <summary>
/// A glyph rasterized for one exact pixel size and horizontal sub-pixel phase, in a texture owned by an
/// <see cref="IGlyphBitmapProvider"/>. Unlike a distance-field glyph it cannot be scaled: it is drawn texel for pixel.
/// </summary>
public struct BitmapGlyph
{
    /// <summary>The texture holding the bitmap, as created by the font renderer.</summary>
    public object Texture;

    /// <summary>Offset of the bitmap's left edge from the integer pen position, in pixels.</summary>
    public int Left;

    /// <summary>Offset of the bitmap's top edge from the baseline, in pixels (negative is above the baseline).</summary>
    public int Top;

    /// <summary>Size of the bitmap in pixels. Zero for a glyph with no ink, such as a space.</summary>
    public int Width;
    public int Height;

    /// <summary>The bitmap's rectangle in the texture, as UVs.</summary>
    public float U0, V0, U1, V1;
}

/// <summary>
/// An optional source of pre-rasterized glyph bitmaps (for instance from the platform's own text rasterizer) that
/// <see cref="FontSystem"/> uses instead of its distance-field atlas for small, axis-aligned text. Scribe still does all
/// layout; the provider only decides what each glyph's pixels look like. Bitmap glyphs are placed at an integer pen plus
/// one of <see cref="Phases"/> sub-pixel positions and on an integer baseline, so they are only used when the font
/// system snaps both the draw origin and the baseline (<see cref="TextPixelSnap.Origin"/> and
/// <see cref="TextPixelSnap.Baseline"/>), and only when the renderer implements <see cref="IBitmapFontRenderer"/>.
/// </summary>
public interface IGlyphBitmapProvider
{
    /// <summary>How many horizontal sub-pixel positions a glyph is rasterized at (1 means whole pixels only).</summary>
    int Phases { get; }

    /// <summary>
    /// Bumped whenever previously returned glyphs become invalid (the provider's texture was cleared or recreated).
    /// Layouts whose cached quads came from an older version rebuild them on their next draw.
    /// </summary>
    int Version { get; }

    /// <summary>Whether text in <paramref name="font"/> at <paramref name="pixelSize"/> device pixels should use bitmaps.</summary>
    bool Accepts(FontFile font, float pixelSize);

    /// <summary>
    /// The bitmap for <paramref name="glyphIndex"/> in <paramref name="font"/> at <paramref name="pixelSize"/> (Scribe's pixel
    /// size: ascender to descender), with its pen <paramref name="phase"/> / <see cref="Phases"/> of a pixel right of an
    /// integer position. Returns false when the provider cannot draw this glyph, in which case the distance field is used.
    /// </summary>
    bool TryGetGlyph(FontFile font, int glyphIndex, float pixelSize, int phase, out BitmapGlyph glyph);
}

/// <summary>A font renderer that can also draw <see cref="BitmapGlyph"/> quads (texel for pixel, no distance field).</summary>
public interface IBitmapFontRenderer : IFontRenderer
{
    /// <summary>
    /// Whether text drawn now would land texel for pixel: false while the renderer applies a scale, rotation or skew, in which
    /// case <see cref="FontSystem"/> draws from the distance field. Asked on every draw, so it can follow the current transform.
    /// </summary>
    bool CanDrawBitmaps { get; }

    /// <summary>Draws quads whose texture is a provider's bitmap texture rather than the distance-field atlas.</summary>
    void DrawBitmapQuads(object texture, ReadOnlySpan<IFontRenderer.Vertex> vertices, ReadOnlySpan<int> indices);
}
