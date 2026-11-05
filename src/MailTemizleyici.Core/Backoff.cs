using Google;
using Google.Apis.Requests;

namespace MailTemizleyici.Core;

public static class Backoff
{
    public static bool IsRateLimit(int code, IEnumerable<SingleError>? errors) =>
        code == 429 ||
        (code == 403 && errors?.Any(e => e.Reason is "rateLimitExceeded" or "userRateLimitExceeded") == true);

    static bool IsRateLimit(GoogleApiException ex) => IsRateLimit((int)ex.HttpStatusCode, ex.Error?.Errors);

    public static async Task<T> RetryAsync<T>(Func<Task<T>> action, TimeSpan baseDelay, int maxRetries, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action();
            }
            catch (GoogleApiException ex) when (IsRateLimit(ex) && attempt < maxRetries)
            {
                await Task.Delay(baseDelay * Math.Pow(2, attempt), ct);
            }
        }
    }
}
