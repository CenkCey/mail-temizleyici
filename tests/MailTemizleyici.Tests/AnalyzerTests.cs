using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class AnalyzerTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

    static MessageRow Msg(string id, string sender, string labels = "INBOX", string category = "NONE",
        string subject = "hi", bool read = true, bool starred = false, bool unsub = false, long size = 100, string rcpt = "") =>
        new(id, "t", sender, "", sender[(sender.IndexOf('@') + 1)..], subject, Now.AddDays(-10).ToUnixTimeMilliseconds(),
            size, labels, read, starred, false, category, unsub, false, rcpt);

    static Dictionary<string, SenderSummary> Analyze(MailDatabase db, params MessageRow[] rows)
    {
        db.Upsert(rows);
        Analyzer.Run(db, Now, Classifier.DefaultAllowlist);
        return db.GetSenders().ToDictionary(s => s.SenderAddress);
    }

    [Fact]
    public void Aggregates_CountSizeAndReadRatio()
    {
        using var db = new MailDatabase(":memory:");
        var s = Analyze(db,
            Msg("1", "a@x.com", size: 100, read: true),
            Msg("2", "a@x.com", size: 300, read: false))["a@x.com"];
        Assert.Equal(2, s.Count);
        Assert.Equal(400, s.TotalSize);
        Assert.Equal(0.5, s.ReadRatio);
    }

    [Fact]
    public void SentMail_BecomesContact_AndMarksSenderReplied_NotASender()
    {
        using var db = new MailDatabase(":memory:");
        var senders = Analyze(db,
            Msg("1", "me@example.com", labels: "SENT", rcpt: "friend@x.com,other@y.com"),
            Msg("2", "friend@x.com", category: "PROMOTIONS"));
        Assert.DoesNotContain("me@example.com", senders.Keys);
        Assert.Equal(["friend@x.com", "other@y.com"], db.GetSentContacts());
        Assert.True(senders["friend@x.com"].UserReplied);
        Assert.Equal((Decision.Keep, 2), (senders["friend@x.com"].Suggested, senders["friend@x.com"].Rule));
    }

    [Fact]
    public void KeywordInAnyMessage_KeepsWholeSender()
    {
        using var db = new MailDatabase(":memory:");
        var s = Analyze(db,
            Msg("1", "shop@x.com", category: "PROMOTIONS"),
            Msg("2", "shop@x.com", category: "PROMOTIONS", subject: "Siparişiniz hazır"))["shop@x.com"];
        Assert.Equal((Decision.Keep, 5), (s.Suggested, s.Rule));
    }

    [Fact]
    public void UserLabelAndStar_Keep()
    {
        using var db = new MailDatabase(":memory:");
        var s = Analyze(db,
            Msg("1", "a@x.com", labels: "INBOX,Label_12"),
            Msg("2", "b@x.com", starred: true));
        Assert.Equal(3, s["a@x.com"].Rule);
        Assert.Equal(1, s["b@x.com"].Rule);
    }

    [Fact]
    public void Reanalysis_PreservesUserDecision()
    {
        using var db = new MailDatabase(":memory:");
        Analyze(db, Msg("1", "a@x.com", category: "PROMOTIONS"));
        db.SetUserDecision(["a@x.com"], Decision.Keep);
        var s = Analyze(db)["a@x.com"];
        Assert.Equal(Decision.Trash, s.Suggested);
        Assert.Equal(Decision.Keep, s.UserDecision);
        Assert.Equal(Decision.Keep, s.Effective);
    }

    [Fact]
    public void SetUserDecision_Null_ClearsOverride()
    {
        using var db = new MailDatabase(":memory:");
        Analyze(db, Msg("1", "a@x.com", category: "PROMOTIONS"));
        db.SetUserDecision(["a@x.com"], Decision.Keep);
        db.SetUserDecision(["a@x.com"], null);
        Assert.Equal(Decision.Trash, db.GetSenders().Single().Effective);
    }

    [Fact]
    public void Allowlist_LoadsFromFile_OrFallsBackToDefault()
    {
        Assert.Equal(Classifier.DefaultAllowlist, Classifier.LoadAllowlist(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt")));
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Bank.com\n\n  ");
            Assert.Equal(["bank.com"], Classifier.LoadAllowlist(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseRecipients_HandlesListsAndGarbage()
    {
        Assert.Equal("a@x.com,b@y.com", GmailInventory.ParseRecipients("A <A@x.com>, b@y.com"));
        Assert.Equal("", GmailInventory.ParseRecipients(""));
        Assert.Equal("", GmailInventory.ParseRecipients("not an address <"));
    }

    [Theory]
    [InlineData("ŞİFRE SIFIRLAMA")]
    [InlineData("SİPARİŞİNİZ HAZIR")]
    [InlineData("INVOICE 123")]
    [InlineData("Fatura Bilgisi")]
    public void Keywords_MatchCapitalTurkishAndEnglish(string subject) => Assert.True(Classifier.HasKeyword(subject));

    [Fact]
    public void Keywords_IgnoreOrdinarySubjects() => Assert.False(Classifier.HasKeyword("Haftalık bülten"));

    [Fact]
    public void Drafts_AreNotSenderGroups()
    {
        using var db = new MailDatabase(":memory:");
        var senders = Analyze(db, Msg("1", "me@example.com", labels: "DRAFT"), Msg("2", "a@x.com"));
        Assert.DoesNotContain("me@example.com", senders.Keys);
    }
}
