# MariaBot3000 Improvement TODO

Last updated: 2026-10-27

Current state: single-file F#/.NET Playwright prototype that polls Steam profile-comment notifications, generates replies through OpenRouter, and posts them. It does not currently automate private Steam chat.

Guiding constraint: keep the default mode draft-only and test against local fixtures until the Steam workflow is explicitly authorized and the reliability/safety foundations below are complete. Do not add stealth, CAPTCHA bypass, or anti-detection behavior.

## P0: Security and authorization blockers

- [x] Remove all plaintext API keys and secrets from source, scratch files, and generated artifacts.
- [ ] Load the replacement key from `OPENROUTER_API_KEY` or a secret manager; fail startup with a non-secret error when missing.
- [x] Treat `storageState.json` and `.steam-profile/` as sensitive session data. Revoke affected Steam sessions if either has been shared or exposed.
- [x] Add `.gitignore` entries for secrets, `.env`, `.auth/`, `.steam-profile/`, `storageState.json`, Playwright traces/screenshots, `bin/`, and `obj/`.
- [x] Confirm with Valve/Steam that the exact workflow is permitted. Steam Subscriber Agreement §4.C broadly restricts non-human-controlled interaction with Steam services.
- [x] Remove prompt instructions that encourage covertly starting arguments, baiting, manipulating, or escalating conflict.
- [x] Remove or clearly frame fabricated biography, trauma, mental-health, relationship, and sexual details so they are never presented as real experiences.
- [ ] Add clear AI-assisted/automated disclosure where appropriate.
- [ ] Make draft-only mode the default; live posting must require explicit opt-in for an authorized workflow.
- [ ] Do not implement stealth, human-like typing, anti-bot evasion, CAPTCHA bypass, or Steam Guard automation.

## P1: Reply correctness and durable state

- [ ] Introduce a structured `Comment` record: comment ID, thread ID, target profile, author ID/name, text, timestamp, and canonical URL.
- [ ] Extract author, body, timestamp, and permalink relative to the same comment container. Stop zipping independently collected sequences.
- [ ] Establish stable notification and comment identities; fail closed when identity cannot be verified.
- [ ] Add a SQLite state store with a unique operation key based on account and target comment.
- [ ] Model workflow states: `Discovered`, `Drafted`, `Submitting`, `Posted`, and `NeedsReview`.
- [ ] Persist processing state before posting, but do not mark a notification processed until the intended reply is confirmed.
- [ ] On startup, recover or reconcile stale `Submitting` operations.
- [ ] After a posting timeout or ambiguous click result, inspect the thread for the expected reply before retrying.
- [ ] Ensure a restart never loses deduplication history or posts the same reply twice.
- [ ] Handle no-notification, multiple-unread-notification, deleted-comment, and profile-private scenarios.
- [ ] Keep posting sequential until identity, idempotency, consent, and limits are proven reliable.

## P1: Authentication and browser lifecycle

- [ ] Add a separate, explicitly headful `login` command.
- [ ] Verify the expected signed-in Steam account at worker startup instead of trusting `.auth/authfile`.
- [ ] Remove or replace the `.auth/authfile` marker heuristic.
- [ ] Never automate passwords, Steam Guard, MFA, or CAPTCHA; pause for human action or cancellation.
- [ ] Handle signed-out redirects, expired sessions, and unexpected account changes with a clear stop-for-review state.
- [ ] Use one session strategy. Either use the persistent profile only or explicitly support storage state, not both ambiguously.
- [ ] Scope the persistent browser context with `use!` and ensure deterministic cleanup.
- [ ] Store browser profiles outside the repository in a restrictive user-specific state directory.
- [ ] Add a single-process lock per browser-profile directory.
- [ ] Pass a `CancellationToken` through polling, HTTP calls, and startup tasks.
- [ ] Replace fixed sleeps with cancellable `Task.Delay`.
- [ ] Add SIGINT/SIGTERM handling and reachable browser cleanup.
- [ ] Name variables accurately (for example, distinguish `IBrowserContext` from browser/driver objects).

## P1: Playwright reliability

- [ ] Extract a scoped `SteamCommentsPage`/page-object module instead of scattering selectors through `Program.fs`.
- [ ] Scope notification queries to a verified notification container rather than the whole page.
- [ ] Replace CSS-class-count read/unread detection with a verified semantic state.
- [ ] Replace positional `Locator("div").Nth(2)` assumptions with verified structure.
- [ ] Scope comment author/body/timestamp extraction to each comment container.
- [ ] Scope the comment textbox and submit button to the intended target thread.
- [ ] Prefer verified roles and exact accessible names over broad `GetByText("Post Comment")`.
- [ ] Assert exactly one intended target before filling or clicking; zero or multiple matches should produce a diagnostic and skip.
- [ ] Wait for visible/enabled elements and expected application states instead of attached-state plus fixed sleeps.
- [ ] Confirm the expected new comment appears after submission rather than assuming a successful click.
- [ ] Explicitly configure or validate Steam UI language; current selectors depend on English text.
- [ ] Subscribe to page errors, console errors, failed requests, and browser disconnects.
- [ ] Make screenshots/traces opt-in, access-controlled, and short-lived because they may capture private account data.

## P1: OpenRouter client hardening

- [ ] Reuse a scoped `HttpClient` instead of creating one per generation.
- [ ] Use end-to-end `task`/`let!` with cancellation rather than blocking `Async.RunSynchronously`.
- [ ] Require successful HTTP status and distinguish provider, validation, authentication, and rate-limit errors.
- [ ] Handle malformed JSON, provider error payloads, empty choices, and null/empty content.
- [ ] Enforce request/response size limits and a configurable output token limit.
- [ ] Validate generated reply length and required constraints independently of the prompt.
- [ ] Add configurable model, timeout, and input limits.
- [ ] Retry only transient failures with a small cap and backoff; honor `Retry-After`.
- [ ] Do not retry `400`, `401`, `403`, or content-policy failures blindly.
- [ ] Avoid duplicate billable generation requests where possible.
- [ ] Stop logging full prompts, raw provider responses, comments, or account details.

## P1: Safety, consent, and rate limits

- [ ] Require explicit participant/profile/thread allowlists for any authorized live mode.
- [ ] Add a persistent opt-out list and honor Steam blocks, privacy settings, and deletion requests.
- [ ] Re-check consent/opt-out status immediately before submission.
- [ ] Enforce minimum intervals and per-recipient, per-thread, per-account, and daily caps.
- [ ] Add deterministic outbound checks for harassment, threats, sexual content, scams, unwanted links, personal data, and excessive length.
- [ ] Route sensitive or uncertain cases to human review instead of posting automatically.
- [ ] Treat Steam comments as untrusted data, never as instructions.
- [ ] Add a checked emergency-stop/kill switch before generation and posting.
- [ ] Add a circuit breaker for authentication loss, CAPTCHA, repeated Steam errors, or rate limiting.
- [ ] Keep navigation and posting serialized; do not scale concurrency until all safety controls are proven.

## P2: Validation, configuration, and maintainability

- [ ] Replace positional URL string splitting with validated URL parsing.
- [ ] Accept only HTTPS and exact allowed Steam hosts.
- [ ] Support only validated `/id/<name>` and `/profiles/<steamid64>` routes.
- [ ] Validate path segment counts, nonempty identifiers, query/fragment handling, and URL escaping.
- [ ] Remove or fix the latent `steampath` URL helper that can combine components from different URLs.
- [ ] Replace compile-time `testing` flags with validated runtime configuration.
- [ ] Make mode explicit: `local-fixture`, `draft`, or authorized `send`.
- [ ] Configure expected account identity, model, poll intervals, hard safety caps, timeouts, and retry limits.
- [ ] Remove current-working-directory assumptions; resolve persona/config/state paths deterministically.
- [ ] Declare `personality.txt` as project content or embed it so running from another directory works.
- [ ] Add an SDK `global.json` and package lock file.
- [ ] Split `Program.fs` into focused modules: `Config`, `SteamProfileUrl`, `SteamCommentsPage`, `OpenRouterClient`, `CommentPolicy`, `StateStore`, and cancellable `Worker`.
- [ ] Reduce `Program.fs` to CLI handling and composition.

## P2: Observability

- [ ] Replace unstructured `printfn` output with structured logging.
- [ ] Add operation/correlation IDs and durations.
- [ ] Track discovered, skipped, drafted, posted, duplicate, and failed counts.
- [ ] Record HTTP/provider status categories and retry counts.
- [ ] Log explicit skip reasons such as `OptedOut`, `AlreadyProcessed`, and `AuthenticationRequired`.
- [ ] Never log cookies, authorization headers, full prompts, session state, or raw user content by default.
- [ ] Add health/status visibility for last successful poll, last successful post, and current circuit-breaker state.

## P2: Offline test suite

- [ ] Add a separate test project.
- [ ] Unit-test `/id/` and `/profiles/` URL parsing, redirects, query strings, malformed hosts, and invalid routes.
- [ ] Unit-test comment extraction and field alignment.
- [ ] Unit-test empty, malformed, error, rate-limited, and single-choice OpenRouter responses.
- [ ] Unit-test prompt limits, output limits, opt-outs, rate limits, deduplication, and state transitions.
- [ ] Unit-test retry decisions and uncertain-post reconciliation.
- [ ] Add Playwright integration tests against local synthetic HTML fixtures.
- [ ] Cover no notifications, multiple unread notifications, multiple comments, duplicate timestamp markup, missing selectors, and locale changes.
- [ ] Cover signed-out redirects, expired sessions, Steam Guard/CAPTCHA stop states, and unexpected account identity.
- [ ] Cover post success, timeout-after-success, confirmed failure, and process restart in every persisted state.
- [ ] Use a fake OpenRouter `HttpMessageHandler`; never require live Steam or real session files in CI.
- [ ] Gate any authorized live smoke test separately, use a dedicated account, and default it to no-send.
- [ ] Add CI once the core offline test suite exists.

## P3: Documentation and cleanup

- [ ] Add a README covering setup, supported .NET SDK, Playwright installation, secure manual login, state locations, secret rotation, dry-run behavior, testing, and privacy.
- [ ] Document the Steam policy limitation and required authorization before live posting.
- [ ] Document recovery from `NeedsReview`, expired sessions, and uncertain posting outcomes.
- [ ] Remove or repair the unrelated `hello.fsx` scratch script; it is not compiled and its current Playwright bindings are incorrect.
- [ ] Remove unused URL helpers and dead code after extracting tested components.
- [ ] Decide whether unused `storageState.json` should be securely deleted after confirming it is unnecessary.
- [ ] Remove generated build artifacts containing secrets from any shared/distributed copy.

## Suggested milestones

1. **Security and authorization:** rotate secrets, contain session data, add ignores, settle platform permission, establish draft-only default.
2. **Offline draft-only prototype:** safe prompting, structured comment extraction, validated URLs, local fixtures, no posting.
3. **Durable pipeline:** stable comment identity, SQLite state, deduplication, state transitions, restart recovery.
4. **Authorized browser hardening:** manual login, account verification, reliable selectors, posting confirmation, cancellation, cleanup.
5. **Safety and operations:** consent/allowlists, rate limits, moderation checks, structured logging, circuit breakers.
6. **Testing and release readiness:** unit/integration tests, README, configuration validation, CI.
7. **Live send mode:** only after the above and explicit authorization; keep human review and emergency stop available.

## Next-milestone definition of done

- [ ] No secrets in source, scratch files, generated artifacts, or logs.
- [ ] Application starts in draft-only mode and cannot post live.
- [ ] Comments are parsed into stable, container-scoped records.
- [ ] URL and account validation fails closed.
- [ ] OpenRouter errors and malformed responses are handled without posting.
- [ ] Offline fixtures cover notification, comment, authentication, and provider failures.
- [ ] Restart tests demonstrate that no comment is processed twice.
- [ ] README documents setup, security, limitations, and test commands.
