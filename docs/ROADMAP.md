# Roadmap

## Done

- Read-only connection, resumable and quota-aware inventory, incremental "fetch new mail"
- Per-sender analysis with transparent Keep, Trash and Undecided rules
- Sender review screen with filters, bulk decisions, starred filter, mail list with "open in Gmail",
  editable trusted-domain list
- Subscriptions screen with unsubscribe links and a copyable Gmail filter query
- Dry-run preview, trial mode, confirmation dialog, move to Trash, history and undo by batch
- One database per account, account switching, account guard
- Dark and light theme, themed title bar, keyboard accessibility
- Throttled fetch engine based on the real Gmail quota

## Ideas (not committed)

- English (and other) UI languages through resource files
- Own screen for starred mail and a "large attachments" view
- Undo per sender group, not only per batch
- Persist the theme choice, high-contrast support, status bar
- Optional import of headers from a Google Takeout archive to avoid the API quota (needs a design for
  mapping archive mails to Gmail ids, see [INVENTORY_SPEED.md](INVENTORY_SPEED.md))
- Category-scoped first inventory pass (promotions, social, updates) for very large mailboxes
- Installer or single-file release with a published checksum

## Known limitations

- Windows only; the UI is Turkish.
- Inventory speed is bound by the Gmail API quota (about 300 mails per minute).
- Mail deleted in Gmail after the inventory stays in the local database until it is rebuilt.
- Gmail filters are not created by the app (a `gmail.settings.basic` scope would be needed); it copies a
  search query instead.
