using Google;
using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class TrashTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

    static MessageRow Msg(string id, string sender, string category = "PROMOTIONS", bool starred = false,
        string labels = "INBOX,UNREAD", long size = 10) =>
        new(id, "t", sender, "", "x.com", "hi", 1, size, labels, true, starred, false, category, false, false);

    static MailDatabase Seed(params MessageRow[] rows)
    {
        var db = new MailDatabase(":memory:");
        db.Upsert(rows);
        Analyzer.Run(db, Now, []);
        db.SaveState(new InventoryState(null, rows.Length, "s", "f"));
        return db;
    }

    [Fact]
    public void Plan_ContainsOnlyTrashSenders_AndSkipsStarredMessages()
    {
        using var db = Seed(
            Msg("1", "promo@x.com"), Msg("2", "promo@x.com"),
            Msg("3", "keep@x.com", category: "NONE"));
        db.SetUserDecision(["promo@x.com"], Decision.Trash);
        db.Upsert([Msg("4", "promo@x.com", starred: true)]);
        var plan = TrashPlan.Build(db);
        Assert.Equal(["1", "2"], plan.Ids.Order());
        Assert.Equal(1, plan.SkippedStarred);
        Assert.Equal(20, plan.TotalSize);
    }

    [Fact]
    public void Plan_IncludeStarred_AddsThemAndCountsThem()
    {
        using var db = Seed(Msg("1", "promo@x.com"), Msg("2", "promo@x.com", starred: true), Msg("3", "other@x.com"));
        db.SetUserDecision(["promo@x.com", "other@x.com"], Decision.Trash);

        var without = TrashPlan.Build(db);
        var with = TrashPlan.Build(db, includeStarred: true);

        Assert.Equal(["1", "3"], without.Ids.Order());
        Assert.Equal(1, without.SkippedStarred);
        Assert.Equal(0, without.IncludedStarred);
        Assert.Equal(["1", "2", "3"], with.Ids.Order());
        Assert.Equal(0, with.SkippedStarred);
        Assert.Equal(1, with.IncludedStarred);
    }

    [Fact]
    public async Task Undo_RestoresTheStarOfAStarredMailThatWasTrashed()
    {
        using var db = Seed(Msg("1", "promo@x.com", starred: true, labels: "INBOX,STARRED"));
        db.SetUserDecision(["promo@x.com"], Decision.Trash);
        var restored = new List<string>();
        await new TrashRunner(db, (_, _) => Task.CompletedTask, TimeSpan.Zero)
            .RunAsync(TrashPlan.Build(db, includeStarred: true), null, CancellationToken.None);

        await new UndoRunner(db, (_, add, _) =>
        {
            restored.AddRange(add);
            return Task.CompletedTask;
        }, TimeSpan.Zero).RunAsync(1, null, CancellationToken.None);

        Assert.Contains("STARRED", restored);
    }

    [Fact]
    public void Plan_UserKeepOverridesSuggestedTrash()
    {
        using var db = Seed(Msg("1", "promo@x.com"));
        db.SetUserDecision(["promo@x.com"], Decision.Keep);
        Assert.Equal(0, TrashPlan.Build(db).MessageCount);
    }

    [Fact]
    public void Plan_ExcludesAlreadyTrashedMessages_UntilUndone()
    {
        using var db = Seed(Msg("1", "promo@x.com"), Msg("2", "promo@x.com"));
        db.LogActions(["1"], "Trash", 1);
        Assert.Equal(["2"], TrashPlan.Build(db).Ids);
        db.MarkUndone(["1"], 1);
        Assert.Equal(2, TrashPlan.Build(db).MessageCount);
    }

    [Fact]
    public void Trial_TakesSmallestGroupsWithinLimit()
    {
        var plan = new TrashPlan(
        [
            new PlanGroup("big", Enumerable.Range(0, 90).Select(i => "b" + i).ToList(), 1),
            new PlanGroup("small", ["s1", "s2"], 1),
            new PlanGroup("mid", Enumerable.Range(0, 20).Select(i => "m" + i).ToList(), 1)
        ], 0);
        Assert.Equal(["small", "mid"], plan.Trial(100).Groups.Select(g => g.Sender));
        Assert.Equal(["big"], new TrashPlan([plan.Groups[0]], 0).Trial(10).Groups.Select(g => g.Sender));
    }

    [Fact]
    public async Task Runner_LogsEveryChunk_AndReturnsBatchNo()
    {
        using var db = Seed(Msg("1", "p@x.com"), Msg("2", "p@x.com"), Msg("3", "p@x.com"));
        var calls = new List<string[]>();
        var runner = new TrashRunner(db, (ids, _) => { calls.Add(ids.ToArray()); return Task.CompletedTask; }, TimeSpan.Zero, chunkSize: 2);
        var batch = await runner.RunAsync(TrashPlan.Build(db), null, CancellationToken.None);
        Assert.Equal(1, batch);
        Assert.Equal([2, 1], calls.Select(c => c.Length));
        var info = Assert.Single(db.GetBatches());
        Assert.Equal((3, 3), (info.Total, info.Active));
        Assert.Equal(0, TrashPlan.Build(db).MessageCount);
    }

    [Fact]
    public async Task Runner_WhenGmailAnswersWithError_RemovesThatChunkFromLog()
    {
        using var db = Seed(Msg("1", "p@x.com"), Msg("2", "p@x.com"), Msg("3", "p@x.com"));
        var n = 0;
        var runner = new TrashRunner(db, (_, _) => ++n == 2 ? throw new GoogleApiException("Gmail", "boom") : Task.CompletedTask,
            TimeSpan.Zero, chunkSize: 2);
        await Assert.ThrowsAsync<GoogleApiException>(() => runner.RunAsync(TrashPlan.Build(db), null, CancellationToken.None));
        Assert.Equal(2, Assert.Single(db.GetBatches()).Total);
        Assert.Equal(1, TrashPlan.Build(db).MessageCount);
    }

    [Fact]
    public async Task Runner_WhenOutcomeIsUnclear_KeepsLogSoUndoStillWorks()
    {
        using var db = Seed(Msg("1", "p@x.com"));
        var runner = new TrashRunner(db, (_, _) => throw new HttpRequestException("timeout"), TimeSpan.Zero);
        await Assert.ThrowsAsync<HttpRequestException>(() => runner.RunAsync(TrashPlan.Build(db), null, CancellationToken.None));
        Assert.Equal(1, Assert.Single(db.GetBatches()).Active);
    }

    [Fact]
    public void Plan_WhenInventoryUnfinished_Throws()
    {
        using var db = new MailDatabase(":memory:");
        db.Upsert([Msg("1", "p@x.com")]);
        Analyzer.Run(db, Now, []);
        Assert.Throws<InventoryUnfinishedException>(() => TrashPlan.Build(db));
        db.SaveState(new InventoryState("tok", 1, "s", null));
        Assert.Throws<InventoryUnfinishedException>(() => TrashPlan.Build(db));
    }

    [Fact]
    public async Task Runner_NeverTouchesStarredMail()
    {
        using var db = Seed(Msg("1", "p@x.com"));
        db.Upsert([Msg("2", "p@x.com", starred: true)]);
        var sent = new List<string>();
        var runner = new TrashRunner(db, (ids, _) => { sent.AddRange(ids); return Task.CompletedTask; }, TimeSpan.Zero);
        await runner.RunAsync(TrashPlan.Build(db), null, CancellationToken.None);
        Assert.Equal(["1"], sent);
    }

    [Fact]
    public async Task Undo_RestoresByLabelGroup_AndMarksUndone()
    {
        using var db = Seed(
            Msg("1", "p@x.com", labels: "INBOX,UNREAD,CATEGORY_PROMOTIONS"),
            Msg("2", "p@x.com", labels: "INBOX,UNREAD,CATEGORY_PROMOTIONS"),
            Msg("3", "p@x.com", labels: "CATEGORY_PROMOTIONS,Label_1,SENT"));
        var batch = await new TrashRunner(db, (_, _) => Task.CompletedTask, TimeSpan.Zero)
            .RunAsync(TrashPlan.Build(db), null, CancellationToken.None);

        var calls = new List<(string[] Ids, string[] Labels)>();
        var undo = new UndoRunner(db, (ids, labels, _) => { calls.Add((ids.ToArray(), labels.ToArray())); return Task.CompletedTask; }, TimeSpan.Zero);
        Assert.Equal(3, await undo.RunAsync(batch, null, CancellationToken.None));

        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, c => c.Ids.Order().SequenceEqual(["1", "2"]) && c.Labels.SequenceEqual(["CATEGORY_PROMOTIONS", "INBOX", "UNREAD"]));
        Assert.Contains(calls, c => c.Ids.SequenceEqual(["3"]) && c.Labels.SequenceEqual(["CATEGORY_PROMOTIONS", "Label_1"]));
        Assert.Equal(0, Assert.Single(db.GetBatches()).Active);
        Assert.Equal(0, await undo.RunAsync(batch, null, CancellationToken.None));
    }

    [Fact]
    public void ModifyScope_IsNeverFullMailAccess()
    {
        Assert.Equal(["https://www.googleapis.com/auth/gmail.modify"], GmailConnection.ModifyScopes);
    }
}
