## What and why

## How I tested it

- [ ] `dotnet build MailTemizleyici.sln` is warning-free
- [ ] `dotnet test MailTemizleyici.sln` is green
- [ ] New or changed logic has unit tests

## Safety checklist

- [ ] No permanent deletion, no `https://mail.google.com/` scope, no mail body download
- [ ] No personal data, tokens or real mailbox screenshots in the diff
- [ ] Docs in `docs/` updated if behavior changed
