using Xunit;

// Enable parallel test execution and set a higher thread limit for CI/local runs.
[assembly: CollectionBehavior(
    DisableTestParallelization = false, 
    MaxParallelThreads = 32
)]
