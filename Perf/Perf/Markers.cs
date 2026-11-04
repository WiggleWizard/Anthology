namespace Prowl.Perf;

/// <summary>
/// Entry point libraries use to mark their own work, and where a host installs the one handler that receives the marks.
/// With no handler installed every call here is a single static read.
/// </summary>
/// <example>
/// <code>
/// private static readonly MarkerSite LayoutSite = MarkerSite.Create("Paper", "Layout");
///
/// MarkerScope phase = Markers.Begin(LayoutSite);
/// ComputeLayout();
/// phase.End();
/// </code>
/// </example>
public static class Markers
{
    private static IMarkerHandler? s_handler;

    /// <summary>
    /// The handler receiving marks, or null for none. One per process. Spans begun before a change end through the
    /// handler that began them.
    /// </summary>
    public static IMarkerHandler? Handler
    {
        get => Volatile.Read(ref s_handler);
        set => Volatile.Write(ref s_handler, value);
    }

    /// <summary>Whether a handler is installed.</summary>
    public static bool Enabled => Volatile.Read(ref s_handler) != null;

    /// <summary>Whether marks at <paramref name="site"/> are being recorded, to guard work done only for a mark.</summary>
    public static bool IsActive(MarkerSite site)
    {
        var handler = Volatile.Read(ref s_handler);
        if (handler == null) return false;
        return handler.IsActive(site, site.DataFor(handler));
    }

    /// <summary>Begins a span at <paramref name="site"/>. End it with <see cref="MarkerScope.End"/> or by disposing it.</summary>
    public static MarkerScope Begin(MarkerSite site)
    {
        var handler = Volatile.Read(ref s_handler);
        if (handler == null) return default;
        if (!handler.TryBegin(site, site.DataFor(handler), out ulong token)) return default;
        return new MarkerScope(handler, token);
    }

    /// <summary>
    /// Begins a span at <paramref name="site"/> named <paramref name="name"/>, for names built at runtime. Build the name
    /// only when <see cref="IsActive"/> is true so the unrecorded path allocates nothing.
    /// </summary>
    public static MarkerScope BeginDynamic(MarkerSite site, string name)
    {
        var handler = Volatile.Read(ref s_handler);
        if (handler == null) return default;
        if (!handler.TryBeginDynamic(site, site.DataFor(handler), name, out ulong token)) return default;
        return new MarkerScope(handler, token);
    }

    /// <summary>Records <paramref name="value"/> at <paramref name="site"/>.</summary>
    public static void Value(MarkerSite site, double value)
    {
        var handler = Volatile.Read(ref s_handler);
        if (handler == null) return;
        handler.Value(site, site.DataFor(handler), value);
    }
}
