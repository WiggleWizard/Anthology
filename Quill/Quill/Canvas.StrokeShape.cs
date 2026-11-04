// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

using Prowl.Perf;
using Prowl.Vector;

namespace Prowl.Quill
{
    public partial class Canvas
    {
        private readonly StrokeShapeCache _strokeShapeCache = new StrokeShapeCache();

        /// <summary>
        /// Whether <see cref="DrawStrokeShape"/> reuses cached meshes.
        /// When false every call meshes the shape, exactly like <see cref="Stroke"/>. Default true.
        /// </summary>
        public bool StrokeShapeCaching { get; set; } = true;

        /// <summary>Number of stroke shape meshes currently cached.</summary>
        public int StrokeShapeCacheCount => _strokeShapeCache.Count;

        // Entries that fell back to direct meshing (for tests).
        internal int StrokeShapeUnusableCount => _strokeShapeCache.UnusableCount;

        /// <summary>Drops every cached stroke shape mesh.</summary>
        public void ClearStrokeShapeCache() => _strokeShapeCache.Clear();

        /// <summary>
        /// Strokes every contour of <paramref name="shape"/>, scaled by <paramref name="scale"/> and placed with
        /// its origin at (<paramref name="x"/>, <paramref name="y"/>), using the current stroke colour, width
        /// and style. The width is in local units, as for <see cref="Stroke"/>. The current path is left alone.
        /// </summary>
        /// <remarks>
        /// The result matches building the same contours with <see cref="MoveTo"/>/<see cref="LineTo"/> and
        /// calling <see cref="Stroke"/>, except that the mesh is built once per size bucket (sizes within about
        /// 14% of each other share one) and replayed after that, with the anti-aliasing fringe kept at one
        /// physical pixel. Only identity and translation transforms are cached; anything else meshes directly.
        /// </remarks>
        public void DrawStrokeShape(StrokeShape shape, float x, float y, float scale)
        {
            using (Markers.Begin(PerfSites.ShapeStroke))
                StrokeShapeCore(shape, x, y, scale);
        }

        private void StrokeShapeCore(StrokeShape shape, float x, float y, float scale)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (shape.Contours.Count == 0 || !(scale > 0f))
                return;

            float localWidth = _state.strokeWidth * _state.strokeScale;
            if (!(localWidth > 0f))
                return;

            // Same early-out as the mesher: a fully transparent stroke draws nothing.
            Color32 pmColor = PremultiplyColor(_state.strokeColor);
            if (pmColor.A == 0)
                return;

            if (!StrokeShapeCaching || !_state.transformIsTranslationOnly)
            {
                _perf.ShapeStrokeUncached.Add();
                StrokeShapeDirect(shape, x, y, scale, pmColor);
                return;
            }

            float pixelScale = scale * _framebufferScale;
            float fringe = _antiAlias ? 1.0f : 0.0f;
            int widthKey = (int)MathF.Round(localWidth / scale / StrokeShapeCache.WidthQuantum);
            if (widthKey <= 0)
            {
                _perf.ShapeStrokeUncached.Add();
                StrokeShapeDirect(shape, x, y, scale, pmColor);
                return;
            }

            var key = new StrokeShapeCache.Key(shape, widthKey, StrokeShapeCache.Bucket(pixelScale), _state.strokeJoint,
                _state.strokeStartCap, _state.strokeEndCap, _state.miterLimit, _state.roundingMinDistance * _framebufferScale, fringe);
            StrokeShapeCache.Entry entry = _strokeShapeCache.GetOrBuild(in key, out bool built);
            if (built)
                _perf.ShapeStrokeMisses.Add();
            else
                _perf.ShapeStrokeHits.Add();

            float k = pixelScale / entry.CentreScale;

            // The thin-line path has a different topology from the thick one, so an entry only serves
            // requests that land on the same side of the one pixel threshold.
            float pixelThickness = localWidth * _framebufferScale;
            bool thin = fringe > 0f && pixelThickness <= fringe;
            if (!entry.Usable || thin != entry.Thin)
            {
                _perf.ShapeStrokeUncached.Add();
                StrokeShapeDirect(shape, x, y, scale, pmColor);
                return;
            }

            // A thin line's coverage is its width in pixels. Taken from the requested width rather than
            // k, which carries the width rounding of the cache key.
            float coverageScale = 1.0f;
            if (entry.Thin)
                coverageScale = pixelThickness / entry.CentreThickness;

            ReplayStrokeShape(entry, TransformPoint(new Float2(x, y)), k, coverageScale, pmColor);
        }

        private void ReplayStrokeShape(StrokeShapeCache.Entry entry, Float2 origin, float k, float coverageScale, Color32 pmColor)
        {
            Float2[] core = entry.Core;
            Float2[] fringe = entry.Fringe;
            float[] coverage = entry.Coverage;
            uint[] indices = entry.Indices;
            if (indices.Length == 0)
                return;

            uint startVertexIndex = (uint)_vertices.Count;
            _vertices.Reserve(core.Length);
            for (int i = 0; i < core.Length; i++)
            {
                Float2 c = core[i];
                Float2 f = fringe[i];
                var position = new Float2(origin.X + c.X * k + f.X, origin.Y + c.Y * k + f.Y);
                _vertices.Add(new Vertex(position, new Float2(coverage[i] * coverageScale, 0f), pmColor));
            }

            _indices.Reserve(indices.Length);
            for (int i = 0; i < indices.Length; i++)
                _indices.Add(startVertexIndex + indices[i]);

            AddTriangleCount(indices.Length / 3);
        }

        // Meshes each contour now, the same as building the path and calling Stroke().
        private void StrokeShapeDirect(StrokeShape shape, float x, float y, float scale, Color32 pmColor)
        {
            foreach (Float2[] contour in shape.Contours)
            {
                if (contour.Length < 2)
                    continue;

                _strokePoints.Clear();
                for (int i = 0; i < contour.Length; i++)
                    _strokePoints.Add(TransformPoint(new Float2(x + contour[i].X * scale, y + contour[i].Y * scale)));

                StrokeTransformedPoints(pmColor);
            }
        }
    }
}
