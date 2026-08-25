# Implementation Roadmap

**Companion:** `platform-architecture.md` — the reference (what and why). This is the execution order (how and when). Hand both to Claude Code. Where they disagree, the architecture document wins.

**One repository, one database, one API**, serving three products: the Player Registry, Tournament Management, and Statistics Recording. Give Claude Code one phase at a time.

---

## 0. Before writing any code

### 0.1 Repository bootstrap

Run `setup.sh` (provided alongside these documents). It creates the monorepo, all backend projects, the project reference graph, packages, and the supporting configuration files. It writes **no application code** — that is Claude Code's job, phase by phase.

```bash
chmod +x setup.sh && ./setup.sh
```

### 0.2 Layout

```
hoops-api/
├── CLAUDE.md                  # working agreements — re-read every session
├── README.md
├── docker-compose.yml         # local Postgres
├── Directory.Build.props      # nullable, warnings-as-errors, XML docs on
├── BasketballStats.sln
├── docs/
│   ├── platform-architecture.md
│   └── implementation-roadmap.md
├── src/
│   ├── Hoops.Api/                    # controllers, auth, middleware
│   ├── Hoops.SharedKernel/
│   ├── Hoops.Modules.Identity/
│   ├── Hoops.Modules.Registry/
│   ├── Hoops.Modules.Competitions/
│   ├── Hoops.Modules.GameRecording/
│   ├── Hoops.Modules.Statistics/
│   └── Hoops.Infrastructure/
└── tests/
    ├── Hoops.UnitTests/
    ├── Hoops.IntegrationTests/
    └── Hoops.ArchitectureTests/
```

The three front ends live in their own repositories and are not scaffolded here.

### 0.3 Reference graph

The script wires this. **It is the module boundary made mechanical** — do not add references beyond it without revisiting §4 of the architecture document.

```
Hoops.SharedKernel   → nothing
Hoops.Modules.*      → SharedKernel only  (no Infrastructure, no MVC)
Hoops.Infrastructure → SharedKernel + all modules
Hoops.Api            → SharedKernel + Infrastructure + all modules
```

Because modules cannot see `Hoops.Infrastructure` or MVC, a module physically cannot reach for a `DbContext` or return an `IActionResult` — the compiler catches it. Repositories are declared as interfaces in each module's `Application` layer and implemented in Infrastructure; controllers live only in `Hoops.Api`.

The one rule the compiler cannot enforce is `Contracts`-only access **between** modules, since all of them are referenced by the host. That is what the architecture test in Phase 1 is for.

### 0.4 Local infrastructure

`docker-compose.yml` at the repo root runs Postgres 16 with a healthcheck. `docker compose up -d` before running the API or the integration tests.

### 0.5 `CLAUDE.md` — written by the script, review it before committing

Claude Code reads this on every session. It is the highest-leverage file in the project.

```markdown
# Hoops — Working Agreements

## Architecture
Read `docs/platform-architecture.md` before changing anything structural.
It is the source of truth. If a shortcut conflicts with it, raise the conflict
rather than diverging silently.

## Non-negotiables
1. `game_events` rows are NEVER updated (except is_voided) and NEVER deleted.
2. Projection tables must be fully rebuildable from `game_events` alone.
   If you write something to a projection that cannot be derived from the log,
   you have broken the product. There is a test for this.
3. The projector is a PURE function. No I/O, no DateTime.Now, no randomness.
4. Modules reference other modules ONLY through their `Contracts` namespace.
5. Every tenant-scoped entity implements ITenantScoped and has a global query filter.
6. Statistics are never authored directly. Only events are authored.
7. `players` is a platform-level entity: no organisation_id, no tenant filter.
   Registry entities are the ONLY intentional exception. See ADR-003.
8. The plaintext NIN is NEVER stored, logged, or placed in a URL. HMAC only.
   If you find yourself writing `player.Nin = ...`, stop and re-read ADR-008.
9. Rosters are built from an existing playerId. Never from a free-text name.

## Conventions
- MVC Controllers with [ApiController]. One folder per module under Hoops.Api/Controllers/.
  Controllers are thin: parse, call one application service, map the result.
  MVC types NEVER appear in module class libraries.
- EVERY action carries an explicit [Authorize(Policy = ...)] or [AllowAnonymous].
  There is an architecture test for this. Do not disable it.
- EVERY action carries [ProducesResponseType] for each status it returns, plus
  XML doc comments — these are the Swagger documentation.
- No MediatR. No AutoMapper. Hand-written ToDto() extension methods.
- Result<T> for expected failures. Exceptions only for genuinely exceptional cases.
- snake_case in the database, PascalCase in C#. Configure via EF conventions.
- All timestamps are timestamptz in UTC. Game clock is milliseconds REMAINING.
- Jersey numbers are strings. "00" and "0" are different players in FIBA.

## Testing
- Every new validation rule needs a positive and a negative unit test.
- Every projector change needs its golden files regenerated AND reviewed by a human.
- Run `dotnet test` before declaring any phase complete.

## Commands
dotnet run --project src/Hoops.Api
dotnet ef migrations add <Name> --project src/Hoops.Infrastructure --startup-project src/Hoops.Api
dotnet test
```

Copy both markdown documents into `docs/` and commit them.

---

## 1. Build Order

Seven phases. Each ends in a demonstrable, tested state. **Do not start a phase until the previous one's acceptance criteria pass.** The temptation to jump ahead to Phase 4 is strong because it's the interesting part; resist it, because Phase 4 depends on roster snapshots that Phase 2 and 3 produce.

---

### Phase 1 — Foundation

**Goal:** an authenticated, multi-tenant API skeleton that starts, connects to Postgres, and rejects unauthorised callers.

**Build:**
- `Hoops.SharedKernel`: `Result<T>`, `Error`, `ITenantScoped`, `IDomainEvent`, `Guard`, strongly-typed ID structs
- `AppDbContext` with snake_case naming convention and global query filter registration
- `ICurrentTenant` / `ICurrentUser`, resolved from JWT claims in middleware
- Identity module: users, organisations, memberships, roles
- Auth endpoints: register, login, refresh, logout, me
- `ProblemDetails` exception middleware (RFC 9457) with `code` and `traceId`; configure `ProblemDetailsFactory` so `[ApiController]` model-binding failures match the same shape
- Swashbuckle with XML comments, JWT bearer security definition, and `api/v1` grouping
- Base controller with `Result<T>` → `ActionResult<T>` mapping
- `/health` and `/health/ready`
- First EF migration

**Acceptance criteria:**
- [ ] `docker compose up -d && dotnet run` starts cleanly and applies migrations
- [ ] Register → login → call `/auth/me` with the bearer token returns the user
- [ ] Calling any org endpoint without a token returns `401`, not `500`
- [ ] Calling an org endpoint for an org the user does not belong to returns `403`
- [ ] Errors render as `application/problem+json`, never a stack trace
- [ ] Architecture test passes: no module references `Hoops.Infrastructure`
- [ ] Architecture test passes: no module class library references any MVC type
- [ ] Architecture test passes: every controller action has an explicit `[Authorize]` or `[AllowAnonymous]`
- [ ] Swagger UI renders at `/swagger`, shows XML descriptions, and authorises with a bearer token

---

### Phase 2 — Competition Management

**Goal:** the management app is functional end to end for everything except fixtures.

**Build:**
- Seasons, competitions (with the `RuleSet` JSONB and a strongly-typed binding)
- Teams (canonical), competition entries, stages, groups
- Venues
- Team staff

**Acceptance criteria:**
- [ ] A competition can be created with a custom rule set, and the JSONB round-trips into the typed `RuleSet` without loss
- [ ] A team can be entered into two competitions in different seasons and appears once in the canonical table
- [ ] Tenancy test: org A cannot read or mutate any org B entity through any endpoint

Rosters are deliberately **not** in this phase. They depend on the registry, which is next.

---

### Phase 2B — Player Registry

**Goal:** a working registry with the identity, privacy, and consent machinery in place before a single player record exists.

This phase carries legal exposure none of the others do. Everything here is easier built correctly the first time than retrofitted — particularly the redaction policy and the audit trail, which are worthless applied after NINs are already sitting in log files in the clear.

**Build:**
- `players` as a **platform-level** entity: no `organisation_id`, no tenant filter
- `INinHasher` — HMAC-SHA256 with a pepper from the secrets manager. Plaintext never persisted, never logged
- `nin_hmac` unique index; NIN accepted only in request bodies, never in query strings
- Serilog destructuring policy redacting `nin`, `ninHmac`, `guardianPhone`, `photoObjectKey`
- Identity tier derivation from `nin_verified` + `dob_evidence_type`
- `INinVerificationProvider` — interface with a **stubbed** implementation. Do not integrate a real provider yet; the licensing question is unresolved
- Registry search with minimum specificity, 25-row cap, rate limiting
- `registry_audit` on every search and sensitive read
- `consent_records` + guardian-consent gate for `is_minor`
- Photo upload and read via presigned URLs; private bucket only
- `player_org_links`, `player_team_history`
- `player_eligibility_flags`
- `registry_ledger` with hash chaining; `GET /registry/ledger/verify`
- Merge **proposal queue**: orgs propose, platform admins approve
- `POST /registry/players/{id}/anonymise` for NDPA erasure
- `GET /registry/metrics`
- **Then** roster entries, created from `playerId` only, with jersey uniqueness

**Acceptance criteria:**
- [ ] A plaintext NIN appears in **no** log output. Assert by submitting one and grepping the captured log sink
- [ ] No code path puts a NIN in a URL, a query string, or `registry_audit.query_terms`
- [ ] Two players registered with the same NIN collide and return `PLAYER_ALREADY_REGISTERED` with the existing `playerId`
- [ ] A player can be fully registered and rostered **without** a NIN, at tier 0
- [ ] `identity_tier` cannot be set through any endpoint; it moves only when evidence changes
- [ ] Search with `lastName: "A"` and no DOB returns `SEARCH_TOO_BROAD`
- [ ] Search never returns more than 25 rows and offers no page 2
- [ ] Every search writes exactly one audit row with acting user, org, and IP
- [ ] Registering a 10-year-old **without** guardian consent fails
- [ ] Photo URLs are presigned and expire; the raw object key never reaches a client
- [ ] `POST /competition-teams/{id}/roster` rejects a body containing a name instead of a `playerId`
- [ ] An org admin calling merge-approve gets `403`; only platform admin executes
- [ ] Merging B into A repoints every roster entry, unions org links, carries the higher tier
- [ ] Merging B into A, then A into C, leaves no two-hop lookups anywhere
- [ ] Append 1,000 ledger entries, tamper with one, and `verify` reports the exact divergence point
- [ ] Anonymising clears identity fields but leaves `player_id` and all statlines intact
- [ ] Architecture test: exactly seven named entities are unfiltered; an eighth fails the build

**Build the duplicate-prevention UX, not just the endpoint.** Returning fuzzy candidates is the easy half. The flow only works if the candidate list shows **photo and date of birth**, so an organiser at a registration desk can actually decide. An endpoint returning three similar names and no other signal will produce duplicates rather than prevent them.

---

### Phase 3 — Fixtures

**Goal:** games exist, are scheduled, and reach `RosterLocked`.

**Build:**
- `games` table and the state machine through `RosterLocked`
- Single-fixture creation and `PATCH` for rescheduling
- Round-robin generation (single and double)
- Knockout bracket generation with seeding
- Game officials assignment
- `game_roster_entries` snapshot written at roster lock
- Rule set snapshot frozen onto the game at roster lock

**Acceptance criteria:**
- [ ] Round-robin generation for 8 teams produces 28 fixtures, each pairing once
- [ ] Double round-robin produces 56, with home and away correctly alternated
- [ ] A team cannot be scheduled against itself
- [ ] Roster lock fails when a team has fewer than `minRosterSize` active players
- [ ] Roster lock fails unless exactly `playersOnCourt` starters are marked per team
- [ ] After roster lock, changing a player's jersey number in `roster_entries` does **not** change `game_roster_entries`
- [ ] After roster lock, editing the competition's rule set does **not** change `rule_set_snapshot`

That last pair of criteria is the whole point of Phase 3. Verify them explicitly.

---

### Phase 4 — Event Recording

**The most important phase. The projector written here is the product.**

**Build:**
- `game_events` table with all indexes from §5.5
- The validation pipeline: `IEventRule` implementations for every code in §10, tiered
- Override mechanism with reason header and audit
- `POST /games/{id}/events` with idempotency on `eventId` and conflict detection on `lastKnownSequence`
- Batch submission for free-throw sequences and substitution groups
- Void and undo-last
- `CourtGeometry.Classify()` with the FIBA landmarks from §8
- **`IGameProjector`** — pure, deterministic, full-replay
- `LiveGameState`, computed from the log, cached on `(gameId, lastSequence)`
- Game-scoped access tokens
- State machine through `PendingReview`

**Acceptance criteria:**
- [ ] Submitting the same `eventId` twice produces one row and two byte-identical responses
- [ ] A stale `lastKnownSequence` returns `409` with the missing events attached
- [ ] Every rule code in §10 has a passing positive and negative test
- [ ] A tapped three-pointer at coordinates inside the arc is rejected with `SHOT_ZONE_MISMATCH`
- [ ] A player with 5 personal fouls cannot record further events
- [ ] Voiding an event removes its contribution from the projection and leaves the row in place
- [ ] `undo-last` after a void skips the already-voided event
- [ ] **Golden-file suite:** at least 3 complete games' event logs project to exactly the expected statlines
- [ ] Projecting the same log twice produces identical output (determinism test)
- [ ] The projector compiles with no reference to `DbContext`, `DateTime.Now`, or `Random`

**Golden files.** Build these by hand from a real box score if you can get one. Store as `tests/Hoops.UnitTests/GoldenFiles/{game-name}/events.json` and `expected.json`. When the projector changes, regenerate and **diff the expected output by eye** — a regenerated golden file that nobody reads is not a test.

---

### Phase 5 — Statistics Persistence

**Goal:** finalised games produce durable statlines and leaderboards.

**Build:**
- `player_game_statlines`, `team_game_statlines`, `game_period_states`
- `lineup_stints` derivation from substitution events, with plus/minus
- `outbox_messages` + `BackgroundService` drainer
- `competition_player_aggregates` with qualification recomputation across the whole competition
- `player_career_aggregates`
- `competition_standings` with the configured tiebreakers
- `POST /admin/recompute/*`
- Finalise, reopen/amend, forfeit transitions

**Acceptance criteria:**
- [ ] Finalising a game writes statlines matching the golden-file expectations exactly
- [ ] **Truncate every projection table, run recompute, and the API returns identical results.** This is the load-bearing test of the entire architecture
- [ ] Finalising a game changes `is_qualified` for players in *other* games in that competition when the threshold is crossed
- [ ] Plus/minus for the two teams in a game sums to zero
- [ ] Team statline points equal the sum of its players' points
- [ ] Sum of `seconds_played` for a team equals `playersOnCourt × total game seconds`
- [ ] Reopening a finalised game invalidates and correctly recomputes affected aggregates
- [ ] Standings tiebreakers resolve a manufactured three-way tie in the configured order

Those arithmetic invariants (plus/minus sums to zero, minutes reconcile, team points equal player points) are worth writing as assertions inside the projector itself, not just as tests. They catch a whole class of bugs at the moment of creation.

---

### Phase 6 — Query Surface

**Goal:** the historical data the product is sold on.

**Build:**
- Box score, play-by-play, shot chart, lineups (per game)
- `GET /competitions/{id}/leaders?stat=&per=` and `/leaders/all`
- Standings
- Player career page: totals plus per-competition breakdown
- Player shot chart across a competition or career
- `GET /organisations/{orgId}/records` — all-time single-game and career records
- Index tuning against realistic volumes

**Acceptance criteria:**
- [ ] `/leaders/all` returns every headline stat in one round trip in under 200 ms with 200 finalised games seeded
- [ ] Per-game leaderboards exclude unqualified players; totals leaderboards include everyone
- [ ] A player who transferred mid-competition appears once, with combined totals
- [ ] A merged player's career page includes statistics from both original records
- [ ] Shot chart aggregation by zone matches a manual count from the event log
- [ ] `EXPLAIN ANALYZE` on every leaderboard query shows an index scan, not a sequential scan

---

### Phase 7 — Hardening

**Build:**
- Rate limiting on `POST /events` (generous — a fast official taps often)
- OpenTelemetry traces on the event write path
- Serilog structured logging with `gameId` and `sequence` on every recording log line
- Load test: 8 concurrent games, realistic event cadence
- Backup and restore rehearsal against a populated database
- Deployment pipeline and migration strategy

**Acceptance criteria:**
- [ ] 8 concurrent games sustain event submission with p99 under 300 ms
- [ ] A restored backup passes the full-recompute equality test
- [ ] Every recording error log line carries enough context to reconstruct what the official tapped

---

## 2. Prompts for Claude Code

Give one phase at a time. Do not paste the whole roadmap and ask for everything.

**Phase 1**
> This is the platform repository. Read `docs/platform-architecture.md` and `CLAUDE.md`. Implement Phase 1: SharedKernel primitives, AppDbContext with snake_case conventions and tenant query filters, the Identity module, JWT auth endpoints, ProblemDetails middleware, and the first migration. Stop when every Phase 1 acceptance criterion passes and show me `dotnet test` output.

**Phase 2**
> Implement Phase 2 (competition management only — no rosters, no players yet). Write the tenancy isolation integration test before the endpoints, not after.

**Phase 2B**
> Implement Phase 2B, the player registry. Read ADR-003 and ADR-008 first. Build `INinHasher` and the Serilog redaction policy BEFORE any player endpoint exists, and write the "no NIN in logs" test before the code that could violate it. Registry entities are deliberately not tenant-scoped — add them to the architecture test's explicit whitelist rather than loosening the test.

**Phase 3**
> Implement Phase 3. The two acceptance criteria about snapshots — jersey changes and rule set edits not affecting locked games — are the point of this phase. Write those tests first.

**Phase 4**
> Implement Phase 4. Build `IGameProjector` first, as a pure function, with the golden-file test suite, before any endpoint exists. Then build the validation pipeline. Then the endpoints. Do not build the endpoints first.

**Phase 5**
> Implement Phase 5. Start with the truncate-and-recompute equality test — write it before the aggregates exist, watch it fail, then make it pass.

**Phase 6**
> Implement Phase 6. After each endpoint, run `EXPLAIN ANALYZE` on its primary query against the seeded dataset and show me the plan.

---

## 3. Seed Data

Build this during Phase 2 and extend it each phase. It pays for itself immediately.

```
tools/Hoops.Seeder/
  → 2 organisations ("Anambra BBA", "Lagos Schools League") — proves registry sharing
  → 1 season, 1 tournament each, 8 teams, 12 players each
  → 6 players deliberately appearing in BOTH orgs, to exercise shared career totals
  → a mix of identity tiers: some NIN-verified, some documented, some tier 0
  → at least 4 minors with guardian consent records
  → 3 fully recorded games with realistic event logs
  → 1 deliberately duplicated player, for testing merge
  → 1 game with an overridden validation rule, for testing the review screen
```

Run it with `dotnet run --project tools/Hoops.Seeder -- --reset`. The client teams will need it as much as you do.

---

## 4. What "Done" Looks Like

The backend is finished when all of these are true simultaneously:

1. `dotnet test` is green, including the architecture and tenancy suites.
2. Truncating every projection table and running recompute yields byte-identical API responses.
3. A complete 4-period game can be recorded through the API, finalised, and appears correctly on the tournament leaderboard.
4. A player who played for two different teams across two competitions **run by different organisations** shows correct combined career totals.
6. A NIN submitted through any endpoint appears nowhere in logs, URLs, or the audit table.
7. A registry search cannot be used to enumerate the database.
5. No endpoint can return another organisation's data under any input.

Criterion 2 is the one that proves the architecture. If it holds, every historical question the product promises to answer is answerable, including the ones nobody has thought of yet.
