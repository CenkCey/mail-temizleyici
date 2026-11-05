# Classification Rules

Decisions are made per sender, not per message. Rules are evaluated top to bottom; the first match wins. Keep rules always come first.

## Keep (untouchable)

1. Starred mail.
2. Addresses the user has mailed or replied to (`sent_contact`).
3. Mail under labels the user created.
4. Domains on the allowlist (below).

## Keep (caution, shown as Keep in the app)

5. Subject contains any of these keywords (Turkish and English, matching real subjects, do not translate): fatura, e-fatura, dekont, makbuz, sözleşme, poliçe, bilet, rezervasyon, garanti, sipariş, şifre, doğrulama, invoice, receipt, contract, ticket, warranty.
6. Mail with attachments (large attachments are also listed in a separate view).

## Trash candidates (shown as Trash in the app)

7. Category Promotions or Social.
8. Senders with an unsubscribe header and a read ratio under 10%.
9. Sender address contains `noreply`, `no-reply`, `donotreply`, `notification`, `bildirim`, and mail older than 1 year.
10. Category Updates and older than 2 years.

## Undecided

11. Anything matching no rule. Untouched until the user decides.

## Allowlist

Edit it in the app (Sender groups, "Güvenilir alanlar…"); the file is `%APPDATA%\MailTemizleyici\allowlist.txt`. The defaults below apply until the user saves a list. Example categories: banks, e-Devlet and public institutions, tax and accounting, insurance, employer and business partners, school.

```
turkiye.gov.tr
gib.gov.tr
google.com
```

## Thresholds

Read ratio and age thresholds are tuned with the user after the first review.
