using System.Runtime.CompilerServices;

namespace Prowl.Perf;

/// <summary>
/// One place in a library's code that is marked: which library it belongs to, a fixed name, and the source location.
/// Create one per call site and keep it in a <c>static readonly</c> field next to the code it measures, so the handler
/// sees the same site every time and can cache what it needs for it (see <see cref="IMarkerHandler.CreateSiteData"/>).
/// </summary>
/// <remarks>
/// The name is fixed text. Names built at runtime (one per element, one per file) go through
/// <see cref="Markers.BeginDynamic"/>, which takes the varying part separately so the site itself stays fixed.
/// </remarks>
public sealed class MarkerSite
{
    // The handler this site's data was created for, paired with that data. Swapped as one object so a reader on
    // another thread never sees one handler's data paired with a different handler.
    private sealed class Binding
    {
        public readonly IMarkerHandler Handler;
        public readonly object? Data;

        public Binding(IMarkerHandler handler, object? data)
        {
            Handler = handler;
            Data = data;
        }
    }

    private Binding? _binding;

    private MarkerSite(string library, string name, string member, string file, int line)
    {
        Library = library;
        Name = name;
        Member = member;
        File = file;
        Line = line;
    }

    /// <summary>
    /// Creates the site for the calling location. <paramref name="library"/> groups sites so a host can map them to its own
    /// categories (for example "Paper", "Paper.Elements", "Quill", "Scribe", "Origami"); <paramref name="name"/> is what
    /// the profiler shows.
    /// </summary>
    public static MarkerSite Create(string library, string name,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (library == null) throw new ArgumentNullException(nameof(library));
        if (name == null) throw new ArgumentNullException(nameof(name));
        return new MarkerSite(library, name, member, file, line);
    }

    /// <summary>The library the site belongs to, used by hosts to pick a category.</summary>
    public string Library { get; }

    /// <summary>What the profiler lists the marked work as.</summary>
    public string Name { get; }

    /// <summary>The member the site was created in.</summary>
    public string Member { get; }

    /// <summary>The source file the site was created in.</summary>
    public string File { get; }

    /// <summary>The source line the site was created on.</summary>
    public int Line { get; }

    /// <inheritdoc />
    public override string ToString() => Library + "/" + Name;

    /// <summary>
    /// What <paramref name="handler"/> keeps for this site, created on first use and again whenever a different handler
    /// asks. Two threads reaching a new site together may both create it; the handler's data must tolerate that.
    /// </summary>
    internal object? DataFor(IMarkerHandler handler)
    {
        Binding? binding = Volatile.Read(ref _binding);
        if (binding != null && ReferenceEquals(binding.Handler, handler))
            return binding.Data;

        var created = new Binding(handler, handler.CreateSiteData(this));
        Volatile.Write(ref _binding, created);
        return created.Data;
    }
}
