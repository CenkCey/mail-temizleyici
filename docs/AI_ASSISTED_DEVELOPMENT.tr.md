# Yapay zeka destekli geliştirme

Mail Temizleyici, **yapay zeka kodlama asistanlarıyla birlikte** geliştirildi. Bu sayfa hangi araçların
kullanıldığını, iş birliğinin nasıl yürüdüğünü, kimin neye karar verdiğini, çıktının nasıl denetlendiğini ve
sınırlarını anlatır. English: [AI_ASSISTED_DEVELOPMENT.md](AI_ASSISTED_DEVELOPMENT.md).

## Bir bakışta

| Ne | Ayrıntı |
| --- | --- |
| Ana araç | **Claude Code** (Anthropic'in komut satırı kodlama ajanı, Claude modelleri) |
| İşin payı | Kodun, testlerin, dokümanların, ekran görüntülerinin ve yardımcı araçların çoğunu proje sahibinin yönlendirmesiyle asistan yazdı |
| Proje sahibinin rolü | Ürün kararları, güvenlik kuralları, onaylar, gerçek posta kutusunda canlı test, davranış incelemesi, yayın |
| Güvenlik ağı | Birim testleri, kaynak kodu tarayan güvenlik testi, her gerçek işlemden önce dry-run ve onay, posta kutusuna dokunan her çağrıdan önce insan onayı |
| Yapılmayanlar | Bağımsız güvenlik denetimi ve her dosyanın satır satır insan incelemesi yapılmadı |

Asistanın yazdığı değişikliklerde commit'lerde `Co-Authored-By` satırı bulunur.

## Kullanılan araçlar

| Araç | Ne için |
| --- | --- |
| **Claude Code** | Planlama, C# ve XAML yazma ve yeniden düzenleme, test yazma, derleme ve test çalıştırma, doküman yazma, ekran görüntüsü için uygulamayı gezme |
| **Claude Code eklentileri** (açık kaynak) | *caveman*: kısa sohbet yanıtları; kodu, `docs/` dosyalarını, commit mesajlarını, hata mesajlarını ve Gmail işleminden önceki onay metnini asla kısaltmaz. *ponytail*: "en küçük tam değişiklik" disiplini (yazmadan önce yeniden kullan, yeni mantığa test, her fazdan sonra gözden geçirme). *ui-ux-pro-max*: arayüz ve erişilebilirlik rehberliği için aranabilir tasarım bilgi tabanı (stiller, paletler, tipografi, WPF notları) |
| **Claude in Chrome** (tarayıcı otomasyonu) | Google Cloud projesinin tek seferlik kurulumu: Gmail API, onay ekranı, Masaüstü OAuth istemcisi. Sahibinin açık onayıyla yapıldı; indirilen `client_secret.json` yerine taşındı ve asistan tarafından **hiç okunmadı** |
| **Yardımcı betikler** | UI Automation ve GDI+ ile PowerShell (pencere görüntüleri, logo çizimi), Python (kontrast oranları, salt okunur SQLite kontrolleri). Yalnızca geliştirme yardımcıları, depoda yoklar |

Üçüncü taraf eklentiler ve yetenek dosyaları bu depoda yeniden dağıtılmaz.

## İş birliği nasıl yürüdü

1. **Önce kurallar.** Asistan için bir proje talimat dosyası değişmez güvenlik kurallarını ([SAFETY.md](SAFETY.md)),
   kod kurallarını (en az kod, spekülatif soyutlama yok, kod yorumu yok, kurallar ve geri alma için test) ve dil
   kurallarını (kod ve dokümanlar İngilizce, arayüz Türkçe) tanımladı. Bu dosya özel bir çalışma dosyasıdır;
   içeriği [SAFETY.md](SAFETY.md) ve [CONTRIBUTING.md](../CONTRIBUTING.md) olarak yayımlanır.
2. **Önce doküman, sonra kod.** Plan, sınıflandırma kuralları ve tasarım sistemi uygulamadan önce yazıldı ve
   onaylandı. "Konsol yerine WPF", "Excel ile inceleme yok", "yalnızca çöp kutusu" ve "önce `gmail.readonly`
   kapsamı" gibi kararları proje sahibi verdi.
3. **Faz faz ilerleme.** Çalışma numaralı fazlarla (bağlantı, envanter, analiz, inceleme, çöp, geri alma,
   abonelikler) ve iki özel slash komutuyla (`/phase <n>`, `/project-status`) yürüdü. Faz; derleme, testler,
   gözden geçirme ve ilerleme notlarının güncellenmesiyle bitti.
4. **Koruyucu izinler.** Asistanın ayarları `client_secret*.json`, `token*.json` ve `*.mbox` okumayı yasaklar.
   Araç çağrıları ajanın izin istemleri arkasında çalıştı.
5. **İnsan onay kapıları.** Gerçek bir posta kutusuna dokunan her işlemden önce asistan bunu tam, sıkıştırılmamış
   Türkçe ile anlatıp beklemek zorundaydı. Proje sahibi ilk canlı testleri ayrı bir test hesabında yaptı veya onaylayıp izledi ve
   çöpe taşıma ile geri almanın anlatıldığı gibi çalıştığını Gmail'de doğruladı.
6. **Doğrulama döngüsü.** Her kural ve hata modu için birim testleri, kodun otomatik gözden geçirilmesi (on bulgu
   düzeltildi), arayüz değişikliklerinden sonra tam pencere ekran görüntüleri, kontrast oranı betiği, Gmail API
   kotalarının resmi dokümandan araştırılması ve herhangi bir şey yayımlanmadan önce tüm ağacın geçici bir git
   deposunda gizlilik denetimi.

## Kim neye karar verdi

| Proje sahibi | Asistan |
| --- | --- |
| Ürün ne işe yarar, ilkeleri, bir maile asla ne olmamalı | Tasarım ve alternatifleri artı-eksileriyle önerdi |
| WPF, yalnızca çöp kutusu, yalnızca başlıklar, kendi OAuth istemcisi seçimi | Uyguladı, kuralları testlerle zorunlu kıldı |
| Referans görsellerden görsel yön (koyu mor, cam, vurgu gradyanı) | Token ve stillere çevirdi, kontrastı denetledi |
| Yıldızlı mailin çöpe taşınıp taşınamayacağı (önizlemede isteğe bağlı kutu) | Daha güçlü bir onay metniyle uyguladı |
| Her canlı test, her onay, neyin ne zaman yayımlanacağı | Hazırladı, betikledi, belgeledi |

## Neler ters gitti ve nasıl yakalandı

Hataları dürüstçe söylemek yöntemin parçasıdır:

- **Yanlış kota varsayımı.** Asistan önce `messages.get` için 5 kota birimi varsaydı. Resmi kota sayfası 20
  birim olduğunu gösterdi ve hız stratejisinin tamamı değişti ([INVENTORY_SPEED.md](INVENTORY_SPEED.md)).
- **Israf eden yeniden deneme tasarımı.** Tek bir başarısız çağrı 500 maillik sayfanın tamamını çöpe
  attırıyordu. Gerçek bir çalıştırmayı ölçerek (300 tavanına karşı dakikada 200 mail) ve kodu okuyarak bulundu;
  parça başı kayıtla düzeltildi.
- **Ekran değişiminde çökme.** Bir tablo boşalırken `DataContext`'in null olması uygulamayı çökertti. Proje sahibi
  uygulamayı kullanırken buldu; düzeltildi ve yeniden denendi.
- **Kafa karıştıran sayılar.** Gmail'in mail sayısı çöp kutusunu içermez, yerel envanter içerir. Proje sahibi
  uyuşmazlığı canlı testte fark etti; arayüz artık bunu açıklıyor.
- **Fazla katı bir kural.** Yıldızlı mail asla çöpe taşınamıyordu. Proje sahibi bu sınıra takıldı; açık bir
  isteğe bağlı seçenek oldu.

## Kendiniz doğrulayın

```
dotnet test MailTemizleyici.sln        # güvenlik testi dahil 101 test
dotnet run --project tools/DemoData    # kurgusal demo posta kutusu (README'ye bakın)
```

`SafetyTests`, kaynakta kalıcı silme çağrısı veya `https://mail.google.com/` kapsamı bulunursa derlemeyi bozar ve
yalnızca `gmail.readonly` ile `gmail.modify` istendiğini doğrular.

## Sınırlar ve dürüst uyarılar

- Kodun büyük kısmı bir yapay zeka modeliyle yazıldı ve çoğunlukla testlerle, otomatik gözden geçirmeyle ve proje
  sahibinin kullanımıyla denetlendi. Postanıza dokunan her yeni açık kaynak araç gibi değerlendirin:
  [SAFETY.md](SAFETY.md) dosyasını okuyun, Deneme modundan başlayın ve Google Takeout yedeği tutun.
- Yapay zeka çıktısı, testlerin kapsamadığı biçimlerde yanlış olabilir. Sorunları lütfen bildirin
  ([SECURITY.md](../SECURITY.md)).
- Proje MIT lisansıyla yayımlanır; proje sahibi yapay zekanın yazdığı kısımları da aynı koşullarla yayımlar.

## Yapay zeka araçlarıyla katkı

Yapay zeka destekli katkılar diğerleriyle aynı kurallarla kabul edilir: [SAFETY.md](SAFETY.md) içindeki güvenlik
kuralları, mantık için testler, farkta kişisel veri olmaması ve pull request'te bir asistan kullanıldığına dair
dürüst bir not. Gönderdiğinizden siz sorumlusunuz.
