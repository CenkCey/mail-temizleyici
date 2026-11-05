using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    [ObservableProperty] string status = "Envanter henüz başlatılmadı. Yarım kalmışsa kaldığı yerden devam eder.";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressText))] int fetched;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressText))] int total;
    [ObservableProperty] string speedText = "";

    Stopwatch? clock;
    int baseline;
    int pageBase;
    int inPage;
    double perMinute;

    public void Load()
    {
        if (StartCommand.IsRunning || RefreshCommand.IsRunning) return;
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            var count = db.Count();
            if (count == 0) return;
            Fetched = count;
            if (db.LoadState()?.FinishedAt == null)
            {
                Status = "Envanter yarım kalmış. 'Başlat / Devam et' kaldığı yerden devam eder.";
                return;
            }
            Total = count;
            Status = Summary(count, db.CountTrashedByApp()) + " Yeni gelen mailler için 'Yeni mailleri al'ı kullanın.";
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
        }
    }

    static string Summary(int count, int trashed) =>
        $"Envanterde {count.ToString("N0", Text.Tr)} mail kayıtlı" +
        (trashed > 0
            ? $"; {trashed.ToString("N0", Text.Tr)} tanesi uygulamayla çöp kutusuna taşındı ve Gmail'in toplam sayısına dahil değil."
            : ".");

    void Show(int count)
    {
        Fetched = count;
        if (clock == null)
        {
            clock = Stopwatch.StartNew();
            baseline = count;
            return;
        }
        if (clock.Elapsed.TotalSeconds < 15) return;
        perMinute = (count - baseline) / clock.Elapsed.TotalMinutes;
        if (perMinute <= 0) return;
        var left = Math.Max(0, Total - count) / perMinute;
        SpeedText = $"{perMinute.ToString("N0", Text.Tr)} mail/dk, yaklaşık {Eta(left)} kaldı";
    }

    static string Eta(double minutes) => minutes switch
    {
        < 1 => "1 dakikadan az",
        < 60 => $"{Math.Round(minutes):N0} dk",
        _ => $"{(int)(minutes / 60)} sa {Math.Round(minutes % 60):N0} dk"
    };

    public string ProgressText => string.Format(Text.Tr, "{0:N0} / {1:N0}", Fetched, Total);

    [RelayCommand]
    void CancelAll()
    {
        StartCancelCommand.Execute(null);
        RefreshCancelCommand.Execute(null);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task RefreshAsync(CancellationToken ct)
    {
        Status = "Gmail'e bağlanılıyor...";
        try
        {
            using var service = await GmailConnection.CreateServiceAsync(ct);
            var profile = await GmailConnection.GetProfileAsync(service, ct);
            using var db = new MailDatabase(AppPaths.Database);
            db.EnsureAccount(profile.Email);
            if (db.LoadState()?.FinishedAt == null)
            {
                Status = "Önce ilk envanteri tamamlayın (Başlat / Devam et).";
                return;
            }
            var before = db.Count();
            Total = Fetched = before;
            Status = "Yeni mailler aranıyor...";
            var added = await Task.Run(() => GmailInventory.RefreshAsync(service, db, new Progress<int>(n => Fetched = before + n), ct), ct);
            Status = "Ekli mailler işaretleniyor...";
            var withAttachment = await Task.Run(() => GmailInventory.SyncAttachmentsAsync(service, db, ct), ct);
            Total = Fetched = db.Count();
            Status = (added == 0 ? "Yeni mail yok. " : $"{added.ToString("N0", Text.Tr)} yeni mail alındı. ") +
                     Summary(Total, db.CountTrashedByApp()) +
                     $" Gmail'de {withAttachment.ToString("N0", Text.Tr)} mailde ek görünüyor; analizi yeniden çalıştırın.";
        }
        catch (OperationCanceledException)
        {
            Status = "İptal edildi. Yeniden başlattığınızda yeni mailler yeniden aranır.";
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
            Status = $"Hata: Yenileme yarıda kaldı, yeniden deneyebilirsiniz. {ex.Message}";
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task StartAsync(CancellationToken ct)
    {
        Status = "Gmail'e bağlanılıyor...";
        try
        {
            using var service = await GmailConnection.CreateServiceAsync(ct);
            var profile = await GmailConnection.GetProfileAsync(service, ct);
            Total = profile.MessagesTotal;
            using var db = new MailDatabase(AppPaths.Database);
            db.EnsureAccount(profile.Email);
            clock = null;
            SpeedText = "";
            pageBase = db.LoadState()?.FetchedCount ?? 0;
            inPage = 0;
            var runner = GmailInventory.CreateRunner(service, db, new Progress<int>(d =>
            {
                inPage += d;
                Show(pageBase + inPage);
            }));
            Status = "Mail başlıkları alınıyor. Pencereyi kapatmadan bekleyin.";
            await Task.Run(() => runner.RunAsync(new Progress<int>(n =>
            {
                pageBase = n;
                inPage = 0;
                Show(n);
            }), ct), ct);
            Total = Fetched;
            SpeedText = perMinute > 0 ? $"Ortalama hız: {perMinute.ToString("N0", Text.Tr)} mail/dk" : "";
            Status = "Ekli mailler işaretleniyor...";
            var withAttachment = await Task.Run(() => GmailInventory.SyncAttachmentsAsync(service, db, ct), ct);
            Status = $"Tamamlandı: {Fetched.ToString("N0", Text.Tr)} mail envantere alındı, {withAttachment.ToString("N0", Text.Tr)} mailde ek var. Analizi yeniden çalıştırın.";
        }
        catch (OperationCanceledException)
        {
            Status = "İptal edildi. Yeniden başlattığınızda kaldığı yerden devam eder.";
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
            Status = $"Hata: Envanter yarıda kaldı, yeniden başlattığınızda kaldığı yerden devam eder. {ex.Message}";
        }
    }
}
