using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public static class Text
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    static readonly string[] Rules =
    [
        "", "Yıldızlı mail var", "Yanıtladığınız kişi", "Kendi etiketiniz var", "Güvenilir alan adı", "Önemli konu",
        "Ek dosya var", "Tanıtım / sosyal", "Abonelik, okunmuyor", "Eski bildirim", "Eski güncelleme", "Kural yok"
    ];

    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("N1", Tr) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("N1", Tr) + " MB",
        _ => (bytes / 1024.0).ToString("N0", Tr) + " KB"
    };

    public static string Date(long unixMs) =>
        unixMs <= 0 ? "-" : DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().ToString("d MMM yyyy", Tr);

    public static string Label(Decision d) => d switch
    {
        Decision.Keep => "Koru",
        Decision.Trash => "Sil",
        _ => "Kararsız"
    };

    public static string MissingClient => $"Hata: {AppPaths.ClientSecret} bulunamadı. docs/KURULUM.md 2. adımı tamamlayın.";

    public static string AccountMismatch(AccountMismatchException e) => e.Stored is null
        ? "Hata: Veritabanında mail kayıtları var ama hangi Gmail hesabına ait olduğu bilinmiyor. Hesaplar karışmasın diye işlem durduruldu. " +
          @"Devam etmek için %APPDATA%\MailTemizleyici\veri\mail.db dosyasını silip envanteri yeniden başlatın."
        : $"Hata: Veritabanı {e.Stored} hesabına ait, şu an {e.Current} ile bağlandınız. Hesaplar karışmasın diye işlem durduruldu. " +
          @"Başka hesaba geçmek için %APPDATA%\MailTemizleyici\veri\mail.db dosyasını silip envanteri yeniden başlatın.";

    public static string Rule(int rule) => rule is >= 1 and <= 11 ? Rules[rule] : "";
}

public partial class SenderRow(SenderSummary summary, int starred = 0) : ObservableObject
{
    public SenderSummary Summary { get; } = summary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Effective), nameof(DecisionLabel), nameof(Source))]
    Decision? userDecision = summary.UserDecision;

    public string Address => Summary.SenderAddress;
    public int StarredCount => starred;
    public string StarredText => starred > 0 ? starred.ToString("N0", Text.Tr) : "";
    public int Count => Summary.Count;
    public long TotalSize => Summary.TotalSize;
    public string SizeText => Text.Size(Summary.TotalSize);
    public long LastDate => Summary.LastDate;
    public string LastDateText => Text.Date(Summary.LastDate);
    public int ReadPercent => (int)Math.Round(Summary.ReadRatio * 100);
    public string RuleText => Text.Rule(Summary.Rule);
    public Decision Effective => UserDecision ?? Summary.Suggested;
    public string DecisionLabel => Text.Label(Effective);
    public string Source => UserDecision == null ? "Öneri" : "Sizin kararınız";
}
