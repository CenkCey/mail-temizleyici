using System.Net;
using Google;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Requests;
using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class InventoryTests
{
    static MessageRow Row(string id) =>
        new(id, "t", "a@x.com", "A", "x.com", "s", 1, 1, "", true, false, false, "NONE", false, false);

    static GoogleApiException RateLimit(HttpStatusCode code, string? reason = null) =>
        new("Gmail", "limit")
        {
            HttpStatusCode = code,
            Error = new RequestError { Code = (int)code, Errors = reason == null ? [] : [new SingleError { Reason = reason }] }
        };

    static InventoryRunner Runner(MailDatabase db, string[][] pages, Action<int>? onList = null,
        Func<IReadOnlyList<string>, Task<IReadOnlyList<MessageRow>>>? metadata = null)
    {
        return new InventoryRunner(
            db,
            (token, _) =>
            {
                var i = token == null ? 0 : int.Parse(token);
                onList?.Invoke(i);
                return Task.FromResult(new IdPage(pages[i], i + 1 < pages.Length ? (i + 1).ToString() : null));
            },
            (ids, _) => metadata != null
                ? metadata(ids)
                : Task.FromResult<IReadOnlyList<MessageRow>>(ids.Select(Row).ToList()),
            TimeSpan.Zero);
    }

    [Fact]
    public async Task Run_FetchesAllPagesAndMarksFinished()
    {
        using var db = new MailDatabase(":memory:");
        await Runner(db, [["1", "2"], ["3"]]).RunAsync(null, CancellationToken.None);
        Assert.Equal(3, db.Count());
        Assert.NotNull(db.LoadState()!.FinishedAt);
        Assert.Equal(3, db.LoadState()!.FetchedCount);
    }

    [Fact]
    public async Task Run_AfterCancel_ResumesFromSavedPage()
    {
        using var db = new MailDatabase(":memory:");
        using var cts = new CancellationTokenSource();
        var pages = new[] { new[] { "1" }, new[] { "2" }, new[] { "3" } };
        var runner = Runner(db, pages, metadata: ids =>
        {
            if (ids[0] == "1") cts.Cancel();
            return Task.FromResult<IReadOnlyList<MessageRow>>(ids.Select(Row).ToList());
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(null, cts.Token));
        Assert.Equal(1, db.Count());
        Assert.Equal("1", db.LoadState()!.PageToken);

        var listed = new List<int>();
        await Runner(db, pages, listed.Add).RunAsync(null, CancellationToken.None);
        Assert.Equal([1, 2], listed);
        Assert.Equal(3, db.Count());
    }

    [Fact]
    public async Task Run_WhenAlreadyFinished_DoesNothing()
    {
        using var db = new MailDatabase(":memory:");
        db.SaveState(new InventoryState(null, 5, "s", "f"));
        var listed = new List<int>();
        await Runner(db, [["1"]], listed.Add).RunAsync(null, CancellationToken.None);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task Run_RetriesOnRateLimitThenSucceeds()
    {
        using var db = new MailDatabase(":memory:");
        var calls = 0;
        await Runner(db, [["1"]], metadata: ids =>
        {
            if (++calls < 3) throw RateLimit(HttpStatusCode.TooManyRequests);
            return Task.FromResult<IReadOnlyList<MessageRow>>(ids.Select(Row).ToList());
        }).RunAsync(null, CancellationToken.None);
        Assert.Equal(3, calls);
        Assert.Equal(1, db.Count());
    }

    [Fact]
    public async Task Run_GivesUpAfterMaxRetries()
    {
        using var db = new MailDatabase(":memory:");
        var calls = 0;
        var runner = new InventoryRunner(
            db,
            (_, _) => Task.FromResult(new IdPage(["1"], null)),
            (_, _) => { calls++; throw RateLimit(HttpStatusCode.Forbidden, "rateLimitExceeded"); },
            TimeSpan.Zero, maxRetries: 2);
        await Assert.ThrowsAsync<GoogleApiException>(() => runner.RunAsync(null, CancellationToken.None));
        Assert.Equal(3, calls);
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public async Task Run_DoesNotRetryOtherErrors()
    {
        using var db = new MailDatabase(":memory:");
        var calls = 0;
        var runner = Runner(db, [["1"]], metadata: _ =>
        {
            calls++;
            throw RateLimit(HttpStatusCode.Forbidden, "insufficientPermissions");
        });
        await Assert.ThrowsAsync<GoogleApiException>(() => runner.RunAsync(null, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ToRow_MapsMetadata()
    {
        var m = new Message
        {
            Id = "1", ThreadId = "t", InternalDate = 5, SizeEstimate = 9,
            LabelIds = ["INBOX", "STARRED", "CATEGORY_PROMOTIONS"],
            Payload = new MessagePart
            {
                Headers =
                [
                    new MessagePartHeader { Name = "from", Value = "Shop <News@Shop.COM>" },
                    new MessagePartHeader { Name = "Subject", Value = "Sale" },
                    new MessagePartHeader { Name = "List-Unsubscribe", Value = "<mailto:u@shop.com>" }
                ]
            }
        };
        var row = GmailInventory.ToRow(m);
        Assert.Equal("news@shop.com", row.SenderAddress);
        Assert.Equal("Shop", row.SenderName);
        Assert.Equal("shop.com", row.Domain);
        Assert.Equal("PROMOTIONS", row.Category);
        Assert.True(row.IsRead);
        Assert.True(row.IsStarred);
        Assert.True(row.HasUnsubscribe);
        Assert.Equal(9, row.Size);
    }

    [Fact]
    public void ParseSender_UnparsableFrom_FallsBackToRaw()
    {
        Assert.Equal(("weird", ""), GmailInventory.ParseSender("Weird"));
    }

    [Fact]
    public void IsRateLimit_OnlyQuotaErrors()
    {
        Assert.True(Backoff.IsRateLimit(429, null));
        Assert.True(Backoff.IsRateLimit(403, [new SingleError { Reason = "userRateLimitExceeded" }]));
        Assert.False(Backoff.IsRateLimit(403, [new SingleError { Reason = "forbidden" }]));
        Assert.False(Backoff.IsRateLimit(500, null));
    }

    [Fact]
    public async Task Run_WhenAlreadyFinished_ReportsStoredCount()
    {
        using var db = new MailDatabase(":memory:");
        db.SaveState(new InventoryState(null, 5, "s", "f"));
        var reported = new List<int>();
        await Runner(db, [["1"]]).RunAsync(new Progress<int>(reported.Add), CancellationToken.None);
        await Task.Delay(50);
        Assert.Equal([5], reported);
    }
}
