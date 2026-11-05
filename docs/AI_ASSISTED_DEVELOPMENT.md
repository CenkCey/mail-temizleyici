# AI-assisted development

Mail Temizleyici was built **together with AI coding assistants**. This page says which tools were used,
how the collaboration worked, who decided what, how the output was checked and what its limits are.
Türkçe: [AI_ASSISTED_DEVELOPMENT.tr.md](AI_ASSISTED_DEVELOPMENT.tr.md).

## At a glance

| What | Detail |
| --- | --- |
| Main tool | **Claude Code** (Anthropic's command-line coding agent, Claude models) |
| Share of the work | Most of the code, tests, documentation, screenshots and tooling were written by the assistant under the maintainer's direction |
| Maintainer's role | Product decisions, safety rules, approvals, live testing on a real mailbox, review of behavior, publication |
| Safety net | Unit tests, a source-scanning safety test, dry-run and confirmation before every real action, human approval before every call that touches a mailbox |
| Not done | No independent security audit and no line-by-line human review of every file |

Commits carry a `Co-Authored-By` trailer for the assistant where it wrote the change.

## Tools used

| Tool | Used for |
| --- | --- |
| **Claude Code** | Planning, writing and refactoring C# and XAML, writing tests, running builds and tests, writing the documentation, driving the app for screenshots |
| **Claude Code plugins** (open source) | *caveman*: short chat replies; it never shortens code, files in `docs/`, commit messages, error messages or the confirmation text shown before a Gmail action. *ponytail*: "smallest complete change" discipline (reuse before writing, tests for new logic, a review pass after each phase). *ui-ux-pro-max*: a searchable design knowledge base used for UI and accessibility guidance (styles, palettes, typography, WPF notes) |
| **Claude in Chrome** (browser automation) | One-time setup of the Google Cloud project: enable the Gmail API, consent screen, Desktop OAuth client. Done with the owner's explicit approval; the downloaded `client_secret.json` was moved into place and **never read** by the assistant |
| **Helper scripts** | PowerShell with UI Automation and GDI+ (window screenshots, logo drawing), Python (contrast ratios, read-only SQLite checks). Development aids only, not part of the repository |

The third-party plugins and their skill files are not redistributed in this repository.

## How the collaboration worked

1. **Rules first.** A project instructions file for the assistant defined the immutable safety rules
   (see [SAFETY.md](SAFETY.md)), the code rules (least code, no speculative abstractions, no code
   comments, tests for rules and undo) and the language rules (English for code and docs, Turkish for the
   UI). That file is a private working file; its content is published as [SAFETY.md](SAFETY.md) and
   [CONTRIBUTING.md](../CONTRIBUTING.md).
2. **Docs before code.** The plan, classification rules and design system were written and agreed before
   implementation. Decisions such as "WPF instead of a console app", "no Excel review", "trash only" and
   "scope `gmail.readonly` first" were made by the maintainer.
3. **Phase by phase.** Work followed numbered phases (connect, inventory, analysis, review, trash, undo,
   subscriptions) with two custom slash commands, `/phase <n>` and `/project-status`. A phase ended with a
   build, the tests, a review pass and an update of the progress notes.
4. **Permissions as guard rails.** The assistant's settings deny reading `client_secret*.json`,
   `token*.json` and `*.mbox`. Tool calls ran behind the agent's permission prompts.
5. **Human approval gates.** Before any action against a real mailbox the assistant had to describe it
   in full, uncompressed Turkish and wait. The maintainer ran, or approved and watched, the first live tests on a
   separate test mailbox and verified in Gmail that trashing and undo behaved as described.
6. **Verification loop.** Unit tests for every rule and failure mode, an automated review of the code
   (ten findings fixed), full-window screenshots after UI changes, a contrast-ratio script, research on
   Gmail API quotas from the official documentation, and a privacy audit of the whole tree in a scratch
   git repository before anything was published.

## Who decided what

| Maintainer | Assistant |
| --- | --- |
| What the product is for, its principles, what must never happen to a mail | Proposed designs and alternatives with trade-offs |
| Choice of WPF, trash-only, headers-only, own OAuth client | Implemented them, kept the rules enforced by tests |
| Visual direction (dark purple, glass, accent gradient) from reference images | Translated it into tokens and styles, checked contrast |
| Whether starred mail may be trashed (opt-in box on the preview) | Implemented it with a stronger confirmation text |
| Every live test, every approval, what to publish and when | Prepared, scripted and documented |

## What went wrong, and how it was caught

Being honest about mistakes is part of the method:

- **A wrong quota assumption.** The assistant first assumed `messages.get` costs 5 quota units. Reading the
  official quota page showed 20 units, which changed the whole speed strategy
  ([INVENTORY_SPEED.md](INVENTORY_SPEED.md)).
- **Wasteful retry design.** One failed call discarded a whole 500-mail page. Found by measuring a real run
  (200 mails per minute against a 300 ceiling) and reading the code; fixed with per-chunk saving.
- **A crash on screen change.** A null `DataContext` while a grid emptied crashed the app. Found by the
  maintainer using the app; fixed and re-checked.
- **Confusing numbers.** Gmail's mail count excludes Trash, the local inventory does not. The maintainer
  noticed the mismatch during a live test; the UI now explains it.
- **A rule that was too rigid.** Starred mail could never be trashed. The maintainer hit that limit; it
  became an explicit opt-in.

## Verify it yourself

```
dotnet test MailTemizleyici.sln        # 101 tests, including the safety test
dotnet run --project tools/DemoData    # fictional demo mailbox (see the README)
```

`SafetyTests` fails the build if the source ever contains a permanent-delete call or the
`https://mail.google.com/` scope, and it asserts that only `gmail.readonly` and `gmail.modify` are requested.

## Limits and honest disclaimers

- The code was largely written by an AI model and checked mostly through tests, automated review and the
  maintainer's use. Treat it as you would any new open-source tool that touches your mail: read
  [SAFETY.md](SAFETY.md), start with the trial mode and keep a Google Takeout backup.
- AI output can be wrong in ways tests do not cover. Please report problems (see
  [SECURITY.md](../SECURITY.md)).
- The project is released under the MIT license; the maintainer publishes AI-written parts under the
  same terms.

## Contributing with AI tools

AI-assisted contributions are welcome under the same rules as any other: the safety rules in
[SAFETY.md](SAFETY.md), tests for logic, no personal data in the diff, and an honest note in the pull
request that an assistant was used. You are responsible for what you submit.
