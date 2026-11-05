using MailTemizleyici.Core;

const string Account = "demo.user@example.com";

if (Environment.GetEnvironmentVariable(AppPaths.DataDirVariable) is not { Length: > 0 })
{
    Console.Error.WriteLine($"Set {AppPaths.DataDirVariable} to an empty scratch folder first. This tool never touches your real data folder.");
    return 1;
}

var now = DateTimeOffset.UtcNow;
var random = new Random(2026);
var rows = new List<MessageRow>();

AppPaths.SetActiveAccount(Account);
if (File.Exists(AppPaths.Database)) File.Delete(AppPaths.Database);
using var db = new MailDatabase(AppPaths.Database);

void Add(string address, string name, string category, int count, double readRatio, bool unsubscribe, int starred,
    int attachments, int yearsBack, int sizeKb, params string[] subjects)
{
    var domain = address[(address.IndexOf('@') + 1)..];
    for (var i = 0; i < count; i++)
    {
        var read = random.NextDouble() < readRatio;
        var isStarred = i < starred;
        var date = now.AddDays(-random.Next(3, 365 * yearsBack)).ToUnixTimeMilliseconds();
        var labels = new List<string> { "INBOX" };
        if (!read) labels.Add("UNREAD");
        if (category != "NONE") labels.Add("CATEGORY_" + category);
        if (isStarred) labels.Add("STARRED");
        var id = $"d{rows.Count + 1:x15}";
        rows.Add(new MessageRow(id, id, address, name, domain, subjects[random.Next(subjects.Length)], date,
            (long)(sizeKb * 1024 * (0.6 + random.NextDouble() * 0.8)), string.Join(",", labels), read, isStarred, false,
            category, unsubscribe, i < attachments));
    }
}

Add("deals@brightmart.example", "BrightMart", "PROMOTIONS", 120, 0.02, true, 0, 0, 6, 110, "Hafta sonuna özel %40 indirim", "Sepetinizde ürün unuttunuz", "Yeni sezon ürünleri geldi");
Add("offers@shoply.example", "Shoply", "PROMOTIONS", 80, 0.05, true, 0, 0, 5, 95, "Sadece bugün: ücretsiz kargo", "Size özel kupon kodunuz hazır");
Add("news@fashionhub.example", "FashionHub", "PROMOTIONS", 60, 0.03, true, 0, 0, 4, 140, "Yaz koleksiyonunu keşfedin", "Son 24 saat: %50'ye varan fırsatlar");
Add("promo@travelnest.example", "TravelNest", "PROMOTIONS", 45, 0.08, true, 0, 0, 5, 160, "Erken kayıt fırsatı", "Hafta sonu kaçamağı önerileri");
Add("hello@gadgetgo.example", "GadgetGo", "PROMOTIONS", 35, 0.04, true, 0, 0, 3, 120, "Yeni telefon modelleri", "Aksesuarlarda indirim günleri");
Add("coupons@foodrush.example", "FoodRush", "PROMOTIONS", 70, 0.02, true, 0, 0, 3, 85, "İlk alışverişinize özel indirim", "Bugün ne yesek?");
Add("notify@friendnet.example", "FriendNet", "SOCIAL", 90, 0.03, false, 0, 0, 5, 45, "Ayşe seni etiketledi", "3 yeni bildirimin var", "Yeni bir bağlantı isteği aldın");
Add("updates@picwall.example", "PicWall", "SOCIAL", 55, 0.02, false, 0, 0, 4, 60, "Fotoğrafın 20 beğeni aldı", "Bu hafta öne çıkanlar");
Add("digest@pinboard.example", "Pinboard", "SOCIAL", 40, 0.05, true, 0, 0, 4, 90, "Senin için seçtiğimiz fikirler", "Yeni panolar");
Add("no-reply@streamly.example", "Streamly", "UPDATES", 70, 0.03, false, 0, 0, 4, 55, "Aboneliğiniz yenilendi", "Yeni içerikler eklendi");
Add("billing@cloudbox.example", "CloudBox", "UPDATES", 30, 0.7, false, 0, 4, 3, 210, "Aralık ayı fatura özeti", "Faturanız hazır");
Add("alerts@bankdemo.example", "BankDemo", "UPDATES", 40, 0.6, false, 2, 3, 6, 130, "Hesap dekontunuz", "Kart harcama bildirimi: dekont ektedir");
Add("no-reply@accounts.demo-id.example", "Demo ID", "UPDATES", 25, 0.2, false, 0, 0, 5, 40, "Yeni cihazdan giriş yapıldı", "Doğrulama kodunuz", "Şifrenizi sıfırlama isteği");
Add("orders@marketplace.example", "Marketplace", "UPDATES", 50, 0.5, false, 3, 2, 4, 150, "Siparişiniz kargoya verildi", "Sipariş onayı", "Siparişiniz teslim edildi");
Add("tickets@airlines.example", "Demo Airlines", "UPDATES", 12, 0.9, false, 1, 8, 5, 280, "Biletiniz hazır", "Rezervasyon onayı");
Add("friend@mailbox.example", "Deniz Kaya", "NONE", 35, 0.9, false, 0, 1, 7, 30, "Hafta sonu buluşalım mı?", "Fotoğraflar", "Re: akşam planı");
Add("colleague@workmail.example", "Mert Aksoy", "NONE", 60, 0.85, false, 1, 6, 4, 90, "Toplantı notları", "Sunum dosyası", "Proje takvimi");
Add("family@homemail.example", "Aile", "NONE", 40, 0.95, false, 5, 2, 8, 70, "Bayram planı", "Yeni fotoğraflar", "Tarif");
Add("mentor@university.example", "Prof. Demir", "NONE", 15, 0.9, false, 1, 3, 6, 100, "Tez önerisi geri bildirimi", "Ders programı");
Add("landlord@homes.example", "Ev Sahibi", "NONE", 8, 0.9, false, 2, 3, 5, 180, "Kira sözleşmesi", "Aidat bilgilendirmesi");
Add("weekly@devdigest.example", "DevDigest", "NONE", 80, 0.4, true, 0, 0, 4, 70, "Haftanın en iyi makaleleri", "Yeni araçlar ve kütüphaneler");
Add("blog@designdaily.example", "DesignDaily", "NONE", 60, 0.15, true, 0, 0, 3, 100, "Bu haftanın ilham verici işleri", "Tasarım trendleri");

var sentTo = new[] { "friend@mailbox.example", "colleague@workmail.example", "family@homemail.example" };
for (var i = 0; i < 24; i++)
{
    var id = $"s{i + 1:x15}";
    rows.Add(new MessageRow(id, id, Account, "Demo User", "example.com", "Re: konu", now.AddDays(-random.Next(10, 1500)).ToUnixTimeMilliseconds(),
        20_000, "SENT", true, false, false, "NONE", false, false, sentTo[i % sentTo.Length]));
}

db.EnsureAccount(Account);
db.Upsert(rows);
db.SaveState(new InventoryState(null, rows.Count, now.AddMinutes(-9).ToString("o"), now.ToString("o")));
Analyzer.Run(db, now, Classifier.LoadAllowlist(AppPaths.Allowlist));
db.SetUserDecision(["weekly@devdigest.example"], Decision.Keep);

var links = db.GetSenders().Where(s => s.HasUnsubscribe).Select((s, i) =>
    (s.SenderAddress, i % 3 == 0 ? $"mailto:unsubscribe@{s.Domain}" : $"https://{s.Domain}/unsubscribe?u=demo"));
db.SaveUnsubscribeLinks(links);

var firstBatch = rows.Where(r => r.SenderAddress == "coupons@foodrush.example").Take(40).Select(r => r.Id).ToList();
var secondBatch = rows.Where(r => r.SenderAddress == "promo@travelnest.example").Take(25).Select(r => r.Id).ToList();
db.LogActions(firstBatch, "Trash", db.NextBatchNo());
var undoneNo = db.NextBatchNo();
db.LogActions(secondBatch, "Trash", undoneNo);
db.MarkUndone(secondBatch, undoneNo);

Console.WriteLine($"Demo data written for {Account}: {rows.Count} messages, {db.GetSenders().Count} senders in {AppPaths.Dir}");
return 0;
