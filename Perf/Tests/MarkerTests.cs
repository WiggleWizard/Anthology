using Prowl.Perf;

namespace Prowl.Perf.Tests;

// Markers.Handler is process-wide, so these tests must not run in parallel with each other.
[Collection(nameof(MarkerTests))]
public sealed class MarkerTests : IDisposable
{
    private sealed class RecordingHandler : IMarkerHandler
    {
        public bool Active = true;
        public int SiteDataCreated;
        public readonly List<string> Calls = new();
        private ulong _next = 1;

        public object? CreateSiteData(MarkerSite site)
        {
            SiteDataCreated++;
            return "data:" + site.Name;
        }

        public bool IsActive(MarkerSite site, object? siteData) => Active;

        public bool TryBegin(MarkerSite site, object? siteData, out ulong token)
        {
            token = 0;
            if (!Active) return false;
            token = _next++;
            Calls.Add($"begin {site.Name} {siteData} {token}");
            return true;
        }

        public bool TryBeginDynamic(MarkerSite site, object? siteData, string name, out ulong token)
        {
            token = 0;
            if (!Active) return false;
            token = _next++;
            Calls.Add($"begin {site.Name}:{name} {token}");
            return true;
        }

        public void End(ulong token) => Calls.Add($"end {token}");

        public void Value(MarkerSite site, object? siteData, double value) => Calls.Add($"value {site.Name} {value}");
    }

    private static readonly MarkerSite Site = MarkerSite.Create("Test", "Work");

    public void Dispose() => Markers.Handler = null;

    [Fact]
    public void NoHandler_DoesNothing()
    {
        Markers.Handler = null;

        MarkerScope scope = Markers.Begin(Site);
        Assert.False(scope.IsBegun);
        scope.End();
        Assert.False(Markers.IsActive(Site));
        Markers.Value(Site, 1);
    }

    [Fact]
    public void BeginEnd_ReachesHandlerWithSiteData()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;

        MarkerScope scope = Markers.Begin(Site);
        Assert.True(scope.IsBegun);
        scope.End();

        Assert.Equal(new[] { "begin Work data:Work 1", "end 1" }, handler.Calls);
    }

    [Fact]
    public void End_IsIdempotent()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;

        MarkerScope scope = Markers.Begin(Site);
        scope.End();
        scope.End();
        scope.Dispose();

        Assert.Single(handler.Calls, c => c.StartsWith("end"));
    }

    [Fact]
    public void InactiveSite_SkipsEnd()
    {
        var handler = new RecordingHandler { Active = false };
        Markers.Handler = handler;

        MarkerScope scope = Markers.Begin(Site);
        Assert.False(scope.IsBegun);
        scope.End();

        Assert.Empty(handler.Calls);
        Assert.False(Markers.IsActive(Site));
    }

    [Fact]
    public void SiteData_CreatedOncePerHandler_AndAgainForANewHandler()
    {
        var site = MarkerSite.Create("Test", "Cached");
        var first = new RecordingHandler();
        Markers.Handler = first;
        Markers.Begin(site).End();
        Markers.Begin(site).End();
        Assert.Equal(1, first.SiteDataCreated);

        var second = new RecordingHandler();
        Markers.Handler = second;
        Markers.Begin(site).End();
        Assert.Equal(1, second.SiteDataCreated);
    }

    [Fact]
    public void Scope_EndsThroughTheHandlerThatBeganIt()
    {
        var first = new RecordingHandler();
        Markers.Handler = first;
        MarkerScope scope = Markers.Begin(Site);

        var second = new RecordingHandler();
        Markers.Handler = second;
        scope.End();

        Assert.Contains("end 1", first.Calls);
        Assert.Empty(second.Calls);
    }

    [Fact]
    public void Dynamic_AndValue_ReachHandler()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;

        Markers.BeginDynamic(Site, "Hierarchy").End();
        Markers.Value(Site, 42);

        Assert.Equal(new[] { "begin Work:Hierarchy 1", "end 1", "value Work 42" }, handler.Calls);
    }

    [Fact]
    public void Tally_IsInactiveUntilFirstFlush_ThenReportsAndResets()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;
        var tally = MarkerTally.Timed("Test", "Text");

        Assert.False(tally.IsActive);
        Assert.Equal(0, tally.Begin());
        tally.Flush();
        Assert.True(tally.IsActive);
        Assert.Empty(handler.Calls);

        tally.End(tally.Begin());
        tally.End(tally.Begin());
        tally.Flush();

        Assert.Contains(handler.Calls, c => c.StartsWith("value Text ms "));
        Assert.Contains("value Text count 2", handler.Calls);

        handler.Calls.Clear();
        tally.Flush();
        Assert.Contains("value Text count 0", handler.Calls);
    }

    [Fact]
    public void Counter_ReportsCountOnly_AndStopsWhenHandlerRemoved()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;
        var counter = MarkerTally.Counter("Test", "Nodes");
        counter.Flush();

        counter.Add(3);
        counter.Add();
        counter.Flush();
        Assert.Equal(new[] { "value Nodes 4" }, handler.Calls);

        Markers.Handler = null;
        counter.Flush();
        Assert.False(counter.IsActive);
        counter.Add(5);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public void Tally_AddTicks_ClampsNegative_AndCounts()
    {
        var handler = new RecordingHandler();
        Markers.Handler = handler;
        var tally = MarkerTally.Timed("Test", "Other");
        tally.Flush();

        tally.AddTicks(-50);
        Assert.Equal(0, tally.Ticks);
        tally.AddTicks(100);
        Assert.Equal(100, tally.Ticks);
        tally.Flush();

        Assert.Contains("value Other count 2", handler.Calls);
        Assert.Equal(0, tally.Ticks);
    }

    [Fact]
    public void Site_RecordsCallerLocation()
    {
        var site = MarkerSite.Create("Test", "Here");
        Assert.Equal(nameof(Site_RecordsCallerLocation), site.Member);
        Assert.EndsWith("MarkerTests.cs", site.File);
        Assert.True(site.Line > 0);
        Assert.Equal("Test/Here", site.ToString());
    }
}
