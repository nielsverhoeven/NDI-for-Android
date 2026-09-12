using Xunit;

// Two live Appium sessions must never run concurrently against one CI emulator. This makes the
// whole assembly run one collection at a time instead of parallelizing across collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
