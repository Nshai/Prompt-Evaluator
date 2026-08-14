# Abstractions

The contracts a front end drives and a test substitutes. One interface per file; the
implementations live in [`../Services/`](../Services), grouped by pipeline stage.

## What earns an interface

An interface earns its place when something would plausibly implement it differently — a store
backed by memory instead of Qdrant, a chat client that replays a recording, a search that returns
a fixed pack.

**Not everything in `Services` has one, and that is deliberate.** `CitationVerifier`,
`DerivedFigures`, `ContentDensity`, `CrossGroupContradictions` and `CheckPlanLint` are static
functions over their arguments. They have no state to fake and no second sensible implementation,
and wrapping them would add indirection that hides where the work happens.

## Why some of these are factories

Six contracts here create things rather than being things. Three reasons put a service in that
category:

**It needs a value only the caller has.** A search service is scoped to one case reference; a
check runner is scoped to one extracted canonical model. Neither is known when the container is
built, and threading them through as registrations would mean rebuilding the container per case.

**The caller owns its lifetime.** `ICaseDocumentStore` and `ICheckPlanRunner` are `IDisposable`
and used inside a `using` for one operation. Resolving them as singletons would quietly change
when a Qdrant connection closes and when a log file is flushed.

**Settings may not be the live ones.** The configuration screen tests a connection against
settings the user has typed and not yet saved, so it needs a client built from *those* settings
rather than the registered ones. That is what the optional `AppSettings` parameter is for.

## One thing worth knowing before you use them

`ICaseDocumentSearchServiceFactory.Create` takes an optional embedding generator, and passing the
caller's is usually the right thing to do. A run wraps the generator to count what it spends, and
every planned search embeds its text — a service bound to the registered generator instead would
leave a few hundred embedding calls per run uncounted, and the cost line would quietly
under-report. Omit it only where nothing is being measured.

Registration is in
[`../DependencyInjection/CasePipelineServiceCollectionExtensions.cs`](../DependencyInjection/CasePipelineServiceCollectionExtensions.cs),
which explains the lifetime chosen for each.
