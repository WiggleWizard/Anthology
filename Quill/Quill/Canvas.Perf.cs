using Prowl.Perf;

namespace Prowl.Quill
{
    // Performance marks for the canvas. See Prowl.Perf: with no handler installed each mark is one static read.
    public partial class Canvas
    {
        internal static class PerfSites
        {
            public const string Library = "Quill";

            // One span per drawing call. These are frequent (one or more per UI element), so a host should put
            // them in a category of their own.
            public static readonly MarkerSite PathFill = MarkerSite.Create(Library, "Fill path");
            public static readonly MarkerSite PathTessellate = MarkerSite.Create(Library, "Fill complex path (tessellate)");
            public static readonly MarkerSite PathStroke = MarkerSite.Create(Library, "Stroke path");
            public static readonly MarkerSite RectFill = MarkerSite.Create(Library, "Rect filled");
            public static readonly MarkerSite RoundedRectFill = MarkerSite.Create(Library, "Rounded rect filled");
            public static readonly MarkerSite RingFill = MarkerSite.Create(Library, "Rounded rect ring filled");
            public static readonly MarkerSite CircleFill = MarkerSite.Create(Library, "Circle filled");
            public static readonly MarkerSite Border = MarkerSite.Create(Library, "Rounded rect border");
            public static readonly MarkerSite Image = MarkerSite.Create(Library, "Image");
            public static readonly MarkerSite TextLayout = MarkerSite.Create(Library, "Draw text layout");
            public static readonly MarkerSite GlyphQuads = MarkerSite.Create(Library, "Glyph quads");
            public static readonly MarkerSite AtlasUpload = MarkerSite.Create(Library, "Font atlas upload");
            public static readonly MarkerSite Submit = MarkerSite.Create(Library, "Submit draw calls");

            // Per-frame values, reported when the frame is submitted.
            public static readonly MarkerSite DrawCallCount = MarkerSite.Create(Library, "Draw calls");
            public static readonly MarkerSite VertexCount = MarkerSite.Create(Library, "Vertices");
            public static readonly MarkerSite TriangleCount = MarkerSite.Create(Library, "Triangles");
        }

        // Per-frame counts of canvas state and batching work, reported when the frame is submitted.
        internal sealed class PerfCounters
        {
            private const string Library = PerfSites.Library;

            public readonly MarkerTally SaveState = MarkerTally.Counter(Library, "SaveState calls");
            public readonly MarkerTally RestoreState = MarkerTally.Counter(Library, "RestoreState calls");

            // Batching: how often adding triangles had to re-check the draw state, and why a new draw call started.
            public readonly MarkerTally StateChecks = MarkerTally.Counter(Library, "Draw state checks");
            public readonly MarkerTally BreakScissor = MarkerTally.Counter(Library, "Draw call breaks: scissor");
            public readonly MarkerTally BreakBrush = MarkerTally.Counter(Library, "Draw call breaks: brush");
            public readonly MarkerTally BreakAtlas = MarkerTally.Counter(Library, "Draw call breaks: font atlas");
            public readonly MarkerTally BreakRequested = MarkerTally.Counter(Library, "Draw call breaks: requested");

            public readonly MarkerTally Glyphs = MarkerTally.Counter(Library, "Glyphs");
            public readonly MarkerTally AtlasUploads = MarkerTally.Counter(Library, "Font atlas uploads");
            public readonly MarkerTally AtlasUploadBytes = MarkerTally.Counter(Library, "Font atlas upload bytes");

            public void Flush()
            {
                SaveState.Flush();
                RestoreState.Flush();
                StateChecks.Flush();
                BreakScissor.Flush();
                BreakBrush.Flush();
                BreakAtlas.Flush();
                BreakRequested.Flush();
                Glyphs.Flush();
                AtlasUploads.Flush();
                AtlasUploadBytes.Flush();
            }
        }

        internal readonly PerfCounters _perf = new PerfCounters();

        // Counts why a new draw call was started instead of extending the last one.
        private void CountDrawCallBreak(in DrawCall last)
        {
            if (!_perf.BreakRequested.IsActive) return;

            if (_isNewDrawCallRequested)
            {
                _perf.BreakRequested.Add();
                return;
            }

            if (!ReferenceEquals(last.fontAtlas, _currentFontAtlas))
                _perf.BreakAtlas.Add();
            else if (!DrawCall.SameTransform(in last.scissor, in _state.scissor) || last.scissorExtent != _state.scissorExtent)
                _perf.BreakScissor.Add();
            else
                _perf.BreakBrush.Add();
        }

        // Reports the frame's values and counters. Called when the frame is submitted.
        private void ReportPerf()
        {
            _perf.Flush();
            if (!Markers.Enabled) return;

            Markers.Value(PerfSites.DrawCallCount, _drawCalls.Count);
            Markers.Value(PerfSites.VertexCount, _vertices.Count);
            Markers.Value(PerfSites.TriangleCount, _indices.Count / 3);
        }
    }
}
