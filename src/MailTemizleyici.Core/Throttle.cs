namespace MailTemizleyici.Core;

public sealed class Throttle
{
    static readonly TimeSpan FirstPenalty = TimeSpan.FromSeconds(5);
    static readonly TimeSpan MaxPenalty = TimeSpan.FromSeconds(60);

    readonly Func<DateTimeOffset> now;
    readonly Func<TimeSpan, CancellationToken, Task> delay;
    readonly double maxUnitsPerMinute;
    readonly int maxBatchSize;
    double unitsPerMinute;
    double tokens;
    DateTimeOffset last;
    DateTimeOffset blockedUntil;
    TimeSpan penalty;

    public Throttle(
        double unitsPerMinute, int batchSize,
        Func<DateTimeOffset>? now = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.now = now ?? (() => DateTimeOffset.UtcNow);
        this.delay = delay ?? Task.Delay;
        maxUnitsPerMinute = this.unitsPerMinute = unitsPerMinute;
        maxBatchSize = BatchSize = batchSize;
        tokens = Capacity;
        last = this.now();
    }

    public int BatchSize { get; private set; }

    public double UnitsPerMinute => unitsPerMinute;

    double Capacity => unitsPerMinute / 60 * 10;

    public async Task AcquireAsync(int units, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Refill();
            TimeSpan wait;
            if (now() < blockedUntil) wait = blockedUntil - now();
            else if (tokens >= units || tokens >= Capacity) { tokens -= units; return; }
            else wait = TimeSpan.FromSeconds((Math.Min(units, Capacity) - tokens) / (unitsPerMinute / 60));
            await delay(wait, ct);
        }
    }

    public void OnSuccess()
    {
        penalty = TimeSpan.Zero;
        unitsPerMinute = Math.Min(maxUnitsPerMinute, unitsPerMinute * 1.05);
        BatchSize = Math.Min(maxBatchSize, BatchSize + 1);
    }

    public void OnFailure(bool rateLimited)
    {
        penalty = penalty == TimeSpan.Zero ? FirstPenalty : TimeSpan.FromSeconds(Math.Min(MaxPenalty.TotalSeconds, penalty.TotalSeconds * 2));
        blockedUntil = now() + penalty;
        tokens = 0;
        if (!rateLimited) return;
        unitsPerMinute = Math.Max(maxUnitsPerMinute * 0.4, unitsPerMinute * 0.8);
        BatchSize = Math.Max(5, BatchSize * 3 / 4);
    }

    void Refill()
    {
        var current = now();
        tokens = Math.Min(Capacity, tokens + (current - last).TotalSeconds * unitsPerMinute / 60);
        last = current;
    }
}
