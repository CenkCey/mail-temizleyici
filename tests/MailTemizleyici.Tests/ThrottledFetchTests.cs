using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class ThrottledFetchTests
{
    static Throttle Fast(int batch = 4)
    {
        var now = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        return new Throttle(1_000_000, batch, () => now, (span, _) =>
        {
            now += span;
            return Task.CompletedTask;
        });
    }

    static IReadOnlyList<string> Ids(int n) => Enumerable.Range(1, n).Select(i => i.ToString()).ToList();

    [Fact]
    public async Task RunAsync_RetriesOnlyTheFailedIdsAndKeepsTheRest()
    {
        var calls = new List<string>();
        var failedOnce = new HashSet<string> { "3", "7" };
        Task<ChunkResult<string>> Fetch(IReadOnlyList<string> chunk, CancellationToken _)
        {
            calls.AddRange(chunk);
            var failed = chunk.Where(id => failedOnce.Remove(id)).ToList();
            return Task.FromResult(new ChunkResult<string>(chunk.Except(failed).ToList(), failed, true, "429"));
        }
        var persisted = new List<string>();

        var items = await ThrottledFetch.RunAsync(Ids(10), Fast(), 20, Fetch, persisted.AddRange, 3, CancellationToken.None);

        Assert.Equal(Ids(10).Order(), items.Order());
        Assert.Equal(items.Order(), persisted.Order());
        Assert.Equal(12, calls.Count);
        Assert.Equal(2, calls.Count(id => id == "3"));
        Assert.Equal(2, calls.Count(id => id == "7"));
        Assert.Equal(1, calls.Count(id => id == "1"));
    }

    [Fact]
    public async Task RunAsync_AllFailing_GivesUpWithGmailMessage()
    {
        Task<ChunkResult<string>> Fetch(IReadOnlyList<string> chunk, CancellationToken _) =>
            Task.FromResult(new ChunkResult<string>([], chunk, true, "quota"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ThrottledFetch.RunAsync(Ids(3), Fast(), 20, Fetch, null, 2, CancellationToken.None));

        Assert.Contains("quota", ex.Message);
    }

    [Fact]
    public async Task RunAsync_RateLimitShrinksTheNextChunk()
    {
        var sizes = new List<int>();
        var first = true;
        Task<ChunkResult<string>> Fetch(IReadOnlyList<string> chunk, CancellationToken _)
        {
            sizes.Add(chunk.Count);
            if (!first) return Task.FromResult(new ChunkResult<string>(chunk, [], false, null));
            first = false;
            return Task.FromResult(new ChunkResult<string>([], chunk, true, "429"));
        }

        await ThrottledFetch.RunAsync(Ids(20), Fast(batch: 20), 20, Fetch, null, 3, CancellationToken.None);

        Assert.Equal(20, sizes[0]);
        Assert.True(sizes[1] < 20);
    }

    [Fact]
    public async Task RunAsync_EmptyInput_DoesNothing()
    {
        var items = await ThrottledFetch.RunAsync<string>([], Fast(), 20,
            (_, _) => throw new InvalidOperationException("must not fetch"), null, 3, CancellationToken.None);
        Assert.Empty(items);
    }
}
