# Contributing

Thanks for your interest. This project handles people's real mailboxes, so a few rules are strict.

## Ground rules

1. **Safety first.** A pull request must never introduce permanent deletion, the
   `https://mail.google.com/` scope, mail body downloads, or any network call other than the
   Gmail API. See [docs/SAFETY.md](docs/SAFETY.md).
2. **No personal data in the repository**: no real addresses, subjects, tokens, database files or
   screenshots of a real mailbox. Use the demo data tool (below) for screenshots and examples.
3. **AI-assisted contributions are welcome**, under the same rules. Say in the pull request that an assistant
   was used; you are responsible for what you submit ([docs/AI_ASSISTED_DEVELOPMENT.md](docs/AI_ASSISTED_DEVELOPMENT.md)).
4. **Tests for logic.** Classification rules, undo, throttling and anything that decides what
   happens to a mail comes with unit tests. The Gmail service is always faked in tests.

## Getting started

```
git clone https://github.com/<your-user>/mail-temizleyici.git
cd mail-temizleyici
dotnet build MailTemizleyici.sln
dotnet test MailTemizleyici.sln
```

Requirements: Windows 10/11, .NET 8 SDK (the desktop project is WPF). The Core library and the tests
are cross-platform. To run against a real mailbox follow [docs/SETUP.md](docs/SETUP.md).

## Try the app without a mailbox

The demo data tool writes a fictional mailbox (only `*.example` addresses) into a scratch folder and
never touches your real data:

```
set MAILTEMIZLEYICI_DATA_DIR=%TEMP%\mail-temizleyici-demo
dotnet run --project tools/DemoData
dotnet run --project src/MailTemizleyici.Desktop
```

Both commands must see the same `MAILTEMIZLEYICI_DATA_DIR`. Sign-in buttons will not work without a
real OAuth client, everything else (analysis, groups, preview, history) does.

## Code conventions

- C# 12, nullable enabled, file-scoped namespaces. Business logic lives in `MailTemizleyici.Core`;
  `MailTemizleyici.Desktop` only holds views and view models (MVVM with CommunityToolkit.Mvvm).
- The code base is intentionally comment-free: names, small methods and tests carry the meaning.
  Explain *why* in the pull request or in `docs/`, not in code.
- Least code that solves the whole problem. No speculative abstractions or options.
- Code, identifiers, commits and docs are in English. The application's user interface text is Turkish.
- UI work follows [docs/DESIGN.md](docs/DESIGN.md): colors and sizes come from `Themes/*.xaml`, never
  hard-coded in a view.

## Pull requests

- Keep them small and focused, describe the user-visible change and how you tested it.
- `dotnet build` must be warning-free and `dotnet test` green. CI runs both on Windows.
- Update the relevant document in `docs/` when behavior changes.
