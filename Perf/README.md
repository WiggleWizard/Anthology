# Prowl.Perf

Performance instrumentation hooks for the Anthology libraries. Libraries mark their own work; a host installs one
handler that forwards the marks to whichever profiler it uses (Tracy, Superluminal, a custom overlay). Nothing in this
package profiles anything by itself, and it has no dependencies.

## Marking work in a library

Create one `MarkerSite` per call site and keep it in a static field. Names are fixed text.

```csharp
using Prowl.Perf;

private static readonly MarkerSite LayoutSite = MarkerSite.Create("Paper", "Layout");
private static readonly MarkerSite ElementCountSite = MarkerSite.Create("Paper", "Elements");

MarkerScope phase = Markers.Begin(LayoutSite);
ComputeLayout();
phase.End();

Markers.Value(ElementCountSite, elementCount);
```

For names built at runtime, guard the string building so the unrecorded path allocates nothing:

```csharp
if (Markers.IsActive(ElementSite))
    scope = Markers.BeginDynamic(ElementSite, element.Name);
```

`MarkerScope.End` can be called more than once, so sequential phases can end one and assign the next to the same
variable. Spans on one thread must end in the reverse order they began.

## Receiving marks in a host

Implement `IMarkerHandler` and set `Markers.Handler`. `CreateSiteData` runs once per site per handler, so map the
site's `Library` to your profiler's category and source location there and return it; it comes back with every later
call. Return false from `TryBegin` to skip a site whose category is switched off.

The handler is called from whichever thread runs the marked code, so it must be thread-safe.

## Cost

- No handler: one static read per mark.
- Handler installed, site off: one interface call returning false.
- Handler installed, site on: whatever the handler does.
