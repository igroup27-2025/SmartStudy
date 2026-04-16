# Security review — user-credential storage

**Date:** 2026-04-14
**Scope:** `SmartStudy_Users` columns holding third-party credentials.

## What is stored today

| Column | Type | Purpose | Lifetime |
|---|---|---|---|
| `GoogleCalendarAccessToken` | NVARCHAR(MAX) | Bearer token for Google Calendar API (OAuth). | ~1h (refreshed via refresh token). |
| `GoogleCalendarRefreshToken` | NVARCHAR(MAX) | Long-lived OAuth refresh token for Google Calendar. | Until user revokes. |
| `ComposioConnectedAccountId` | NVARCHAR(255) | Opaque ID referencing a Composio-managed OAuth account. Not itself a credential. | Until disconnect. |
| `MoodleToken` | NVARCHAR(500) | Moodle Web Services token — acts as a bearer credential against the Moodle API. | Until user rotates. |
| `RuppinetId` | NVARCHAR(20) | University username. | Until disconnect. |
| `RuppinetPassword` | NVARCHAR(500) | University **password** stored to enable headless scrape-style sync. | Until disconnect. |

Written by `CalendarSyncController`, `MoodleSyncService`, `RuppinetSyncService`.
Read by the corresponding background sync services (`GoogleCalendarService`,
`MoodleBackgroundSyncService`, `RuppinetBackgroundSyncService`).

## Why they are stored

Each integration performs **background / unattended sync** (on login, on a
timer, or on-demand without re-prompting for the user's consent). That flow
requires credentials that outlive the browser session, so they are persisted.
This is a legitimate reason — but *persistence* and *plaintext at rest* are
two different decisions.

## Risks with the current approach

1. **Plaintext at rest.** Every credential above is stored as-is. A DB dump or
   read-only SQL access is equivalent to a full compromise of every connected
   Google Calendar, Moodle account, and university portal.
2. **Cascading blast radius.** `RuppinetPassword` is particularly sensitive —
   unlike an OAuth token, it's the user's actual reusable password and very
   likely reused elsewhere.
3. **No rotation / expiry on our side.** Even if Google revokes, our stored
   refresh token value lingers indefinitely. No retention policy is enforced.
4. **No access audit.** Any code path with a `DBservices` handle can read
   these columns; there's no separation between "normal" user data and secrets.

## Recommendations

Ordered roughly by cost/benefit:

1. **Stop storing `RuppinetPassword` entirely.** If Ruppinet offers any
   token/API-key mechanism, migrate to that. If it truly requires a reusable
   password for scraping, that's a product-level decision — but if we keep it,
   at minimum encrypt it (see #3) and require re-auth after a long idle period.
2. **Encrypt credentials at rest** using ASP.NET Core
   `IDataProtectionProvider` with a purpose string per column (e.g.
   `"smartstudy.google.refresh"`). Keys can be persisted to the filesystem or
   a KMS. This is a small code change and dramatically raises the bar for a
   DB-only attacker.
3. **Prefer the Composio path for Google.** `ComposioConnectedAccountId` is
   just a reference — the actual tokens live in Composio's vault. If that
   path works for all Google flows we need, we can drop
   `GoogleCalendarAccessToken` / `GoogleCalendarRefreshToken` from our DB.
4. **Retention policy.** Scrub all three credential columns when the user
   disconnects the integration *or* after N days of inactivity. Today
   `disconnect…` endpoints exist but retention-on-inactivity does not.
5. **Access boundary.** Consider a thin `ISecretStore` interface that the
   integration services depend on, instead of reading the columns directly
   via `DBservices`. Makes future migration to a secret manager trivial.

## Action items

- [ ] Decide: is `RuppinetPassword` acceptable to keep at all? (product call)
- [ ] Encrypt the four credential columns via `IDataProtectionProvider`.
- [ ] Add a retention job to null out credentials after N days of no sync.
- [ ] Audit whether Composio can replace the direct-token path for Google.

No code changes in this commit — this is a review note to drive the
follow-up discussion.
