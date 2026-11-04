using Prowl.Perf;

namespace Prowl.PaperUI
{
    // Performance marker sites for Paper's frame. See Prowl.Perf: with no handler installed each mark is one static read.
    public partial class Paper
    {
        internal static class PerfSites
        {
            public const string Library = "Paper";

            public static readonly MarkerSite BeginFrame = MarkerSite.Create(Library, "BeginFrame");
            public static readonly MarkerSite EndFrame = MarkerSite.Create(Library, "EndFrame");

            // EndFrame phases, in order. They match the phases DevTools' profiler times.
            public static readonly MarkerSite Styles = MarkerSite.Create(Library, "Styles");
            public static readonly MarkerSite Layout = MarkerSite.Create(Library, "Layout");
            public static readonly MarkerSite PostLayout = MarkerSite.Create(Library, "PostLayout");
            public static readonly MarkerSite Transforms = MarkerSite.Create(Library, "Transforms");
            public static readonly MarkerSite Culling = MarkerSite.Create(Library, "Culling");
            public static readonly MarkerSite Layered = MarkerSite.Create(Library, "Layered");
            public static readonly MarkerSite Interaction = MarkerSite.Create(Library, "Interaction");
            public static readonly MarkerSite Render = MarkerSite.Create(Library, "Render");
            public static readonly MarkerSite Upload = MarkerSite.Create(Library, "Upload");
            public static readonly MarkerSite Cleanup = MarkerSite.Create(Library, "Cleanup");

            // Steps inside Layout (ComputeLayout).
            public static readonly MarkerSite LayoutSubmit = MarkerSite.Create(Library, "Layout submit");
            public static readonly MarkerSite LayoutPrune = MarkerSite.Create(Library, "Layout prune");
            public static readonly MarkerSite LayoutSolve = MarkerSite.Create(Library, "Layout solve");
            public static readonly MarkerSite LayoutCopy = MarkerSite.Create(Library, "Layout copy");

            // Steps inside Render: the tree in place, then the raised layers it deferred.
            public static readonly MarkerSite RenderTree = MarkerSite.Create(Library, "Render tree");
            public static readonly MarkerSite RenderDeferred = MarkerSite.Create(Library, "Render deferred layers");

            // One span per element named with ElementBuilder.ProfileName, covering its whole subtree. Its own library
            // so a host can switch it separately.
            public static readonly MarkerSite NamedElement = MarkerSite.Create("Paper.Elements", "Named element");

            // Per-element work, one span each. Its own library because there are many per frame (one or more per
            // element), so a host can switch them separately from the phases above. Nested inside the phase they run in.
            public const string DetailLibrary = "Paper.Detail";
            public static readonly MarkerSite LayoutMeasureText = MarkerSite.Create(DetailLibrary, "Layout measure text");
            public static readonly MarkerSite LayoutContentSizer = MarkerSite.Create(DetailLibrary, "Layout content sizer");
            public static readonly MarkerSite RenderShadow = MarkerSite.Create(DetailLibrary, "Render shadow");
            public static readonly MarkerSite RenderBackdrop = MarkerSite.Create(DetailLibrary, "Render backdrop blur");
            public static readonly MarkerSite RenderBackground = MarkerSite.Create(DetailLibrary, "Render background");
            public static readonly MarkerSite RenderImage = MarkerSite.Create(DetailLibrary, "Render image");
            public static readonly MarkerSite RenderBorder = MarkerSite.Create(DetailLibrary, "Render border");
            public static readonly MarkerSite RenderText = MarkerSite.Create(DetailLibrary, "Render text");
            public static readonly MarkerSite RenderTextProcess = MarkerSite.Create(DetailLibrary, "Render text process");
            public static readonly MarkerSite RenderTextDraw = MarkerSite.Create(DetailLibrary, "Render text draw");
            public static readonly MarkerSite RenderRichTextDraw = MarkerSite.Create(DetailLibrary, "Render rich text draw");
            public static readonly MarkerSite RenderCommand = MarkerSite.Create(DetailLibrary, "Render custom command");

            // Per-frame values.
            public static readonly MarkerSite ElementCount = MarkerSite.Create(Library, "Elements");
            public static readonly MarkerSite LayoutMeasured = MarkerSite.Create(Library, "Layout measured nodes");
            public static readonly MarkerSite LayoutArranged = MarkerSite.Create(Library, "Layout arranged nodes");
            public static readonly MarkerSite LayoutMeasureHits = MarkerSite.Create(Library, "Layout measure cache hits");
            public static readonly MarkerSite LayoutArrangeHits = MarkerSite.Create(Library, "Layout arrange cache hits");
            public static readonly MarkerSite FrameMilliseconds = MarkerSite.Create(Library, "Frame ms");
        }

        // Per-frame counts reported as values (plots). Timing comes from the spans in PerfSites.
        internal sealed class PerfTallies
        {
            private const string Library = PerfSites.Library;

            // Layout: how much of the tree changed.
            public readonly MarkerTally LayoutStyleChanges = MarkerTally.Counter(Library, "Layout style changes");
            public readonly MarkerTally LayoutRemoved = MarkerTally.Counter(Library, "Layout nodes removed");

            // Culling: the bounds walk is one pass, so what varies its cost is how many nodes and how many need extra work.
            public readonly MarkerTally CullingNodes = MarkerTally.Counter(Library, "Culling nodes");
            public readonly MarkerTally CullingShadows = MarkerTally.Counter(Library, "Culling shadows");
            public readonly MarkerTally CullingTransformed = MarkerTally.Counter(Library, "Culling transformed children");
            public readonly MarkerTally CullingBreakouts = MarkerTally.Counter(Library, "Culling layer breakouts");

            // Render: how many elements were drawn or skipped.
            public readonly MarkerTally RenderElements = MarkerTally.Counter(Library, "Render elements");
            public readonly MarkerTally RenderCulled = MarkerTally.Counter(Library, "Render culled subtrees");
            public readonly MarkerTally RenderHidden = MarkerTally.Counter(Library, "Render hidden or transparent");
            public readonly MarkerTally RenderDeferred = MarkerTally.Counter(Library, "Render deferred elements");

            public void Flush()
            {
                LayoutStyleChanges.Flush();
                LayoutRemoved.Flush();

                CullingNodes.Flush();
                CullingShadows.Flush();
                CullingTransformed.Flush();
                CullingBreakouts.Flush();

                RenderElements.Flush();
                RenderCulled.Flush();
                RenderHidden.Flush();
                RenderDeferred.Flush();
            }
        }

        internal readonly PerfTallies _perf = new PerfTallies();

        // Publishes this frame's counts and tallies. Called at the end of EndFrame.
        private void ReportPerfValues()
        {
            // Always flushed, even with no handler, so tallies stop when a handler is removed.
            _perf.Flush();

            if (!Markers.Enabled) return;

            Markers.Value(PerfSites.ElementCount, CountOfAllElements);
            var layout = LayoutStatistics;
            Markers.Value(PerfSites.LayoutMeasured, layout.MeasuredNodes);
            Markers.Value(PerfSites.LayoutArranged, layout.ArrangedNodes);
            Markers.Value(PerfSites.LayoutMeasureHits, layout.MeasureCacheHits);
            Markers.Value(PerfSites.LayoutArrangeHits, layout.ArrangeCacheHits);
            Markers.Value(PerfSites.FrameMilliseconds, MillisecondsSpent);
        }
    }
}
