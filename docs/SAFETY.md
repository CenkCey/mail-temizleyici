# Safety model

The app is built around one promise: **it must not be able to lose your mail**. The rules below are
design constraints, not preferences. A pull request that weakens one of them will not be merged.

## The rules

| # | Rule | How it is enforced |
| --- | --- | --- |
| 1 | Mail is only moved to Trash, never deleted permanently. | The code path uses `messages.batchModify` (add `TRASH`). `messages.delete`, `batchDelete` and the `https://mail.google.com/` scope appear nowhere. |
| 2 | Least privilege. | `gmail.readonly` until the trash step, then `gmail.modify` with its own token and consent. |
| 3 | Mail bodies are never downloaded. | `format=metadata` with only `From`, `To`, `Subject`, `Date`, `List-Unsubscribe`. The only way to view a mail is "open in Gmail". |
| 4 | Secrets stay out of the project. | OAuth client file and tokens live in the user profile (`%APPDATA%\MailTemizleyici\`); `.gitignore` blocks them; the app never prints or logs them. |
| 5 | Every real action is previewed first. | Dry-run is the default; a separate preview screen and a confirmation dialog whose default button is "Vazgeç" (cancel) come first; the destructive button stays disabled until the user confirms a backup. |
| 6 | Every move is logged. | Rows go to `action_log` *before* the Gmail call; undo works only from this log and restores the original labels. |
| 7 | Keep beats Trash; the unclear is left alone. | Keep rules are evaluated first; an Undecided group is never processed automatically; starred mail is skipped unless the user opts in on the preview. |
| 8 | Respect the API. | Rate-limit errors (429, 403) are retried with exponential backoff and a slower rate; an interrupted inventory resumes. |

## Threats and mitigations

| Threat | Mitigation |
| --- | --- |
| A wrong rule trashes an important mail | Keep-first rules, per-sender review, trial mode, Takeout reminder, 30-day Trash, undo log. |
| Mixing two accounts' data | One database per account plus a guard that stops inventory, trash and undo on a mismatch. |
| Credentials leak through the repository | `.gitignore` for secrets, databases and mbox files; credentials are outside the project; the README and setup guide say never to share them. |
| A malicious unsubscribe link | Only `http`, `https` and `mailto` schemes are opened, after an explicit confirmation that shows the host. |
| A crafted address or id breaks a path or URL | Database file names replace invalid characters; Gmail URLs escape the account and the id. |
| Partial failure during a large move | The log is written per chunk before the call and rolled back for a rejected chunk; undo is idempotent per row. |
| The app exposes data to a third party | No telemetry and no network calls except the Gmail API; all data is local SQLite. |

## What the app cannot protect against

- Emptying Gmail's Trash yourself, or waiting more than 30 days, makes a move permanent. Check the
  result and undo in time.
- A mistaken *Sil* decision on a sender you do review but misjudge. Use the trial mode, look at the
  sender's mail list, and keep the Takeout backup.
- A compromised computer: the local database holds sender addresses and subjects of your mail.

## Verification

- Unit tests cover the plan builder (starred and already-trashed handling), the runner's logging and
  rollback, undo's label restoration, the account guard and the rate-limit behavior.
- `SafetyTests` scans every source file of `src/` for permanent-delete calls and the broad
  `https://mail.google.com/` scope and fails the build if one appears, and asserts that exactly the
  `gmail.readonly` and `gmail.modify` scopes are requested.
