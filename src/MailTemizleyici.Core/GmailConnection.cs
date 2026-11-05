using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace MailTemizleyici.Core;

public sealed record GmailProfile(string Email, int MessagesTotal);

public static class GmailConnection
{
    public static readonly string[] Scopes = [GmailService.Scope.GmailReadonly];

    public static readonly string[] ModifyScopes = [GmailService.Scope.GmailModify];

    public static async Task<GmailService> CreateServiceAsync(CancellationToken ct, bool modify = false)
    {
        if (!File.Exists(AppPaths.ClientSecret))
            throw new FileNotFoundException("OAuth client file not found.", AppPaths.ClientSecret);

        await using var stream = File.OpenRead(AppPaths.ClientSecret);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets, modify ? ModifyScopes : Scopes, modify ? "user-modify" : "user", ct, new FileDataStore(AppPaths.Dir, true));

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Mail Temizleyici"
        });
    }

    public static async Task SignOutAsync()
    {
        var store = new FileDataStore(AppPaths.Dir, true);
        await store.DeleteAsync<TokenResponse>("user");
        await store.DeleteAsync<TokenResponse>("user-modify");
    }

    public static async Task<GmailProfile> ConnectAsync(CancellationToken ct)
    {
        using var service = await CreateServiceAsync(ct);
        return await GetProfileAsync(service, ct);
    }

    public static async Task<GmailProfile> GetProfileAsync(GmailService service, CancellationToken ct)
    {
        var profile = await service.Users.GetProfile("me").ExecuteAsync(ct);
        return new GmailProfile(profile.EmailAddress, profile.MessagesTotal ?? 0);
    }
}
