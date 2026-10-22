using Xunit;

// UI automation launches a single real application, manipulates the shared desktop
// z-order/input state and kills previous instances. Tests must never run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
