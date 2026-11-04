namespace Prowl.Perf;

/// <summary>
/// A span begun by <see cref="Markers.Begin"/>. Ending it more than once does nothing, so sequential phases can
/// <see cref="End"/> one and assign the next to the same variable. The default value is a span that was never begun.
/// </summary>
/// <remarks>
/// Do not call <see cref="End"/> on a <c>using var</c> local: it is read-only, so the call runs on a copy and the span is
/// ended again when the local is disposed. Either let <c>using</c> end it, or hold it in a plain local and call
/// <see cref="End"/> yourself.
/// </remarks>
public struct MarkerScope : IDisposable
{
    private IMarkerHandler? _handler;
    private readonly ulong _token;

    internal MarkerScope(IMarkerHandler handler, ulong token)
    {
        _handler = handler;
        _token = token;
    }

    /// <summary>Whether a handler began this span, as opposed to it being skipped.</summary>
    public readonly bool IsBegun => _handler != null;

    /// <summary>Ends the span through the handler that began it. Safe to call again, and on a span never begun.</summary>
    public void End()
    {
        var handler = _handler;
        if (handler == null) return;
        _handler = null;
        handler.End(_token);
    }

    /// <summary>Same as <see cref="End"/>.</summary>
    public void Dispose() => End();
}
