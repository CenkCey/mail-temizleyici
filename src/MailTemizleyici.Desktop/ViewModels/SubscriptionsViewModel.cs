using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public sealed record SubscriptionRow(string Address, string Mails, string ReadPercent, string LastDate, string Decision, string Kind, string? Link);

public partial class SubscriptionsViewModel : ObservableObject
{
    [ObservableProperty] string status = "";
    [ObservableProperty] IReadOnlyList<SubscriptionRow> rows = [];

    public void Load()
    {
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            var links = db.GetUnsubscribeLinks();
            Rows = db.GetSenders().Where(s => s.HasUnsubscribe).OrderByDescending(s => s.Count)
                .Select(s =>
                {
                    var link = links.GetValueOrDefault(s.SenderAddress);
                    return new SubscriptionRow(
                        s.SenderAddress, s.Count.ToString("N0", Text.Tr), ((int)Math.Round(s.ReadRatio * 100)).ToString(Text.Tr),
                        Text.Date(s.LastDate), Text.Label(s.Effective),
                        link is null ? "Bağlantı yok" : link.StartsWith("mailto:") ? "E-posta" : "Web sayfası", link);
                }).ToList();
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
            return;
        }
        Status = Rows.Count == 0
            ? "Abonelik göndereni yok. Önce Envanter ve Analiz ekranlarını çalıştırın."
            : $"{Rows.Count.ToString("N0", Text.Tr)} abonelik göndereni. Bağlantısı eksik olanlar için 'Bağlantıları getir'e basın.";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task FetchLinksAsync(CancellationToken ct)
    {
        Status = "Gmail'e bağlanılıyor...";
        try
        {
            using var service = await GmailConnection.CreateServiceAsync(ct);
            var profile = await GmailConnection.GetProfileAsync(service, ct);
            using var db = new MailDatabase(AppPaths.Database);
            db.EnsureAccount(profile.Email);
            Status = "Abonelik bağlantıları alınıyor...";
            var saved = await Task.Run(() => GmailInventory.SyncSubscriptionsAsync(service, db, ct), ct);
            Load();
            Status = $"{saved.ToString("N0", Text.Tr)} göndericinin abonelik bağlantısı alındı. " + Status;
        }
        catch (OperationCanceledException)
        {
            Status = "İptal edildi. Yeniden başlattığınızda bağlantılar yeniden alınır.";
        }
        catch (AccountMismatchException ex)
        {
            Status = Text.AccountMismatch(ex);
        }
        catch (FileNotFoundException)
        {
            Status = Text.MissingClient;
        }
        catch (Exception ex)
        {
            Status = $"Hata: Bağlantılar alınamadı, yeniden deneyebilirsiniz. {ex.Message}";
        }
    }
}
