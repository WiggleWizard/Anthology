// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.PaperUI;
using Prowl.Perf;
using Prowl.Quill;
using Prowl.Vector;

namespace Tests;

// Markers.Handler is process-wide; these tests own it while they run.
[Collection(nameof(PerfMarkerTests))]
public class PerfMarkerTests : IDisposable
{
    // Records only calls made on the thread that created it, since other test classes render Papers in parallel.
    private sealed class RecordingHandler : IMarkerHandler
    {
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private readonly Dictionary<ulong, string> _open = new();
        private readonly Stack<ulong> _stack = new();
        private ulong _next = 1;

        public readonly List<string> Begun = new();
        public readonly Dictionary<string, double> Values = new();
        public readonly List<string> Errors = new();
        public readonly Dictionary<string, string> ParentOf = new();

        private bool Mine => Environment.CurrentManagedThreadId == _thread;

        public object? CreateSiteData(MarkerSite site) => null;

        public bool IsActive(MarkerSite site, object? siteData) => Mine;

        public bool TryBegin(MarkerSite site, object? siteData, out ulong token) => Begin(site.Name, out token);

        public bool TryBeginDynamic(MarkerSite site, object? siteData, string name, out ulong token) => Begin(name, out token);

        private bool Begin(string name, out ulong token)
        {
            token = 0;
            if (!Mine) return false;
            token = _next++;
            if (_stack.Count > 0) ParentOf[name] = _open[_stack.Peek()];
            _open[token] = name;
            _stack.Push(token);
            Begun.Add(name);
            return true;
        }

        public void End(ulong token)
        {
            if (_stack.Count == 0 || _stack.Peek() != token)
            {
                Errors.Add("out of order end of " + (_open.TryGetValue(token, out var n) ? n : token.ToString()));
                return;
            }
            _stack.Pop();
        }

        public void Value(MarkerSite site, object? siteData, double value)
        {
            if (Mine) Values[site.Name] = value;
        }

        public int OpenCount => _stack.Count;
    }

    private sealed class Renderer : ICanvasRenderer
    {
        public void Dispose() { }
        public object CreateTexture(uint width, uint height) => new Int2((int)width, (int)height);
        public Int2 GetTextureSize(object texture) => (Int2)texture;
        public void SetTextureData(object texture, IntRect bounds, byte[] data) { }
        public void RenderCalls(Canvas canvas, IReadOnlyList<DrawCall> drawCalls) { }
    }

    public void Dispose() => Markers.Handler = null;

    private static void Frame(Paper paper)
    {
        paper.BeginFrame(0.01f);
        using (paper.Box("panel").Size(100, 100).ProfileName("Panel A").Enter())
        {
            paper.Box("child").Size(50, 20).BackgroundColor(Prowl.Vector.Color.White);
        }
        paper.EndFrame();
    }

    [Fact]
    public void Frame_EmitsBalancedPhases_NamedSubtree_AndTallies()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;
        var paper = new Paper(new Renderer(), 200, 200, new FontAtlasSettings());

        // Tallies activate on the first frame's flush, so the second frame carries them.
        Frame(paper);
        Frame(paper);

        Assert.Empty(handler.Errors);
        Assert.Equal(0, handler.OpenCount);

        Assert.Contains("BeginFrame", handler.Begun);
        Assert.Contains("Layout solve", handler.Begun);
        Assert.Equal("Layout", handler.ParentOf["Layout solve"]);
        Assert.Equal("Render", handler.ParentOf["Render tree"]);
        Assert.Equal("Render tree", handler.ParentOf["Panel A"]);

        Assert.True(handler.Values["Render elements"] >= 2);
        Assert.True(handler.Values["Culling nodes"] >= 3);
        // Per-element detail spans nest inside the element being drawn, and Quill's drawing spans inside them.
        Assert.Contains("Render background", handler.Begun);
        Assert.Equal("Panel A", handler.ParentOf["Render background"]);
        Assert.Equal("Render background", handler.ParentOf["Rect filled"]);
        Assert.Equal("Upload", handler.ParentOf["Submit draw calls"]);
        Assert.True(handler.Values["Draw calls"] >= 1);
    }

    [Fact]
    public void NoHandler_RendersWithoutMarks()
    {
        Markers.Handler = null;
        var paper = new Paper(new Renderer(), 200, 200, new FontAtlasSettings());
        Frame(paper);
        Frame(paper);
    }
}
