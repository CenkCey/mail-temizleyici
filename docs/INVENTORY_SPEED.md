# Inventory speed: research and decisions 

Question: fetching all mail headers takes too long. How can it be faster without breaking the safety rules?

## What the sources say

Official Gmail API documentation (usage limits and per-method quota units, handle-errors guide, batch guide), read 2026-11-05:

| Fact | Value |
| --- | --- |
| Limit per user per project | 6.000 quota units per minute |
| Limit per project | 1.200.000 units per minute |
| `messages.get` (metadata) | 20 units (60 for `full`, `raw`) |
| `messages.list` | 5 units per call, up to 500 ids |
| `messages.trash` | 20 units; `messages.batchModify` 50 units per call regardless of size |
| Batch calls | Each inner call counts separately; at most 100 per batch, Google says do not send more than 50 |
| Concurrency | A per-user concurrent request limit exists (error 429 "Too many concurrent requests for user"), shared by every client of that user; no number published |
| Raising limits | Per-user limits cannot be increased; the only official workaround is splitting work across accounts. Project limits can be requested but approval is not guaranteed |

Consequence: reading headers through the API costs 20 units per mail, so the hard ceiling is 6.000 / 20 = 300 mails per minute (18.000 per hour, 100.000 mails in about 5,6 hours). Parallel requests, bigger batches or a second client cannot beat that, they only trigger 429 errors. The earlier project notes assumed 5 units per `messages.get`; that was wrong.

## What the measurements say

From `inventory_state` of a real test run on a 1.688-mail mailbox: 8,4 minutes = 200 mails per minute. The very first run took 13,6 minutes (124 per minute) with two resumes after quota errors.

## Why the old code was slower than the ceiling

1. One failed call in a page threw away the whole page. A page is 500 mails (50 batches). All 49 good batches were discarded and the page was fetched again after a 5 to 160 second wait, up to 6 times.
2. Rows were saved only when the whole page was done, so a cancel or crash lost up to 500 fetched mails of work.
3. A fixed 1 second pause after every batch of 10, independent of the real quota use.
4. Batches of 10 mails ran at about 460 mails per minute in bursts (9.200 units per minute), above the real limit, which produced the quota errors in the first place.

## What was built

- `Throttle` (token bucket, 5.400 units per minute = 90 percent of the limit, batches of 20): spends units, not seconds. On a rate-limit error it pauses 5, 10, 20 ... 60 seconds, cuts the rate by 20 percent (never below 40 percent) and shrinks the batch (never below 5); each success restores them step by step.
- `ThrottledFetch`: retries only the failed ids, never the successful ones, and gives up with Gmail's own message after six rounds without any success.
- `GmailInventory`: every chunk is written to SQLite as soon as it arrives, ids already in the database are skipped (resume costs nothing), the same engine serves inventory, "Yeni mailleri al" and the subscription link fetch.
- Inventory screen shows mails per minute and the estimated time left.

Expected result: about 250 to 285 mails per minute without wasted calls (up from 200), and no lost work after errors. It cannot go beyond about 300 per minute. Not yet measured on live Gmail.

## Options that go beyond the ceiling (need a decision)

| Option | Gain | Cost and risk |
| --- | --- | --- |
| A. Category-scoped first pass: list ids (5 units per 500) with queries such as `category:promotions OR category:social OR category:updates`, fetch headers only for those | Clutter is usually the bulk of an old mailbox, so maybe 2 to 3 times fewer mails to fetch; primary mail is never touched | Sender decisions are then based on partial data; keep rules that need whole-sender knowledge (starred or replied mail in Primary from the same sender) are blind to the skipped mail. Trashing still acts only on fetched mails, so nothing outside the set is moved. The rest can be fetched later with the same screen |
| B. Import headers from a Google Takeout mbox | Zero quota, limited by disk speed | Takeout has no Gmail message ids, so trash and undo would need an id lookup (`rfc822msgid:` search, 5 units per mail) or a change to trash by query; large redesign, high risk. Not recommended now |
| C. IMAP with OAuth | Not limited by the 6.000 units | Needs the full-access `https://mail.google.com/` scope, which safety rule 2 forbids. Rejected |
| D. Only fetch mail older than N days or larger than N MB | Fewer mails | Misses small old clutter; `larger:` queries only help the size goal |
| E. Run unattended (sleep prevention, resume) | Nothing faster, but no babysitting | Inventory already resumes; only the window must stay open |

Recommendation: ship the throttle (done), measure once on the test mailbox, then decide on A for the main mailbox. B and C are not worth the risk.

## Sources

- Gmail API usage limits and quota units: https://developers.google.com/workspace/gmail/api/reference/quota
- Gmail API error handling (concurrency, batch size, retry): https://developers.google.com/workspace/gmail/api/guides/handle-errors
- Gmail API batch requests: https://developers.google.com/workspace/gmail/api/guides/batch
- Report of 429 on batches of 100 `messages.get` in the .NET client: https://lightrun.com/answers/googleapis-google-api-dotnet-client-need-some-recommendations-when-gmail-api-batch-request-returns-429-responses
