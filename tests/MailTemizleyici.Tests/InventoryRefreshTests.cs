using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class InventoryRefreshTests
{
    static MessageRow Row(string id) =>
        new(id, "t" + id, "a@x.com", "A", "x.com", "s", 1000, 2000, "INBOX", true, false, false, "NONE", false, false);

    static MailDatabase Seeded(params string[] ids)
    {
        var db = new MailDatabase(":memory:");
        db.Upsert(ids.Select(Row));
        db.SaveState(new InventoryState(null, ids.Length, "2026-11-01", "2026-11-02"));
        return db;
    }

    static Task<IReadOnlyList<MessageRow>> Fetch(IReadOnlyList<string> ids, List<string> log)
    {
        log.AddRange(ids);
        return Task.FromResult<IReadOnlyList<MessageRow>>(ids.Select(Row).ToList());
    }

    [Fact]
    public async Task RunAsync_FetchesOnlyNewMailAndStopsAtFirstKnownPage()
    {
        using var db = Seeded("1", "2", "3");
        var pages = new Dictionary<string, IdPage>
        {
            [""] = new(["5", "4", "3"], "next"),
            ["next"] = new(["2", "1"], null)
        };
        var fetched = new List<string>();

        var added = await InventoryRefresh.RunAsync(db, (t, _) => Task.FromResult(pages[t ?? ""]),
            (ids, _) => Fetch(ids, fetched), TimeSpan.Zero, null, CancellationToken.None);

        Assert.Equal(2, added);
        Assert.Equal(["5", "4"], fetched);
        Assert.Equal(5, db.Count());
        Assert.Equal(5, db.LoadState()!.FetchedCount);
    }

    [Fact]
    public async Task RunAsync_ContinuesWhileWholePagesAreNew()
    {
        using var db = Seeded("1");
        var pages = new Dictionary<string, IdPage>
        {
            [""] = new(["4", "3"], "next"),
            ["next"] = new(["2", "1"], null)
        };
        var fetched = new List<string>();

        var added = await InventoryRefresh.RunAsync(db, (t, _) => Task.FromResult(pages[t ?? ""]),
            (ids, _) => Fetch(ids, fetched), TimeSpan.Zero, null, CancellationToken.None);

        Assert.Equal(3, added);
        Assert.Equal(4, db.Count());
    }

    [Fact]
    public async Task RunAsync_UnfinishedInventory_Throws()
    {
        using var db = new MailDatabase(":memory:");
        db.SaveState(new InventoryState("tok", 10, "2026-11-01", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => InventoryRefresh.RunAsync(
            db, (_, _) => Task.FromResult(new IdPage([], null)), (_, _) => Fetch([], []), TimeSpan.Zero, null, CancellationToken.None));
    }
}
