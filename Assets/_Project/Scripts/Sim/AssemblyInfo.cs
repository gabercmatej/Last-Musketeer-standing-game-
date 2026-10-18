using System.Runtime.CompilerServices;

// Tests drive a few internal pieces directly (shot tracing, tick context) for cases the
// public API can't reach in one tick.
[assembly: InternalsVisibleTo("Musketeers.Sim.Tests")]
