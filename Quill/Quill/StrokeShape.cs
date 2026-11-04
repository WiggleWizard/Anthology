// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Vector;

namespace Prowl.Quill
{
    /// <summary>
    /// A fixed set of polylines in its own unit space, drawn with <see cref="Canvas.DrawStrokeShape"/>.
    /// The canvas meshes a shape once per size bucket and replays that mesh for later draws of the same
    /// shape at a similar size, so keep one instance per distinct shape (an icon, a glyph) and reuse it.
    /// The contours must not change once the shape has been drawn.
    /// </summary>
    public sealed class StrokeShape
    {
        internal readonly List<Float2[]> Contours = new List<Float2[]>();

        /// <summary>Number of contours added so far.</summary>
        public int ContourCount => Contours.Count;

        /// <summary>
        /// Adds one polyline. A closed contour gets a closing segment back to its first point, the same
        /// as <see cref="Canvas.ClosePath"/>. Contours with fewer than two points draw nothing.
        /// </summary>
        public void AddContour(IReadOnlyList<Float2> points, bool closed)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            bool appendClose = closed && points.Count >= 2;
            var copy = new Float2[points.Count + (appendClose ? 1 : 0)];
            for (int i = 0; i < points.Count; i++)
                copy[i] = points[i];
            if (appendClose)
                copy[points.Count] = points[0];

            Contours.Add(copy);
        }
    }
}
