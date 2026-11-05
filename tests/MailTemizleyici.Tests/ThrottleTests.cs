using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class ThrottleTests
{
    sealed class Clock
    {
        public DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        public TimeSpan Waited;

        public Task Delay(TimeSpan span, CancellationToken _)
        {
            Now += span;
            Waited += span;
            return Task.CompletedTask;
        }
    }

    static Throttle Make(Clock clock, double perMinute = 6000, int batch = 10) =>
        new(perMinute, batch, () => clock.Now, clock.Delay);

    [Fact]
    public async Task AcquireAsync_BurstIsFreeThenPaced()
    {
        var clock = new Clock();
        var throttle = Make(clock);

        await throttle.AcquireAsync(1000, CancellationToken.None);
        Assert.Equal(TimeSpan.Zero, clock.Waited);

        await throttle.AcquireAsync(200, CancellationToken.None);
        Assert.InRange(clock.Waited.TotalSeconds, 1.9, 2.1);
    }

    [Fact]
    public async Task AcquireAsync_SustainedRateMatchesBudget()
    {
        var clock = new Clock();
        var throttle = Make(clock);

        for (var i = 0; i < 100; i++) await throttle.AcquireAsync(200, CancellationToken.None);

        var units = 100 * 200;
        var minutes = clock.Waited.TotalMinutes + 10.0 / 60;
        Assert.InRange(units / minutes, 5900, 6100);
    }

    [Fact]
    public async Task OnFailure_RateLimited_BlocksSlowsAndShrinksBatch()
    {
        var clock = new Clock();
        var throttle = Make(clock);

        throttle.OnFailure(rateLimited: true);
        await throttle.AcquireAsync(20, CancellationToken.None);
        Assert.True(clock.Waited >= TimeSpan.FromSeconds(5));
        Assert.Equal(4800, throttle.UnitsPerMinute, 1);
        Assert.Equal(7, throttle.BatchSize);

        throttle.OnFailure(rateLimited: true);
        var before = clock.Waited;
        await throttle.AcquireAsync(20, CancellationToken.None);
        Assert.True(clock.Waited - before >= TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void OnFailure_NeverDropsBelowFortyPercentOrFiveItems()
    {
        var throttle = Make(new Clock());
        for (var i = 0; i < 30; i++) throttle.OnFailure(rateLimited: true);
        Assert.Equal(2400, throttle.UnitsPerMinute, 1);
        Assert.Equal(5, throttle.BatchSize);
    }

    [Fact]
    public void OnSuccess_RecoversUpToTheOriginalBudget()
    {
        var throttle = Make(new Clock());
        throttle.OnFailure(rateLimited: true);
        for (var i = 0; i < 50; i++) throttle.OnSuccess();
        Assert.Equal(6000, throttle.UnitsPerMinute, 1);
        Assert.Equal(10, throttle.BatchSize);
    }

    [Fact]
    public async Task AcquireAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Make(new Clock()).AcquireAsync(20, cts.Token));
    }
}
