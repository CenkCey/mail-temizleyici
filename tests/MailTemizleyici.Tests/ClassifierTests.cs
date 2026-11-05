using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class ClassifierTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
    static readonly long NowMs = Now.ToUnixTimeMilliseconds();
    static readonly long OldMs = Now.AddYears(-3).ToUnixTimeMilliseconds();

    static SenderStats Stats(Action<SenderStats>? tweak = null)
    {
        var s = new SenderStats { Address = "a@shop.com", Domain = "shop.com", Count = 10, ReadCount = 5, LastDate = NowMs, FirstDate = OldMs };
        tweak?.Invoke(s);
        return s;
    }

    static (Decision, int) Run(SenderStats s) => Classifier.Classify(s, NowMs, Classifier.DefaultAllowlist);

    [Fact]
    public void SaveAllowlist_NormalizesDedupesAndDropsInvalidLines()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "", "allowlist.txt");
        try
        {
            Classifier.SaveAllowlist(path, [" Bank.com ", "@bank.com", "", "two words", "gov.tr"]);
            Assert.Equal(["bank.com", "gov.tr"], Classifier.LoadAllowlist(path));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void DefaultAllowlist_KeepsGoogleAccountMail()
    {
        var s = new SenderStats { Address = "no-reply@accounts.google.com", Domain = "accounts.google.com", Count = 3, PromoSocialCount = 3 };
        Assert.Equal((Decision.Keep, 4), Run(s));
    }

    [Fact]
    public void NoRuleMatches_IsUndecided() => Assert.Equal((Decision.Undecided, 11), Run(Stats()));

    [Fact]
    public void Starred_BeatsPromotions()
    {
        Assert.Equal((Decision.Keep, 1), Run(Stats(s => { s.AnyStarred = true; s.PromoSocialCount = s.Count; })));
    }

    [Fact]
    public void Replied_IsKeep() => Assert.Equal((Decision.Keep, 2), Run(Stats(s => s.Replied = true)));

    [Fact]
    public void UserLabel_IsKeep() => Assert.Equal((Decision.Keep, 3), Run(Stats(s => s.AnyUserLabel = true)));

    [Theory]
    [InlineData("turkiye.gov.tr")]
    [InlineData("mail.gib.gov.tr")]
    public void AllowlistedDomain_IsKeep(string domain)
    {
        var s = new SenderStats { Address = "x@" + domain, Domain = domain, Count = 1, PromoSocialCount = 1 };
        Assert.Equal((Decision.Keep, 4), Run(s));
    }

    [Fact]
    public void KeywordSubject_BeatsPromotions()
    {
        Assert.Equal((Decision.Keep, 5), Run(Stats(s => { s.AnyKeyword = true; s.PromoSocialCount = s.Count; })));
    }

    [Fact]
    public void Attachment_IsKeep() => Assert.Equal((Decision.Keep, 6), Run(Stats(s => s.AnyAttachment = true)));

    [Fact]
    public void AllPromotions_IsTrash() => Assert.Equal((Decision.Trash, 7), Run(Stats(s => s.PromoSocialCount = s.Count)));

    [Fact]
    public void SomePromotions_IsNotTrash() =>
        Assert.Equal((Decision.Undecided, 11), Run(Stats(s => s.PromoSocialCount = s.Count - 1)));

    [Fact]
    public void UnsubscribeAndLowReadRatio_IsTrash()
    {
        Assert.Equal((Decision.Trash, 8), Run(Stats(s => { s.AnyUnsubscribe = true; s.ReadCount = 0; })));
        Assert.Equal((Decision.Undecided, 11), Run(Stats(s => { s.AnyUnsubscribe = true; s.ReadCount = 5; })));
    }

    [Fact]
    public void NoReplyOld_IsTrash_ButRecentIsNot()
    {
        Assert.Equal((Decision.Trash, 9), Run(Stats(s => { s.Address = "noreply@x.com"; s.LastDate = OldMs; })));
        Assert.Equal((Decision.Undecided, 11), Run(Stats(s => s.Address = "noreply@x.com")));
    }

    [Fact]
    public void UpdatesOlderThanTwoYears_IsTrash()
    {
        Assert.Equal((Decision.Trash, 10), Run(Stats(s => { s.UpdatesCount = s.Count; s.LastDate = OldMs; })));
        Assert.Equal((Decision.Undecided, 11), Run(Stats(s => s.UpdatesCount = s.Count)));
    }
}
