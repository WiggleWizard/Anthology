namespace Prowl.Perf;

/// <summary>
/// What a host implements to receive marks from the Anthology libraries, installed with <see cref="Markers.Handler"/>.
/// The handler is called from whichever thread runs the marked code, so every member must be thread-safe.
/// </summary>
public interface IMarkerHandler
{
    /// <summary>
    /// Called the first time this handler sees <paramref name="site"/>. Whatever is returned is handed back with every
    /// later call for that site, so a handler can keep its own per-site object (a profiler's source location, a category).
    /// May be called more than once for a site when two threads reach it together.
    /// </summary>
    object? CreateSiteData(MarkerSite site);

    /// <summary>
    /// Whether marks at <paramref name="site"/> are recorded right now. Libraries ask this before doing work that only
    /// serves a mark, such as building a dynamic name.
    /// </summary>
    bool IsActive(MarkerSite site, object? siteData);

    /// <summary>
    /// Begins a span at <paramref name="site"/>. Return false to skip it (its category is off); the library then does not
    /// call <see cref="End"/> for it. <paramref name="token"/> is whatever the handler needs to end the span.
    /// </summary>
    bool TryBegin(MarkerSite site, object? siteData, out ulong token);

    /// <summary>As <see cref="TryBegin"/>, for a span whose name is built at runtime (<paramref name="name"/>).</summary>
    bool TryBeginDynamic(MarkerSite site, object? siteData, string name, out ulong token);

    /// <summary>
    /// Ends a span begun by this handler. Spans on one thread end in the reverse order they began; libraries keep to that.
    /// </summary>
    void End(ulong token);

    /// <summary>Records a number at <paramref name="site"/>, such as a count or a size, once per frame.</summary>
    void Value(MarkerSite site, object? siteData, double value);
}
