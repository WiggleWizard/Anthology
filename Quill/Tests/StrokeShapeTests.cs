// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Quill;
using Prowl.Vector;
using Prowl.Vector.Spatial;

namespace Tests;

/// <summary>
/// A stroke shape has to come out as building the same path and calling Stroke() would, whether it is
/// meshed, replayed at its bucket's centre, or replayed at another size in the bucket.
/// </summary>
public class StrokeShapeTests
{
    private static readonly Color32 White = Color32.FromArgb(255, 255, 255, 255);
    private static readonly Color32 Red = Color32.FromArgb(200, 255, 0, 0);

    // Scales of exactly a bucket centre (1.3^2) and one in the same bucket but off its centre.
    private static readonly float CentreScale = MathF.Pow(1.3f, 2);
    private static readonly float OffCentreScale = CentreScale * 1.1f;

    // An icon-like shape: curves flattened to short segments, a closed contour, round joins and caps.
    // Arc segment counts and overlap decisions depend on size, so it only matches Stroke() exactly at
    // the centre scale.
    private static readonly (Float2[] Points, bool Closed)[] IconContours =
    {
        (new[] { new Float2(2, 4), new Float2(2.3f, 3.3f), new Float2(3, 3), new Float2(6, 3), new Float2(7.2f, 4.4f), new Float2(13, 4.4f), new Float2(14, 5.4f), new Float2(14, 12), new Float2(2, 12) }, true),
        (new[] { new Float2(8, 7), new Float2(8, 10.5f) }, false),
        (new[] { new Float2(6.3f, 9), new Float2(9.7f, 9) }, false),
    };

    // Long straight segments with wide-angle miter joins and butt caps: nothing in it depends on size,
    // so a replay at any scale in the bucket must match Stroke() at that scale.
    private static readonly (Float2[] Points, bool Closed)[] PlainContours =
    {
        (new[] { new Float2(1, 1), new Float2(15, 2), new Float2(14, 15) }, false),
        (new[] { new Float2(3, 6), new Float2(11, 6), new Float2(11, 12), new Float2(3, 12) }, true),
    };

    [Fact]
    public void A_cached_icon_at_its_bucket_centre_matches_stroking_the_path()
    {
        var shape = Shape(IconContours);
        Action<Canvas> style = c => Style(c, 1.5f * CentreScale, JointStyle.Round, EndCapStyle.Round);

        var canvas = NewCanvas();
        var direct = Frame(canvas, c => { style(c); Direct(c, IconContours, 10, 20, CentreScale); });
        var first = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 10, 20, CentreScale); });
        var replayed = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 10, 20, CentreScale); });

        AssertSameOutput(direct, first);
        AssertSameOutput(direct, replayed);
        Assert.Equal(1, canvas.StrokeShapeCacheCount);
    }

    [Fact]
    public void A_replay_off_the_bucket_centre_matches_stroking_the_path_at_that_size()
    {
        var shape = Shape(PlainContours);
        Action<Canvas> style = c => Style(c, 2f * OffCentreScale, JointStyle.Miter, EndCapStyle.Butt);

        var canvas = NewCanvas();
        Frame(canvas, c => { Style(c, 2f * CentreScale * 0.95f, JointStyle.Miter, EndCapStyle.Butt); c.DrawStrokeShape(shape, 0, 0, CentreScale * 0.95f); });
        var direct = Frame(canvas, c => { style(c); Direct(c, PlainContours, 33.5f, 47.25f, OffCentreScale); });
        var replayed = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 33.5f, 47.25f, OffCentreScale); });

        AssertSameOutput(direct, replayed);
        Assert.Equal(1, canvas.StrokeShapeCacheCount);
    }

    [Fact]
    public void A_thin_line_replayed_off_centre_scales_its_coverage()
    {
        var shape = Shape(PlainContours);
        // 0.4 units is under one pixel at both scales, so both use the mesher's thin path.
        Action<Canvas> style = c => Style(c, 0.4f * OffCentreScale, JointStyle.Miter, EndCapStyle.Butt);

        var canvas = NewCanvas();
        Frame(canvas, c => { Style(c, 0.4f * CentreScale, JointStyle.Miter, EndCapStyle.Butt); c.DrawStrokeShape(shape, 0, 0, CentreScale); });
        var direct = Frame(canvas, c => { style(c); Direct(c, PlainContours, 5, 5, OffCentreScale); });
        var replayed = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 5, 5, OffCentreScale); });

        AssertSameOutput(direct, replayed);
    }

    [Fact]
    public void A_replay_without_anti_aliasing_matches_stroking_the_path()
    {
        var shape = Shape(PlainContours);
        Action<Canvas> style = c => { c.SetAntiAlias(false); Style(c, 2f * OffCentreScale, JointStyle.Miter, EndCapStyle.Butt); };

        var canvas = NewCanvas();
        Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 0, 0, CentreScale); });
        var direct = Frame(canvas, c => { style(c); Direct(c, PlainContours, 5, 5, OffCentreScale); });
        var replayed = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 5, 5, OffCentreScale); });

        AssertSameOutput(direct, replayed);
    }

    [Fact]
    public void A_translated_canvas_replays_in_place()
    {
        var shape = Shape(IconContours);
        Action<Canvas> style = c => { c.TransformBy(Transform2D.CreateTranslation(15, -4)); Style(c, 1.5f * CentreScale, JointStyle.Round, EndCapStyle.Round); };

        var canvas = NewCanvas();
        Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 0, 0, CentreScale); });
        var direct = Frame(canvas, c => { style(c); Direct(c, IconContours, 3, 7, CentreScale); });
        var replayed = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 3, 7, CentreScale); });

        AssertSameOutput(direct, replayed);
    }

    [Fact]
    public void A_rotated_canvas_meshes_directly_and_caches_nothing()
    {
        var shape = Shape(IconContours);
        Action<Canvas> style = c => { c.TransformBy(Transform2D.CreateRotation(0.3f)); Style(c, 1.5f * OffCentreScale, JointStyle.Round, EndCapStyle.Round); };

        var canvas = NewCanvas();
        var direct = Frame(canvas, c => { style(c); Direct(c, IconContours, 40, 10, OffCentreScale); });
        var drawn = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 40, 10, OffCentreScale); });

        AssertSameOutput(direct, drawn);
        Assert.Equal(0, canvas.StrokeShapeCacheCount);
    }

    [Fact]
    public void Turning_caching_off_meshes_directly()
    {
        var shape = Shape(IconContours);
        Action<Canvas> style = c => Style(c, 1.5f * OffCentreScale, JointStyle.Round, EndCapStyle.Round);

        var canvas = NewCanvas();
        canvas.StrokeShapeCaching = false;
        var direct = Frame(canvas, c => { style(c); Direct(c, IconContours, 40, 10, OffCentreScale); });
        var drawn = Frame(canvas, c => { style(c); c.DrawStrokeShape(shape, 40, 10, OffCentreScale); });

        AssertSameOutput(direct, drawn);
        Assert.Equal(0, canvas.StrokeShapeCacheCount);
    }

    [Fact]
    public void Sizes_in_one_bucket_and_any_colour_share_one_entry()
    {
        var shape = Shape(IconContours);
        var canvas = NewCanvas();

        Frame(canvas, c =>
        {
            Style(c, 1.5f * CentreScale, JointStyle.Round, EndCapStyle.Round);
            c.DrawStrokeShape(shape, 0, 0, CentreScale);
            c.SetStrokeColor(Red);
            c.SetStrokeWidth(1.5f * CentreScale * 0.9f);
            c.DrawStrokeShape(shape, 0, 0, CentreScale * 0.9f);
            c.SetStrokeWidth(1.5f * CentreScale * 1.12f);
            c.DrawStrokeShape(shape, 0, 0, CentreScale * 1.12f);
        });
        Assert.Equal(1, canvas.StrokeShapeCacheCount);

        Frame(canvas, c =>
        {
            Style(c, 1.5f * CentreScale * 1.5f, JointStyle.Round, EndCapStyle.Round);
            c.DrawStrokeShape(shape, 0, 0, CentreScale * 1.5f);
        });
        Assert.Equal(2, canvas.StrokeShapeCacheCount);
    }

    [Fact]
    public void A_replay_off_centre_keeps_the_fringe_one_pixel_wide()
    {
        // A single horizontal segment: the fringe vertices sit exactly one pixel outside the core ones.
        var shape = Shape(new[] { (new[] { new Float2(2, 8), new Float2(14, 8) }, false) });
        var canvas = NewCanvas();

        Frame(canvas, c => { Style(c, 2f * CentreScale, JointStyle.Miter, EndCapStyle.Butt); c.DrawStrokeShape(shape, 0, 0, CentreScale); });
        var replayed = Frame(canvas, c => { Style(c, 2f * OffCentreScale, JointStyle.Miter, EndCapStyle.Butt); c.DrawStrokeShape(shape, 0, 0, OffCentreScale); });

        float coreTop = float.MaxValue, fringeTop = float.MaxValue;
        foreach (var v in replayed.Vertices)
        {
            if (v.u > 0.5f) coreTop = MathF.Min(coreTop, v.y);
            else fringeTop = MathF.Min(fringeTop, v.y);
        }
        Assert.Equal(1f, coreTop - fringeTop, 0.001f);
        // Core plus half the fringe on each side spans the stroke width.
        Assert.Equal(8f * OffCentreScale - OffCentreScale * 2f * 0.5f + 0.5f, coreTop, 0.01f);
    }

    [Theory]
    [InlineData(JointStyle.Round, EndCapStyle.Round)]
    [InlineData(JointStyle.Miter, EndCapStyle.Butt)]
    [InlineData(JointStyle.Bevel, EndCapStyle.Square)]
    [InlineData(JointStyle.MiterClip, EndCapStyle.Butt)]
    public void Icon_shapes_split_into_core_and_fringe_at_every_icon_size(JointStyle joint, EndCapStyle cap)
    {
        var shape = Shape(IconContours);
        var canvas = NewCanvas();

        // Icon sizes from 8 to 96 pixels (16 unit viewBox), for thin and thick stroke widths.
        for (float pixels = 8f; pixels <= 96f; pixels *= 1.07f)
        {
            float scale = pixels / 16f;
            foreach (float unitWidth in new[] { 0.5f, 1.5f, 2.5f })
            {
                Frame(canvas, c => { Style(c, unitWidth * scale, joint, cap); c.DrawStrokeShape(shape, 0, 0, scale); });
            }
        }

        Assert.True(canvas.StrokeShapeCacheCount > 10);
        Assert.Equal(0, canvas.StrokeShapeUnusableCount);
    }

    private static void Style(Canvas canvas, float width, JointStyle joint, EndCapStyle cap)
    {
        canvas.SetStrokeColor(White);
        canvas.SetStrokeWidth(width);
        canvas.SetStrokeJoint(joint);
        canvas.SetStrokeCap(cap);
    }

    private static StrokeShape Shape((Float2[] Points, bool Closed)[] contours)
    {
        var shape = new StrokeShape();
        foreach (var (points, closed) in contours)
            shape.AddContour(points, closed);
        return shape;
    }

    // The reference: the same contours built as a path and stroked.
    private static void Direct(Canvas canvas, (Float2[] Points, bool Closed)[] contours, float x, float y, float scale)
    {
        foreach (var (points, closed) in contours)
        {
            canvas.BeginPath();
            canvas.MoveTo(x + points[0].X * scale, y + points[0].Y * scale);
            for (int i = 1; i < points.Length; i++)
                canvas.LineTo(x + points[i].X * scale, y + points[i].Y * scale);
            if (closed)
                canvas.ClosePath();
            canvas.Stroke();
        }
    }

    private sealed record Output(Vertex[] Vertices, uint[] Indices, int Calls);

    private static Output Frame(Canvas canvas, Action<Canvas> draw)
    {
        canvas.BeginFrame(800, 600);
        canvas.ResetState();
        canvas.SetAntiAlias(true);
        draw(canvas);
        return new Output(canvas.Vertices.ToArray(), canvas.Indices.ToArray(), canvas.DrawCalls.Count);
    }

    private static void AssertSameOutput(Output expected, Output actual)
    {
        Assert.Equal(expected.Vertices.Length, actual.Vertices.Length);
        Assert.Equal(expected.Indices, actual.Indices);
        Assert.Equal(expected.Calls, actual.Calls);

        for (int i = 0; i < expected.Vertices.Length; i++)
        {
            var e = expected.Vertices[i];
            var a = actual.Vertices[i];
            Assert.Equal(e.x, a.x, 0.01f);
            Assert.Equal(e.y, a.y, 0.01f);
            Assert.Equal(e.u, a.u, 0.0001f);
            Assert.Equal(e.v, a.v);
            Assert.Equal(e.Color, a.Color);
        }
    }

    private static Canvas NewCanvas()
    {
        var canvas = new Canvas(new NullRenderer(), new FontAtlasSettings());
        canvas.BeginFrame(800, 600);
        return canvas;
    }

    private sealed class NullRenderer : ICanvasRenderer
    {
        public void Dispose() { }
        public object CreateTexture(uint w, uint h) => new Int2((int)w, (int)h);
        public Int2 GetTextureSize(object texture) => (Int2)texture;
        public void SetTextureData(object texture, IntRect bounds, byte[] data) { }
        public void RenderCalls(Canvas canvas, IReadOnlyList<DrawCall> calls) { }
    }
}
