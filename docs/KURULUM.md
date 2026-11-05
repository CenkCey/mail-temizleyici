# Kurulum

Mail Temizleyici **kendi** Google Cloud projenizi ve OAuth istemcinizi kullanır. Yazarla veya herhangi bir
sunucuyla hiçbir şey paylaşılmaz. Yaklaşık 15 dakika ayırın. İngilizce sürüm: [SETUP.md](SETUP.md).

## 1. Mail yedeği

1. <https://takeout.google.com> adresine gidin.
2. "Tümünün seçimini kaldır" deyin, yalnızca **Mail**'i seçin.
3. Dışa aktarımı başlatın. Büyük hesaplarda saatler sürebilir; indirme bağlantısı e-postayla gelir.

Yedek, ilk gerçek çöpe taşımadan önce gereklidir; salt okunur adımlar için gerekmez.

## 2. Google Cloud projesi

1. <https://console.cloud.google.com> adresinde yeni proje açın, örneğin `mail-temizleyici`.
2. *APIs & Services → Library* altında **Gmail API**'yi etkinleştirin.
3. *OAuth consent screen* (Google Auth Platform): user type **External**, yayın durumu **Testing**.
   *Test users* altına kendi Gmail adresinizi (ve temizlemek istediğiniz diğer hesapları) ekleyin.
4. Kapsamlara `.../auth/gmail.readonly` ve `.../auth/gmail.modify` ekleyin. `https://mail.google.com/`
   **eklemeyin**.
5. *Credentials → Create credentials → OAuth client ID*, tür **Desktop app**.
6. JSON dosyasını indirin ve `client_secret.json` adıyla şuraya koyun:
   - `%APPDATA%\MailTemizleyici\client_secret.json`

Bu dosyayı gizli tutun: proje klasörüne koymayın, commit'lemeyin, kimseyle paylaşmayın. Deponun `.gitignore`
dosyası ek güvence olarak `client_secret*.json` ve `token*.json` dosyalarını engeller.

> Onay ekranı *Testing* modunda kaldığı için Google girişte "doğrulanmamış uygulama" uyarısı gösterir
> (*Gelişmiş → devam*) ve token 7 günde sona erer. Kişisel bir temizlik için bu sorun değildir. Uygulamayı
> başkaları için yayınlamak Google doğrulaması gerektirir ve bu projenin kapsamı dışındadır.

## 3. Derleme ve çalıştırma

Gereksinimler: Windows 10 veya 11 ve [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
git clone https://github.com/CenkCey/mail-temizleyici.git
cd mail-temizleyici
dotnet build MailTemizleyici.sln
dotnet test MailTemizleyici.sln
dotnet run --project src/MailTemizleyici.Desktop
```

Hedef bilgisayarda SDK olmadan çalışacak klasör (orada .NET 8 Desktop Runtime gerekir):

```
dotnet publish src/MailTemizleyici.Desktop -c Release -r win-x64 --self-contained false -o publish
```

`publish\MailTemizleyici.Desktop.exe` dosyasını çalıştırın.

## 4. Veriler nerede durur

| Ne | Nerede |
| --- | --- |
| OAuth istemcisi, token'lar, güvenilir alan listesi, veritabanları | `%APPDATA%\MailTemizleyici\` |
| Farklı konum | Uygulamayı başlatmadan önce `MAILTEMIZLEYICI_DATA_DIR` ortam değişkenine bir klasör verin. |

## 5. İlk çalıştırma

[Kullanım kılavuzunu](USER_GUIDE.tr.md) izleyin: bağlan, envanter, analiz, gönderen gruplarını gözden geçir,
önizleme, deneme taşıması, Gmail'de kontrol.

## 6. İş bitince

- <https://myaccount.google.com/permissions> adresinden uygulamanın erişimini kaldırın.
- `%APPDATA%\MailTemizleyici` klasörünü silin (veritabanları gönderen adreslerini ve konuları içerir).
- İsterseniz Google Cloud projesini de silin.

## Sorun giderme

| Belirti | Neden ve çözüm |
| --- | --- |
| "client_secret.json bulunamadı" | Dosya yok veya yanlış klasörde. 2. adıma bakın. |
| Google "Erişim engellendi" diyor | Hesap, onay ekranındaki *Test users* listesinde değil. |
| Bir hafta sonra yeniden giriş isteniyor | Testing modunda normaldir. *Bağlan*'a yeniden basın. |
| Durum satırında "Quota exceeded" veya 429 | Gmail API sınırı. Uygulama kendiliğinden yavaşlar ve yeniden dener; bekleyin. |
| Bağlantı ve Envanter sayıları farklı | Gmail'in sayısı çöp kutusunu içermez. Kılavuzdaki SSS'ye bakın. |
| Uygulama başka bir hesabın verisini gösteriyor | *Hesap değiştir*'i kullanın; her hesabın ayrı veritabanı vardır. |
