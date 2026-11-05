# Setup

Mail Temizleyici uses **your own** Google Cloud project and OAuth client. Nothing is shared with the
author or any server. Plan about 15 minutes.

## 1. Back up your mail

1. Open <https://takeout.google.com>.
2. Choose "Deselect all", then select only **Mail**.
3. Start the export. For large mailboxes this can take hours; the download link arrives by e-mail.

The backup is required before the first real move to Trash, not for the read-only steps.

## 2. Create the Google Cloud project

1. At <https://console.cloud.google.com> create a new project, for example `mail-temizleyici`.
2. *APIs & Services → Library*: enable the **Gmail API**.
3. *OAuth consent screen* (Google Auth Platform): user type **External**, publishing status **Testing**.
   Under *Test users* add your own Gmail address (and every other account you want to clean).
4. Add the scopes `.../auth/gmail.readonly` and `.../auth/gmail.modify`. Do **not** add
   `https://mail.google.com/`.
5. *Credentials → Create credentials → OAuth client ID*, application type **Desktop app**.
6. Download the JSON file and save it as `client_secret.json` in:
   - `%APPDATA%\MailTemizleyici\client_secret.json`

Keep this file private: do not put it into the project folder, do not commit it and do not share it.
The repository's `.gitignore` blocks `client_secret*.json` and `token*.json` as a safety net.

> Because the consent screen stays in *Testing* mode, Google shows an "unverified app" warning at
> sign-in (choose *Advanced → continue*) and tokens expire after 7 days. For a personal clean-up
> that is fine. Publishing the app for other people would require Google's verification and is out of scope.

## 3. Build and run

Requirements: Windows 10 or 11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
git clone https://github.com/CenkCey/mail-temizleyici.git
cd mail-temizleyici
dotnet build MailTemizleyici.sln
dotnet test MailTemizleyici.sln
dotnet run --project src/MailTemizleyici.Desktop
```

A runnable folder without the SDK on the target machine (needs the .NET 8 Desktop Runtime there):

```
dotnet publish src/MailTemizleyici.Desktop -c Release -r win-x64 --self-contained false -o publish
```

Run `publish\MailTemizleyici.Desktop.exe`.

## 4. Where data is stored

| What | Where |
| --- | --- |
| OAuth client, tokens, allowlist, databases | `%APPDATA%\MailTemizleyici\` |
| Different location | Set the environment variable `MAILTEMIZLEYICI_DATA_DIR` to a folder before starting the app. |

## 5. First run

Follow the [user guide](USER_GUIDE.md): connect, run the inventory, analyze, review the sender groups,
preview, trial move, check in Gmail.

## 6. When you are done

- Remove the app's access at <https://myaccount.google.com/permissions>.
- Delete the `%APPDATA%\MailTemizleyici` folder (databases contain sender addresses and subjects).
- Optionally delete the Google Cloud project.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| "client_secret.json not found" | The file is missing or in the wrong folder. See step 2. |
| Google says "Access blocked" | The account is not on the *Test users* list of the consent screen. |
| Sign-in asks again after a week | Normal in Testing mode. Press *Bağlan* again. |
| "Quota exceeded" or 429 in the status line | The Gmail API limit. The app slows down and retries by itself; just wait. |
| Counts differ between Connection and Inventory | Gmail's count excludes Trash. See the FAQ in the user guide. |
| The app shows another account's data | Use *Hesap değiştir*; each account has its own database. |
