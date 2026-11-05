using System.Net;
using System.Net.Mail;
using Google;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Requests;
using Google.Apis.Util;

namespace MailTemizleyici.Core;

public static class GmailInventory
{
    const int BatchSize = 20;
    const int UnitsPerGet = 20;
    const double UnitsPerMinute = 5400;
    const int MaxFailedRounds = 6;
    static readonly string[] Headers = ["From", "To", "Subject", "Date", "List-Unsubscribe"];

    static Throttle NewThrottle() => new(UnitsPerMinute, BatchSize);

    public static InventoryRunner CreateRunner(GmailService service, MailDatabase db, IProgress<int>? chunkProgress = null)
    {
        var throttle = NewThrottle();
        return new InventoryRunner(db,
            (token, ct) => ListAsync(service, token, ct),
            (ids, ct) => GetMetadataAsync(service, db, throttle, ids, chunkProgress, ct),
            TimeSpan.FromSeconds(5));
    }

    public static Task<int> RefreshAsync(GmailService service, MailDatabase db, IProgress<int>? progress, CancellationToken ct)
    {
        var throttle = NewThrottle();
        return InventoryRefresh.RunAsync(db, (token, c) => ListAsync(service, token, c),
            (ids, c) => GetMetadataAsync(service, db, throttle, ids, null, c), TimeSpan.FromSeconds(5), progress, ct);
    }

    public static Task<int> SyncAttachmentsAsync(GmailService service, MailDatabase db, CancellationToken ct) =>
        AttachmentSync.RunAsync(db, (token, c) => ListAsync(service, token, c, "has:attachment"), TimeSpan.FromSeconds(5), ct);

    public static Task<int> SyncSubscriptionsAsync(GmailService service, MailDatabase db, CancellationToken ct)
    {
        var throttle = NewThrottle();
        return SubscriptionSync.RunAsync(db, async (ids, c) =>
        {
            var messages = await ThrottledFetch.RunAsync(ids, throttle, UnitsPerGet,
                (chunk, t) => FetchChunkAsync(service, chunk, ["List-Unsubscribe"], t), null, MaxFailedRounds, c);
            return (IReadOnlyDictionary<string, string>)messages.ToDictionary(m => m.Id, m => HeaderOf(m, "List-Unsubscribe"));
        }, TimeSpan.FromSeconds(5), ct);
    }

    static async Task<IdPage> ListAsync(GmailService service, string? token, CancellationToken ct, string? query = null)
    {
        var request = service.Users.Messages.List("me");
        request.MaxResults = 500;
        request.Q = query;
        request.PageToken = token;
        var response = await request.ExecuteAsync(ct);
        return new IdPage(response.Messages?.Select(m => m.Id).ToList() ?? [], response.NextPageToken);
    }

    static string HeaderOf(Message m, string name) =>
        m.Payload?.Headers?.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    static async Task<IReadOnlyList<MessageRow>> GetMetadataAsync(
        GmailService service, MailDatabase db, Throttle throttle, IReadOnlyList<string> ids,
        IProgress<int>? chunkProgress, CancellationToken ct)
    {
        var known = db.KnownIds(ids);
        var fresh = ids.Where(id => !known.Contains(id)).ToList();
        var rows = new List<MessageRow>(fresh.Count);
        await ThrottledFetch.RunAsync(fresh, throttle, UnitsPerGet,
            (chunk, t) => FetchChunkAsync(service, chunk, Headers, t),
            messages =>
            {
                var batch = messages.Select(ToRow).ToList();
                db.Upsert(batch);
                rows.AddRange(batch);
                chunkProgress?.Report(batch.Count);
            }, MaxFailedRounds, ct);
        return rows;
    }

    static async Task<ChunkResult<Message>> FetchChunkAsync(
        GmailService service, IReadOnlyList<string> ids, string[] headers, CancellationToken ct)
    {
        var batch = new BatchRequest(service);
        var messages = new List<Message>(ids.Count);
        var failed = new List<string>();
        RequestError? error = null;
        foreach (var id in ids)
        {
            var get = service.Users.Messages.Get("me", id);
            get.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
            get.MetadataHeaders = new Repeatable<string>(headers);
            batch.Queue<Message>(get, (message, requestError, _, _) =>
            {
                if (message != null) messages.Add(message);
                else if (requestError is { Code: not 404 })
                {
                    failed.Add(id);
                    error = requestError;
                }
            });
        }
        try
        {
            await batch.ExecuteAsync(ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden or >= HttpStatusCode.InternalServerError)
        {
            return new ChunkResult<Message>([], ids, ex.HttpStatusCode != HttpStatusCode.InternalServerError, ex.Message);
        }
        var rateLimited = error is { Code: 429 or 403 };
        return new ChunkResult<Message>(messages, failed, rateLimited, error?.Message);
    }

    public static MessageRow ToRow(Message m)
    {
        string Header(string name) => HeaderOf(m, name);

        var (address, name) = ParseSender(Header("From"));
        var labels = m.LabelIds ?? [];
        var category = labels.FirstOrDefault(l => l.StartsWith("CATEGORY_"))?["CATEGORY_".Length..] ?? "NONE";
        return new MessageRow(
            m.Id, m.ThreadId, address, name, address.Contains('@') ? address[(address.LastIndexOf('@') + 1)..] : "",
            Header("Subject"), m.InternalDate ?? 0, m.SizeEstimate ?? 0, string.Join(",", labels),
            !labels.Contains("UNREAD"), labels.Contains("STARRED"), labels.Contains("IMPORTANT"),
            category, Header("List-Unsubscribe") != "", false,
            labels.Contains("SENT") ? ParseRecipients(Header("To")) : "");
    }

    public static string ParseRecipients(string to)
    {
        if (string.IsNullOrWhiteSpace(to)) return "";
        try
        {
            var addresses = new MailAddressCollection();
            addresses.Add(to);
            return string.Join(",", addresses.Select(a => a.Address.ToLowerInvariant()));
        }
        catch (FormatException)
        {
            return "";
        }
    }

    public static (string Address, string Name) ParseSender(string from)
    {
        try
        {
            var a = new MailAddress(from);
            return (a.Address.ToLowerInvariant(), a.DisplayName);
        }
        catch (FormatException)
        {
            return (from.Trim().ToLowerInvariant(), "");
        }
    }
}
