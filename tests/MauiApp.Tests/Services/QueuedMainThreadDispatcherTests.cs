using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Services;

/// <summary>#409: the queued test dispatcher must behave like MAUI's main-thread looper, or the
/// ordering tests built on it prove nothing.</summary>
public class QueuedMainThreadDispatcherTests
{
    [Fact]
    public void BeginInvokeOnMainThread_OffTheUiThread_QueuesUntilDrain()
    {
        var sut = new QueuedMainThreadDispatcher();
        var ran = false;

        sut.BeginInvokeOnMainThread(() => ran = true);

        Assert.False(ran);
        Assert.Equal(1, sut.PendingCount);

        Assert.Equal(1, sut.Drain());
        Assert.True(ran);
        Assert.Equal(0, sut.PendingCount);
    }

    [Fact]
    public void Drain_RunsPostsInTheOrderTheyWereQueued()
    {
        var sut = new QueuedMainThreadDispatcher();
        var order = new List<int>();

        for (var i = 0; i < 3; i++)
        {
            var n = i;
            sut.BeginInvokeOnMainThread(() => order.Add(n));
        }
        sut.Drain();

        Assert.Equal(new[] { 0, 1, 2 }, order);
    }

    [Fact]
    public void BeginInvokeOnMainThread_OnTheUiThread_RunsInPlaceAheadOfQueuedPosts()
    {
        var sut = new QueuedMainThreadDispatcher();
        var order = new List<string>();
        sut.BeginInvokeOnMainThread(() =>
        {
            order.Add("outer");
            sut.BeginInvokeOnMainThread(() => order.Add("nested")); // already on the UI thread
            order.Add("outer end");
        });
        sut.BeginInvokeOnMainThread(() => order.Add("next"));

        sut.Drain();

        Assert.Equal(new[] { "outer", "nested", "outer end", "next" }, order);
    }

    [Fact]
    public void BeginInvokeOnMainThread_FromAnotherThreadWhileDraining_IsQueuedAndStillDrained()
    {
        var sut = new QueuedMainThreadDispatcher();
        var order = new List<string>();
        sut.BeginInvokeOnMainThread(() =>
        {
            // A pump/timer thread posting while the UI thread is busy: queued, not run in place. A
            // dedicated Thread, not Task.Run: blocking on a not-yet-started task can inline it onto
            // this (draining) thread.
            var poster = new Thread(() => sut.BeginInvokeOnMainThread(() => order.Add("from another thread")));
            poster.Start();
            poster.Join();
            order.Add("outer end");
        });

        sut.Drain();

        Assert.Equal(new[] { "outer end", "from another thread" }, order);
    }
}
