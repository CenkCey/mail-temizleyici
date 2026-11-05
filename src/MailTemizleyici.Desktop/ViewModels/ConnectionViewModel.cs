using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty] string status = "Henüz bağlanılmadı.";
    [ObservableProperty] string account = "-";
    [ObservableProperty] string totalMessages = "-";

    public event Action? AccountChanged;

    public ConnectionViewModel()
    {
        if (AppPaths.ActiveAccount is not { } saved) return;
        account = saved;
        status = $"Kayıtlı hesap: {saved}. Devam etmek için Bağlan'a basın.";
    }

    static string LocalSummary()
    {
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            var count = db.Count();
            if (count == 0) return "";
            var trashed = db.CountTrashedByApp();
            return $" Gmail'deki sayı çöp kutusunu içermez. Envanterde {count.ToString("N0", Text.Tr)} mail kayıtlı" +
                   (trashed > 0 ? $", {trashed.ToString("N0", Text.Tr)} tanesi uygulamayla çöp kutusuna taşınmış." : ".");
        }
        catch (Exception)
        {
            return "";
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task ConnectAsync(string? mode, CancellationToken ct)
    {
        var switching = mode == "switch";
        var previous = AppPaths.ActiveAccount;
        try
        {
            if (switching)
            {
                Status = "Oturum kapatılıyor...";
                await GmailConnection.SignOutAsync();
            }
            Status = "Tarayıcıda Google girişi bekleniyor...";
            var profile = await GmailConnection.ConnectAsync(ct);
            AppPaths.SetActiveAccount(profile.Email);
            Account = profile.Email;
            TotalMessages = profile.MessagesTotal.ToString("N0", Text.Tr);
            Status = "Bağlandı (yalnızca okuma izni)." + LocalSummary();
            if (!string.Equals(previous, AppPaths.ActiveAccount, StringComparison.Ordinal))
            {
                AccountChanged?.Invoke();
                Status += $" Ekranlar {profile.Email} hesabının kayıtlarıyla yenilendi.";
            }
        }
        catch (OperationCanceledException)
        {
            Status = switching
                ? "İptal edildi. Oturum kapatıldı; Bağlan'a basarak giriş yapın."
                : "İptal edildi. Yeniden bağlanabilirsiniz.";
        }
        catch (FileNotFoundException)
        {
            Status = Text.MissingClient;
        }
        catch (Exception ex)
        {
            Status = $"Hata: Bağlantı kurulamadı. {ex.Message}";
        }
    }
}
