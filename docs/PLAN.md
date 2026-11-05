# Original project plan

> This is the plan the project started from. The current state is described in [ARCHITECTURE.md](ARCHITECTURE.md), [SAFETY.md](SAFETY.md) and [ROADMAP.md](ROADMAP.md).

## Goal and principles

A fully local WPF desktop app that cleans a long-accumulated mailbox with no risk of losing important mail and without giving any third party access.

- **Local only:** Mail and analysis results stay on the user's computer.
- **Own key:** Access uses the user's own Google Cloud project and OAuth identity.
- **Read first, delete later:** Early phases run with read-only permission.
- **Reversible:** No permanent deletion; move to trash (recoverable for 30 days), preceded by a Takeout backup.
- **User has the last word:** The tool suggests; it never runs bulk actions without approval.

## Architecture

| Component | Choice | Why |
| --- | --- | --- |
| Connection | Gmail API, `Google.Apis.Gmail.v1` | Direct Gmail search syntax, labels, categories; faster than IMAP |
| Identity | Own Google Cloud project, OAuth "Desktop app", testing mode | No password stored; access revocable anytime |
| Scope phases 1–4 | `gmail.readonly` | Tool physically cannot delete |
| Scope phase 5+ | `gmail.modify` | Move to trash and labels; no permanent delete |
| Data read | Headers and labels only | No content downloaded |
| Storage | SQLite, one file per account under `%APPDATA%\MailTemizleyici\veri\<email>.db` | Analysis and action log without going back to Gmail; switching accounts shows that account's own data |
| UI | WPF (.NET 8), MVVM, `CommunityToolkit.Mvvm` | User decides in an in-app DataGrid; design in `docs/DESIGN.md` |
| Backup | Google Takeout (mbox) | Full archive before deleting |

No AI classification in the first version; later, only for the Undecided group if needed.

## Solution structure

- `src/MailTemizleyici.Core`: Gmail, SQLite, classification, undo. UI-independent.
- `src/MailTemizleyici.Desktop`: WPF, MVVM, only `CommunityToolkit.Mvvm` as extra package. No business logic.
- `tests/MailTemizleyici.Tests`: xUnit, tests Core.

## Screens

1. **Connection/Setup:** OAuth status, account, total mail count.
2. **Inventory:** progress bar, cancel, resume where stopped.
3. **Analysis summary:** totals, largest senders by size, most frequent senders.
4. **Sender groups:** filterable DataGrid; suggestion, rule, and Keep / Trash / Undecided selection (single and bulk).
5. **Dry-run preview and confirmation:** count, groups, space freed; explicit confirmation dialog for the real action.
6. **Action history and undo:** restore by batch or group.

Dry-run is always the default; moving to trash runs only after the confirmation dialog.

## Data model

- `message`: id, thread_id, sender_address, sender_name, domain, subject, date, size, labels, is_read, is_starred, is_important, category, has_unsubscribe, has_attachment
- `sender_summary`: sender_address, domain, count, total_size, first_date, last_date, read_ratio, user_replied, has_unsubscribe, suggested_decision, rule, user_decision
- `sent_contact`: address (everyone the user has mailed; from the SENT label)
- `inventory_state`: last_page_token, fetched_count, started_at, finished_at
- `action_log`: id, message_id, action, date, batch_no, undone

## Phases

1. **Preparation and backup:** Start the Takeout backup; create the Google Cloud project and OAuth identity (`docs/KURULUM.md`). Set up the solution skeleton (Core, Desktop, Tests); test OAuth sign-in with `gmail.readonly`; show profile info (total mail count) on the Connection screen.
2. **Inventory:** The Inventory screen pulls all message headers into SQLite. Batch requests, quota handling, resume where stopped.
3. **Analysis:** Analysis fills the `sender_summary` and `sent_contact` tables.
4. **Classification and review:** Rules mark each group Keep / Trash / Undecided. The Sender groups screen shows the largest and most frequent groups on top; old mail with large attachments has its own view. The user corrects decisions in the DataGrid; decisions are saved to `sender_summary.user_decision`.
5. **Trial and apply:** Scope upgraded to `gmail.modify` (new token). The preview screen is dry-run by default: count, groups, space freed. After the confirmation dialog, move to trash: first one small group, then in batches. Every action is logged.
6. **Check:** The user looks at the trash for a few days. The Action history screen restores from the log by group or batch.
7. **Lasting order:** A Subscriptions view lists unsubscribe links. Gmail filter suggestions are produced. The tool can be run monthly.

## Risks

| Risk | Mitigation |
| --- | --- |
| Important mail trashed | Takeout + trash only + Keep priority + dry-run + undo |
| OAuth key leak | Files in user profile, outside git; revoke access when done |
| Excess permission | Read-only first; permanent-delete scope never requested |
| Local database | Headers only; can be deleted when the project ends |
| Quota limits | Batch requests, waiting, exponential retry |
| Token lifetime in testing mode | Re-sign-in about every 7 days; fine for a one-off job |
| Takeout delay | Start on day 1; trash only after the backup arrives |
