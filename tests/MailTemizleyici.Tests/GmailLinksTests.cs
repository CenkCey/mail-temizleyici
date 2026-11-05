using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class GmailLinksTests
{
    [Fact]
    public void Thread_WithAccount_PinsTheAccount()
    {
        Assert.Equal("https://mail.google.com/mail/?authuser=me%40example.com#all/18c3f", GmailLinks.Thread("me@example.com", "18c3f"));
    }

    [Fact]
    public void Thread_WithoutAccount_UsesFirstSession()
    {
        Assert.Equal("https://mail.google.com/mail/u/0/#all/18c3f", GmailLinks.Thread(null, "18c3f"));
    }

    [Fact]
    public void Thread_EscapesOddIds()
    {
        Assert.EndsWith("#all/x%2F..%2Fy", GmailLinks.Thread("a@b.c", "x/../y"));
    }
}
