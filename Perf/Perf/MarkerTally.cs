using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Prowl.Perf;

/// <summary>
/// Adds up small, frequent pieces of work over a frame (each element's text, each shadow) and reports the total once,
/// instead of one span per piece, which at thousands per frame would cost more than the work and flood the profiler.
/// A timed tally reports "&lt;name&gt; ms" and "&lt;name&gt; count"; a counter reports "&lt;name&gt;" only.
/// </summary>
/// <remarks>
/// Not thread-safe: keep one per owner that runs on one thread, such as a field of a UI context. Call
/// <see cref="Flush"/> once per frame; it reports the frame's totals, resets them, and re-reads whether the tally's
/// site is recorded, so while it is not (no handler, or the host's category is off) <see cref="Begin"/> is a field read.
/// </remarks>
public sealed class MarkerTally
{
    private readonly MarkerSite? _time;
    private readonly MarkerSite _count;
    private bool _active;
    private long _ticks;
    private long _hits;

    private MarkerTally(MarkerSite? time, MarkerSite count)
    {
        _time = time;
        _count = count;
    }

    /// <summary>A tally of time and count, for work wrapped in <see cref="Begin"/> / <see cref="End"/>.</summary>
    public static MarkerTally Timed(string library, string name,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return new MarkerTally(
            MarkerSite.Create(library, name + " ms", member, file, line),
            MarkerSite.Create(library, name + " count", member, file, line));
    }

    /// <summary>A count only, for events counted with <see cref="Add"/>.</summary>
    public static MarkerTally Counter(string library, string name,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return new MarkerTally(null, MarkerSite.Create(library, name, member, file, line));
    }

    /// <summary>Whether this frame's work is being tallied. Fixed between <see cref="Flush"/> calls.</summary>
    public bool IsActive => _active;

    /// <summary>Starts timing one piece of work. Pass the result to <see cref="End"/>; 0 while inactive.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Begin()
    {
        if (!_active) return 0;
        return Stopwatch.GetTimestamp();
    }

    /// <summary>Adds the time since <paramref name="started"/> and counts one piece of work.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End(long started)
    {
        if (started == 0) return;
        _ticks += Stopwatch.GetTimestamp() - started;
        _hits++;
    }

    /// <summary>Time added since the last <see cref="Flush"/>, in <see cref="Stopwatch"/> ticks.</summary>
    public long Ticks => _ticks;

    /// <summary>
    /// Adds time measured elsewhere, in <see cref="Stopwatch"/> ticks, such as a remainder worked out from other tallies.
    /// Ignored while inactive; negative values are clamped to 0.
    /// </summary>
    public void AddTicks(long ticks, long count = 1)
    {
        if (!_active) return;
        if (ticks > 0) _ticks += ticks;
        _hits += count;
    }

    /// <summary>Counts <paramref name="count"/> events.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(long count = 1)
    {
        if (_active) _hits += count;
    }

    /// <summary>Reports the totals since the last call, resets them, and refreshes <see cref="IsActive"/>.</summary>
    public void Flush()
    {
        if (_active)
        {
            if (_time != null)
                Markers.Value(_time, _ticks * 1000.0 / Stopwatch.Frequency);
            Markers.Value(_count, _hits);
        }

        _ticks = 0;
        _hits = 0;
        _active = Markers.IsActive(_count);
    }
}
