# Client Integration Guide — Scorer's Table App

For the team building the statistics-collection app. This is the one page you need before your
first API call; the full contract is in `docs/platform-architecture.md` (§7 event catalogue,
§8 court frame, §10 validation, §11 API, §12.1 offline) and every endpoint is in Swagger at
`/swagger` when the API runs in Development.

**Run it locally on day one**: `dotnet run --project src/Hoops.Api`, then
`dotnet run --project tools/Hoops.Seeder`. You get two organisations, a league with three recorded
games and 25 unplayed fixtures, and logins — see `tools/Hoops.Seeder/README.md`.

---

## 1. Three facts that shape the app

**The app owns the clock.** The server never runs one. Every event carries `period` and
`gameClockMs` — milliseconds **remaining** in the period — and the server validates they move the
right way. This is deliberate: a scorer's table must work with no connection at all.

**Events are the only thing you write.** You never send a score, a foul count, or a statline. You
send *what happened*; every number is derived server-side and comes back in `state` on each
response. If the app ever computes a total, it is for display while offline, and it must be replaced
by the server's the moment a response arrives.

**Every event has a client-generated id.** Generate a UUID v7 on the device the moment the official
taps. That id is the idempotency key: submitting it twice returns `200` with the original result,
never a duplicate. This is what makes offline queues and retries safe.

---

## 2. Authentication

```
POST /api/v1/auth/login     { email, password }  → { accessToken, refreshToken, accessTokenExpiresAt, ... }
POST /api/v1/auth/refresh   { refreshToken }       → same shape, new pair
GET  /api/v1/auth/me                               → who am I, which organisations, which roles
```

Send `Authorization: Bearer <accessToken>`. Access tokens last **60 minutes**; a game lasts longer.
Refresh tokens last 14 days.

- Refresh proactively when `accessTokenExpiresAt` is within a few minutes — do not wait for a `401`
  in the middle of a free-throw sequence.
- **Offline, the token may expire while events queue.** That is fine: recording never touches the
  server. Refresh first when connectivity returns, then flush the queue.
- Recording needs the **Statistician** role (or higher) in the organisation that owns the game.
  `/auth/me` tells you. Every URL below is under `/api/v1/organisations/{orgId}/…`.

---

## 3. Before the game

```
GET  competitions/{competitionId}/games            fixtures (ids and team ids)
GET  games/{gameId}/setup                          the setup screen: fixture, rule set, both rosters
                                                   WITH names, jersey numbers, positions, tier
POST games/{gameId}/lock-roster                    { selections: [{ rosterEntryId, isStarter }] }
GET  games/{gameId}/roster                         the FROZEN game roster — use these ids from now on
POST games/{gameId}/start                          → state
```

Two ids look similar and are not interchangeable:

| | From | Used for |
|---|---|---|
| `rosterEntryId` | `/setup` | selecting who dresses, in `lock-roster` |
| `gameRosterEntryId` | `/roster` (after lock) | **every event** — `gameRosterEntryId`, `secondaryRosterEntryId` |

The game roster is a snapshot. A jersey change on the competition roster after lock does not affect
this game, by design (§6). Cache `/roster` locally; you need it offline.

The `ruleSet` in `/setup` tells you period length, number of periods, foul-out limit, timeouts per
half, and shot clock — drive the UI from it rather than hard-coding FIBA.

---

## 4. Submitting events

```
POST games/{gameId}/events           one event      → 201 { sequence, event, state }
POST games/{gameId}/events/batch     ordered batch  → 201 { sequence, event (last), state }
```

```json
{
  "eventId": "01930c1e-7b3a-7f6e-9b2c-4d1e8a0f2b3c",
  "lastKnownSequence": 41,
  "eventType": "FIELD_GOAL_MADE",
  "eventSubtype": "ThreePoint",
  "period": 2,
  "gameClockMs": 412300,
  "competitionTeamId": "…",
  "gameRosterEntryId": "…shooter…",
  "secondaryRosterEntryId": "…assister, or null…",
  "shotXCm": 580,
  "shotYCm": -250,
  "clientRecordedAt": "2026-01-15T16:42:11.512+01:00",
  "payload": {}
}
```

- `lastKnownSequence` is the last `sequence` you received. Send it on every submit. Omit it only on
  the very first event.
- `points` is **not** yours to send; the server derives it from type and subtype.
- `shotZone` and `shotDistanceCm` come back computed; never send them.
- Send `clientRecordedAt` with the device's offset. The server stores UTC and returns UTC everywhere.
- Use **batch** for things that are one action to the official but several events to the log: a
  free-throw sequence, a substitution group at a dead ball. The batch is atomic.

### Which fields each event type needs

| Event | `eventSubtype` | actor (`gameRosterEntryId`) | `secondaryRosterEntryId` | notes |
|---|---|---|---|---|
| `PERIOD_START` | `Regulation` \| `Overtime` | — | — | clock = full period length |
| `CLOCK_START` / `CLOCK_STOP` | — | — | — | |
| `FIELD_GOAL_MADE` / `_MISSED` | `TwoPoint` \| `ThreePoint` | shooter | assister (made) / blocker (missed) | `shotXCm`, `shotYCm` |
| `FREE_THROW_MADE` / `_MISSED` | — | shooter | — | `payload: { attemptNumber, totalAttempts, sourceEventId? }` |
| `REBOUND` | `Offensive` \| `Defensive` | rebounder | — | must follow a miss |
| `TEAM_REBOUND` | `Offensive` \| `Defensive` \| `DeadBall` | — | — | `competitionTeamId` |
| `TURNOVER` | `BadPass` `LostBall` `Travelling` `DoubleDribble` `Carrying` `OffensiveFoul` `ThreeSeconds` `FiveSeconds` `EightSeconds` `ShotClockViolation` `OutOfBounds` `BackCourt` `Other` | player losing it | stealer (opponent) | |
| `FOUL` | `Personal` \| `Shooting` \| `Offensive` \| `Technical` \| `Unsportsmanlike` \| `Disqualifying` \| `BenchTechnical` \| `CoachTechnical` | offender (null for bench/coach) | player fouled | `payload: { freeThrowsAwarded }` |
| `SUBSTITUTION` | — | player **out** | player **in** | clock must be stopped |
| `TIMEOUT` | — | — | — | `competitionTeamId`, `payload: { timeoutType: "Team" \| "Media" \| "Official" }` |
| `PERIOD_END` | — | — | — | clock = 0 |
| `NOTE` | — | — | — | free text in `payload` |

Assists, steals and blocks are **attributes of another event**, not events (ADR-002): the assister
rides on the made shot, the stealer on the turnover, the blocker on the miss.

Ending the game is not an event you send: `POST games/{gameId}/end` after the last `PERIOD_END`.

---

## 5. Reading the response

Every successful submit returns the full live state — treat it as the truth and re-render from it:

```json
"state": {
  "lastSequence": 42, "currentPeriod": 2, "gameClockMs": 412300, "clockRunning": true,
  "gameStarted": true, "gameEnded": false, "periodEnded": false,
  "score":             { "<homeCompetitionTeamId>": 31, "<awayCompetitionTeamId>": 28 },
  "teamFouls":         { "…": 3, "…": 4 },
  "timeoutsRemaining": { "…": 1, "…": 2 },
  "onCourt":           { "…": ["gameRosterEntryId", …5], "…": [ …5 ] }
}
```

`GET games/{gameId}/state` returns the same thing on demand, `GET games/{gameId}/box-score` the
per-player lines for a mid-game stats screen.

---

## 6. Errors — and what the UI should do

Every error is RFC 9457 problem-details with a stable `code`:

```json
{ "status": 400, "code": "SHOT_ZONE_MISMATCH", "detail": "The shot was recorded as TwoPoint but its coordinates fall in AboveBreakThree. Submit with override=true and a reason to record it anyway.", "traceId": "…" }
```

Validation has two tiers. Map on `code`, not on wording.

**Tier 1 — rejected, cannot be overridden.** The tap was impossible; show the message and let the
official correct it.

`GAME_NOT_IN_PROGRESS` `EVENT_BEFORE_GAME_START` `PLAYER_NOT_ON_ROSTER` `WRONG_TEAM`
`SHOT_OUT_OF_BOUNDS` `ASSIST_SELF` `ASSIST_WRONG_TEAM` `STEAL_SAME_TEAM` `BLOCK_SAME_TEAM`
`FOUL_DRAWN_SAME_TEAM` `FREE_THROW_SEQUENCE` `TIED_AT_GAME_END`

**Tier 2 — rejected, but the official can insist.** Offer a confirm dialog with a reason, then resend
the same event with `?override=true` and header `X-Override-Reason: <text>`. It is recorded and
flagged for the review screen.

`PLAYER_NOT_ON_COURT` `PLAYER_FOULED_OUT` `INVALID_LINEUP_SIZE` `SUBSTITUTION_WHILE_LIVE`
`CLOCK_NOT_MONOTONIC` `CLOCK_OUT_OF_RANGE` `REBOUND_WITHOUT_MISS` `FREE_THROW_WITHOUT_SOURCE`
`SHOT_ZONE_MISMATCH` `TIMEOUT_LIMIT_EXCEEDED` `PERIOD_NOT_COMPLETE`

**Other codes you will meet:**

| code | status | do |
|---|---|---|
| `STALE_SEQUENCE` | 409 | Another device recorded ahead of you (or your resync is behind). `detail` lists the sequences you are missing. `GET games/{id}/events?sinceSequence=<yourLast>`, apply them, then resubmit with the new `lastKnownSequence`. |
| `UNKNOWN_EVENT_TYPE` | 400 | A typo in `eventType`. |
| `RATE_LIMITED` | 429 | Absurdly generous (600/min per game); only a bug hits it. Wait `Retry-After`, resend — idempotent, so safe. |
| `UNAUTHENTICATED` | 401 | Refresh the token, retry once. |
| `FORBIDDEN` | 403 | Wrong organisation or role. Not retryable. |
| `VALIDATION_ERROR` | 400 | Malformed body; `errors` names the fields. |

---

## 7. Corrections

```
POST games/{gameId}/events/undo-last                    → 201  voids the most recent non-void event
POST games/{gameId}/events/{eventId}/void   { reason }  → 201  voids a specific one
```

Nothing is ever deleted. A void is itself an event appended to the log; the voided event stays but
contributes nothing. Both return the new `state`. Show "undo" as a first-class button — it is safe.

---

## 8. Offline and sync

The API is designed so the app can record an entire game offline and sync afterwards. The protocol:

1. **Queue locally** in tap order, each with its UUID v7 `eventId` and the `state` *you* expect.
2. **On connectivity**, refresh the token if needed, then submit the queue **in order**, one request
   at a time, each carrying `lastKnownSequence` from the previous response.
3. **On `200` (not `201`)** the event was already there — a retry after a lost response. Continue.
4. **On `STALE_SEQUENCE`** resync (§6) and continue. This only happens if another device recorded
   the same game; single-device play never sees it.
5. **On a tier-1 rejection mid-queue**, stop and show the official — the remaining queue may depend
   on it. Tier-2 rejections in a queue should be surfaced together for override, not one by one.
6. Never reorder, never drop. If the app cannot reconcile, `GET /events` is the truth; rebuild from it.

Do not attempt to compute standings or leaderboards on the device. They are recomputed on
finalisation through an outbox and served from `competitions/{id}/standings` and
`…/leaders`.

---

## 9. Shot coordinates (§8)

Shots are reported in one canonical frame, **always as the attacking half-court**, whichever end the
team is physically shooting at:

- `x` runs from half-court (`0`) toward the baseline (`1400`); the hoop is at `x = 1242.5, y = 0`.
- `y` runs across the width, `-750` (left, facing the hoop) to `+750`.
- All values are integer centimetres. `x < 0` is backcourt and is rejected for a shot.

The three-point arc is 675 cm from the hoop; corners are beyond `|y| = 660`. You do not classify
zones — the server does (`shotZone` in the response) and cross-checks your `TwoPoint`/`ThreePoint`
subtype against it (`SHOT_ZONE_MISMATCH`). So: map the tap on your court graphic to this frame,
flipping when the team attacks the other end, and let the server keep you honest.

---

## 10. What is *not* there

- **No real-time push.** The recording app does not need it — state returns on every submit. A
  spectator scoreboard or second-screen view would; it does not exist yet. Do not design one against
  polling `/state` without talking to the backend team first.
- **No server clock.** See §1.
- **No photo upload from the game screen.** Player photos go through the registry endpoints.
- **Staging URL** — coming. Until then, run the API locally with the seeder; it is a complete
  environment.

---

## 11. Team labels on the fixture list

`GET competitions/{id}/games` returns team **ids**. Load `GET competitions/{id}/teams` once per
competition (8–16 rows) and join on `competitionTeamId`; the setup and roster screens carry names
directly. If the fixture list becomes a pain point, tell us — adding names there is a small change.
