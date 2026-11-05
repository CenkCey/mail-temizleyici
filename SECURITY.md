# Security policy

Mail Temizleyici works on a real mailbox, so security and privacy reports are taken seriously.

## Reporting a vulnerability

Please do **not** open a public issue for a security problem. Use GitHub's private
vulnerability reporting for this repository (Security tab, "Report a vulnerability").
Describe the impact and the steps to reproduce. Never attach real mail content, tokens,
`client_secret.json` or database files, redact them first.

You can expect an acknowledgement within a few days. There is no bug bounty.

## What the project guarantees

These are hard rules of the design (details in [docs/SAFETY.md](docs/SAFETY.md)); a change that
breaks one of them is a security bug:

- No permanent deletion: `messages.delete`, `messages.batchDelete` and the
  `https://mail.google.com/` scope are never used or requested.
- Mail bodies are never downloaded; only headers (`From`, `To`, `Subject`, `Date`,
  `List-Unsubscribe`), labels, size and dates are read.
- The OAuth client file and tokens live outside the repository, in
  `%APPDATA%\MailTemizleyici\` (or the folder named by `MAILTEMIZLEYICI_DATA_DIR`).
- All data stays on the user's computer. The app makes no network calls except to the Gmail API.
- Every move to Trash is logged and can be undone.

## Your responsibilities as a user

- Create your own Google Cloud project and OAuth client ([docs/SETUP.md](docs/SETUP.md)). Never share
  `client_secret.json` or the token files, and never commit them.
- Take a Google Takeout backup before the first real trash run.
- Revoke the app's access at <https://myaccount.google.com/permissions> when you are done.
- The local database contains sender addresses, subjects and dates of your mail. Treat it as
  personal data, do not upload it, and delete the data folder when you no longer need it.
