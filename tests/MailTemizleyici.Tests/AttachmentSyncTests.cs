using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class AttachmentSyncTests
{
    static MessageRow Row(string id, bool attachment = false) =>
        new(id, "t" + id, "a@x.com", "A", "x.com", "s", 1000, 2000, "INBOX", true, false, false, "NONE", false, attachment);

    [Fact]
    public async Task RunAsync_MarksListedIdsAcrossPagesAndClearsTheRest()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1"), Row("2"), Row("3", attachment: true)]);
        var pages = new Dictionary<string, IdPage>
        {
            [""] = new(["1"], "next"),
            ["next"] = new(["2"], null)
        };

        var count = await AttachmentSync.RunAsync(db, (token, _) => Task.FromResult(pages[token ?? ""]), TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(["1", "2"], db.ReadMessages().Where(m => m.HasAttachment).Select(m => m.Id).Order());
    }

    [Fact]
    public async Task RunAsync_Cancelled_LeavesDatabaseUntouched()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1", attachment: true)]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AttachmentSync.RunAsync(db, (_, _) => Task.FromResult(new IdPage([], null)), TimeSpan.Zero, cts.Token));

        Assert.True(db.ReadMessages().Single().HasAttachment);
    }
}
