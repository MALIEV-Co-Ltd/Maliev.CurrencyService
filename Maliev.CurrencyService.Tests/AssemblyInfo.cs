using Xunit;

// Disable all test parallelization to prevent test isolation issues with shared database/cache state
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
