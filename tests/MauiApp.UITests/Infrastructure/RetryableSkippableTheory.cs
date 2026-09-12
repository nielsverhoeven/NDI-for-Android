using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NdiForAndroid.UITests.Infrastructure;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[XunitTestCaseDiscoverer("NdiForAndroid.UITests.Infrastructure.RetryableSkippableTheoryDiscoverer", "NdiForAndroid.UITests")]
public sealed class RetryableSkippableTheoryAttribute : TheoryAttribute
{
    public int MaxRetries { get; set; } = 1;
}

public sealed class RetryableSkippableTheoryDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
{
    private readonly TheoryDiscoverer _theoryDiscoverer = new(diagnosticMessageSink);

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
    {
        var maxRetries = factAttribute.GetNamedArgument<int>(nameof(RetryableSkippableTheoryAttribute.MaxRetries));
        if (maxRetries <= 0)
            maxRetries = 1;

        // The Retryable* attributes take no custom skipping-exception types, so the set is exactly
        // the one Skip.If throws. Avoids depending on SkippableFactDiscoverer's internal helper.
        var skippingExceptionNames = new[] { typeof(Xunit.SkipException).FullName! };
        var defaultMethodDisplay = discoveryOptions.MethodDisplayOrDefault();

        foreach (var testCase in _theoryDiscoverer.Discover(discoveryOptions, testMethod, factAttribute))
        {
            if (testCase is XunitTheoryTestCase)
            {
                yield return new RetryableSkippableTheoryTestCase(
                    maxRetries, skippingExceptionNames, diagnosticMessageSink,
                    defaultMethodDisplay, discoveryOptions.MethodDisplayOptionsOrDefault(), testCase.TestMethod);
            }
            else
            {
                yield return new RetryableSkippableFactTestCase(
                    maxRetries, skippingExceptionNames, diagnosticMessageSink,
                    defaultMethodDisplay, discoveryOptions.MethodDisplayOptionsOrDefault(),
                    testCase.TestMethod, testCase.TestMethodArguments);
            }
        }
    }
}

public sealed class RetryableSkippableTheoryTestCase : SkippableTheoryTestCase
{
    private int _maxRetries;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Called by the de-serializer", true)]
    public RetryableSkippableTheoryTestCase() { }

    public RetryableSkippableTheoryTestCase(
        int maxRetries, string[] skippingExceptionNames, IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay, TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod)
        : base(skippingExceptionNames, diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod)
    {
        _maxRetries = maxRetries;
    }

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);
        data.AddValue(nameof(_maxRetries), _maxRetries);
    }

    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);
        _maxRetries = data.GetValue<int>(nameof(_maxRetries));
    }

    public override async Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
        ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
    {
        var totalAttempts = _maxRetries + 1;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            var isFinalAttempt = attempt == totalAttempts;
            var bus = isFinalAttempt ? messageBus : new BufferingMessageBus();

            var summary = await base.RunAsync(
                diagnosticMessageSink, bus, constructorArguments,
                new ExceptionAggregator(aggregator), cancellationTokenSource).ConfigureAwait(false);

            var genuinelyFailed = summary.Failed > 0;
            RetryLog.Append(TestMethod.Method.Name, attempt, totalAttempts, passed: !genuinelyFailed);

            if (!genuinelyFailed || isFinalAttempt)
            {
                if (bus is BufferingMessageBus buffered)
                    foreach (var message in buffered.Messages)
                        messageBus.QueueMessage(message);

                return summary;
            }
        }

        throw new InvalidOperationException("Unreachable: the loop always returns on its final attempt.");
    }
}
