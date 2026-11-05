using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class MailDatabaseTests
{
    static MessageRow Row(string id, string subject = "s") =>
        new(id, "t" + id, "a@x.com", "A", "x.com", subject, 1000, 2000, "INBOX,UNREAD", false, true, false, "PRIMARY", true, false);

    [Fact]
    public void Upsert_SameIdTwice_KeepsOneRow()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1"), Row("2")]);
        db.Upsert([Row("1", "changed")]);
        Assert.Equal(2, db.Count());
    }

    [Fact]
    public void EnsureAccount_SameAccountAnyCase_Passes()
    {
        using var db = new MailDatabase(":memory:");
        db.EnsureAccount("Me@example.com");
        db.EnsureAccount("me@example.com");
    }

    [Fact]
    public void EnsureAccount_OtherAccount_Throws()
    {
        using var db = new MailDatabase(":memory:");
        db.EnsureAccount("a@example.com");
        var ex = Assert.Throws<AccountMismatchException>(() => db.EnsureAccount("b@example.com"));
        Assert.Equal("a@example.com", ex.Stored);
    }

    [Fact]
    public void EnsureAccount_MessagesWithoutAccount_Throws()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1")]);
        var ex = Assert.Throws<AccountMismatchException>(() => db.EnsureAccount("a@example.com"));
        Assert.Null(ex.Stored);
    }

    [Fact]
    public void GetSenderMessages_ReturnsOnlyThatSenderNewestFirst()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1") with { Date = 100 }, Row("2") with { Date = 300 }, Row("3") with { SenderAddress = "other@x.com" }]);
        Assert.Equal(["2", "1"], db.GetSenderMessages("a@x.com").Select(m => m.Id));
    }

    [Fact]
    public void GetStarredCounts_CountsOnlyStarredPerSender()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1"), Row("2"), Row("3") with { IsStarred = false }, Row("4") with { SenderAddress = "b@x.com" }]);
        Assert.Equal(new Dictionary<string, int> { ["a@x.com"] = 2, ["b@x.com"] = 1 }, db.GetStarredCounts());
    }

    [Fact]
    public void CountTrashedByApp_CountsOnlyLoggedAndNotUndone()
    {
        using var db = new MailDatabase(":memory:");
        db.LogActions(["1", "2", "3"], "Trash", 1);
        db.LogActions(["4", "5"], "Trash", 2);
        db.MarkUndone(["4", "5"], 2);
        Assert.Equal(3, db.CountTrashedByApp());
    }

    [Fact]
    public void State_RoundTripsAndOverwrites()
    {
        using var db = new MailDatabase(":memory:");
        Assert.Null(db.LoadState());
        db.SaveState(new InventoryState("tok", 500, "2026-11-04", null));
        db.SaveState(new InventoryState(null, 900, "2026-11-04", "2026-11-05"));
        Assert.Equal(new InventoryState(null, 900, "2026-11-04", "2026-11-05"), db.LoadState());
    }

    [Fact]
    public void FileDatabase_SurvivesReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "", "mail.db");
        try
        {
            using (var db = new MailDatabase(path)) db.Upsert([Row("1")]);
            using var reopened = new MailDatabase(path);
            Assert.Equal(1, reopened.Count());
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
