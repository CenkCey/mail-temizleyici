using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class SubscriptionTests
{
    static MessageRow Row(string id, string sender, long date, bool unsubscribe = true) =>
        new(id, "t" + id, sender, "N", "x.com", "s", date, 2000, "INBOX", false, false, false, "NONE", unsubscribe, false);

    [Theory]
    [InlineData("<https://x.com/u?id=1>", "https://x.com/u?id=1")]
    [InlineData("<mailto:u@x.com>, <https://x.com/u>", "https://x.com/u")]
    [InlineData("<mailto:u@x.com?subject=unsub>", "mailto:u@x.com?subject=unsub")]
    [InlineData("<javascript:alert(1)>, <ftp://x.com/f>", null)]
    [InlineData("not a link", null)]
    [InlineData("", null)]
    public void Best_PrefersHttpsThenMailtoAndRejectsOtherSchemes(string header, string? expected)
    {
        Assert.Equal(expected, UnsubscribeLinks.Best(header));
    }

    [Theory]
    [InlineData("https://x.com/u", true)]
    [InlineData("mailto:a@b.c", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///c:/x.exe", false)]
    public void IsOpenable_AllowsOnlyWebAndMailto(string link, bool expected)
    {
        Assert.Equal(expected, UnsubscribeLinks.IsOpenable(link));
    }

    [Fact]
    public void FromQuery_JoinsDistinctAddresses()
    {
        Assert.Equal("from:(a@x.com OR b@x.com)", GmailLinks.FromQuery(["a@x.com", "b@x.com", "a@x.com"]));
    }

    [Fact]
    public void LatestUnsubscribeMessages_PicksNewestPerSenderOnlyWithHeader()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1", "a@x.com", 100), Row("2", "a@x.com", 300), Row("3", "b@x.com", 200),
                   Row("4", "b@x.com", 900, unsubscribe: false), Row("5", "c@x.com", 50, unsubscribe: false)]);

        var latest = db.LatestUnsubscribeMessages().OrderBy(x => x.Sender).ToList();

        Assert.Equal([("a@x.com", "2"), ("b@x.com", "3")], latest);
    }

    [Fact]
    public async Task RunAsync_SavesBestLinkPerSenderAndSkipsUnparsable()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Row("1", "a@x.com", 100), Row("2", "b@x.com", 200)]);
        IReadOnlyDictionary<string, string> Headers(IReadOnlyList<string> ids) =>
            ids.ToDictionary(id => id, id => id == "1" ? "<mailto:a@x.com>, <https://x.com/u>" : "garbage");

        var saved = await SubscriptionSync.RunAsync(db, (ids, _) => Task.FromResult(Headers(ids)), TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(1, saved);
        Assert.Equal(new Dictionary<string, string> { ["a@x.com"] = "https://x.com/u" }, db.GetUnsubscribeLinks());
    }
}
