# Nigerian Basketball Platform — Backend & Database Architecture

**Target:** One .NET Web API + PostgreSQL serving three products: the Player Registry, the Management app, and the Statistics app.
**Audience:** Claude Code (implementation), plus human reviewers.

---

## 0. How to use this document

This specifies *what* to build and *why*, so downstream implementation choices stay consistent with design intent. Sections 1–3 are context and non-negotiable decisions. Sections 4–11 are the concrete specification. Section 15 is the build order.

Where a decision here looks like over-engineering in isolation, it is usually protecting the product's core value proposition — recomputable history. If a shortcut conflicts with this document, raise the conflict rather than silently diverging.

---

## 1. Product Context

| App | Purpose | Primary users |
|---|---|---|
| **Management app** | Organisations, seasons, competitions, teams, rosters, venues, fixtures | League/tournament administrators |
| **Statistics app** | Record live game events at the scorer's table | Table officials, statisticians |

**The core promise is historical data.** Users must be able to ask: *"Who led this tournament in scoring? In rebounds? What is this player's career three-point percentage across every competition we have ever run?"*

### Three products, one backend

| Product | Users | What it does |
|---|---|---|
| **Player Registry** | Registration desks, federation and league staff | The canonical record of basketball players in Nigeria. Its own website, its own module, the same API |
| **Tournament Management** | League and tournament administrators | Organisations, seasons, competitions, teams, rosters, fixtures |
| **Statistics Recording** | Table officials, statisticians | Live game event capture at the scorer's table |

All three share one database and one API. They are separate *products* with separate front ends, not separate systems. The registry needs the same transactional integrity as the rosters that reference it; the statistics app writes events that belong to tournaments in this same schema. Splitting any of them into its own service would buy network hops and distributed transactions in exchange for nothing.

**NBBF is an organisation on this platform**, running its competitions like any other client. It is not a separate tenant class and not an external consumer.

### The registry is the moat

Rosters are built from `playerId`, always — never from a typed name. Records are created **only through roster registration**, so every player in the database traces to a real appearance in a real competition. That constraint is the product: a registry anyone can write to is a mailing list, while one where every record has competitive provenance is something no individual league can reproduce.

Three consequences reach into every other decision here:

1. **`players` sits outside the tenant boundary** (ADR-003). It is the one deliberate exception to ADR-005.
2. **Regulated personal data is a first-class concern** (ADR-008, §5A). NIN, date of birth, and photographs of what will often be minors are not ordinary application fields.
3. **Career statistics span organisations**, because identity does. A player appearing for two clubs under two different organisers has one career, not two.

### Confirmed constraints

1. **Connectivity is reliable at the scorer's table.** Online-first. **No offline sync engine in v1.** The write path is nonetheless designed so offline can be added later without redesign (§12.1).
2. **Full FIBA granularity.** Shot coordinates and on-court lineup tracking are in scope, enabling shot charts, plus/minus, and lineup analytics.
3. **No live consumers in v1.** SignalR is deferred but architecturally provisioned (§12.2).

---

## 2. Architectural Decisions

These are load-bearing. Do not change them without revisiting this document.

### ADR-001 — The event log is the source of truth

Games are recorded as an **append-only, ordered log of discrete events**. Box scores, statlines, leaderboards, and career totals are **derived projections**, never authored directly.

**Why:**
- Any statistic invented later is recomputable from history already captured. Aggregate-only entry permanently forecloses this, which would undercut the product's entire premise.
- Disputes are resolvable: every number traces to a timestamped event with a named author.
- Corrections become non-destructive, which is what makes "undo" safe at the scorer's table.

**Implication:** there must be a **pure, deterministic projector**: `IReadOnlyList<GameEvent> -> GameProjection`. Live state is an optimisation; full replay is the correctness guarantee. Divergence between the two is always a bug.

### ADR-002 — Assists, steals, and blocks are attributes, not events

An assist is `secondaryRosterEntryId` on `FIELD_GOAL_MADE`. A steal is the secondary player on `TURNOVER`. A block is the secondary player on `FIELD_GOAL_MISSED`.

**Why:** it becomes structurally impossible to record an orphaned assist with no corresponding basket, or a steal with no turnover. Those invariants would otherwise need fragile cross-event validation. The cost — assist queries are a filter rather than a type lookup — is trivial with the right partial index.

### ADR-003 — `Player` is a platform-level entity, not a tenant-owned one

`Player` is a national record of a human being. It has no `organisation_id`, is not covered by the tenant query filter, and is shared by every organisation on the platform. `RosterEntry` — a player's participation in one team's squad for one competition — remains tenant-owned.

**Why:** the career query is only as good as identity resolution. Players change teams, jersey numbers, and organisations. If each organiser held its own copy, the platform would hold fragments of careers rather than careers.

**This is the exception to ADR-005, and it must be a loud one.** Every other entity crosses tenants only through a bug; `players` is designed to cross. That inversion needs its own access model:

| Operation | Who |
|---|---|
| **Search** the registry | Any authenticated org member — subject to minimum specificity, rate limit, and audit (§5A.4) |
| **Read** the limited projection | Any authenticated org member |
| **Read** sensitive fields (NIN status, guardian contact) | Platform admin, or the registering org before verification |
| **Create** a player | `CompetitionManager` or above, during roster registration |
| **Edit** core identity | Registering org **while unverified**; platform admin thereafter |
| **Merge** two players | **Platform admin only** — never an org admin |
| **Verify** identity | Platform admin, via a licensed provider |

`Player` must **not** implement `ITenantScoped`, and the architecture test in §13 must whitelist the registry entities **explicitly by name** rather than silently skipping unfiltered types. An exception invisible in code is indistinguishable from a bug.

*Player identity has a second decision attached: see **ADR-008** on NIN handling.*

### ADR-004 — Modular monolith, not microservices

One deployable API with hard internal module boundaries. Modules communicate through published contracts and in-process domain events, never by reaching into each other's tables.

**Why:** consistency requirements between recording and statistics are strong, the team is small, and the boundaries are not yet proven. Modules can be extracted later. Starting distributed buys operational cost with no present benefit.

### ADR-005 — Single database, discriminator-column multi-tenancy

Tenant-owned tables carry `organisation_id`, enforced by EF Core global query filters plus `NOT NULL` foreign keys.

**Why:** schema-per-tenant complicates migrations badly at the expected tenant count. The risk is cross-tenant leakage from a forgotten filter, mitigated by an architecture test that fails the build if any tenant-scoped entity lacks a filter (§13).

### ADR-006 — Finalisation is an explicit, gated transition

`Scheduled → RosterLocked → InProgress → PendingReview → Finalized`. Only `Finalized` games contribute to leaderboards and career totals.

**Why:** a game in progress has meaningless intermediate state. Leaderboards computed from live data flicker and mislead. The review gate is also where a statistician catches the misattributed rebound before it becomes permanent record.

### ADR-007 — Online-first, but idempotent by construction

No local queue, no conflict resolution, no vector clocks in v1. But every event carries a **client-generated ID** used as an idempotency key, and submission is **safely retryable**.

**Why:** this is nearly free and it eliminates the worst failure mode of a connected system — a request that times out after the server committed it, leaving the official unsure whether to re-tap. It also happens to be the foundation offline support would need.

---

### ADR-008 — The NIN is an identity anchor, never a stored secret and never a key

The National Identification Number answers *"is this the same human?"* and nothing else.

- **`player_id` (UUID v7) is the only key.** The NIN never appears in a primary key, foreign key, URL, log line, or any index that is not a blind index.
- **The plaintext NIN is not stored.** Persist `nin_hmac` — HMAC-SHA256 over the normalised NIN with a server-side pepper held in a secrets manager, never in the database or configuration. A unique index on `nin_hmac` gives exact-match de-duplication without holding the value.
- **Verification is delegated.** Under the NIMC Act, NIN verification runs through licensed channels. Call a licensed provider, store only `{ verified, verified_at, provider, provider_reference }`, and discard the NIN from memory immediately.
- **NIN is optional.** Many players — particularly minors — will not have one to hand at a registration desk. **Every feature must work without it**, degrading to a lower confidence tier (§5A.2).

**Why:** it is nearly free, and it removes the platform from the category of systems worth attacking for identity data. Storing a national ID number in plaintext to solve a de-duplication problem means paying a permanent, unbounded liability for a lookup. If a genuine need for plaintext later emerges, envelope encryption can be added then; unwinding a plaintext store after a breach cannot.

**This is an architectural recommendation, not legal advice.** The NDPA 2023 and the NIMC Act both apply. Engage Nigerian counsel before this handles real data.

## 3. Technology Stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS) | Use current LTS if newer. Latest C# language version. |
| API style | **MVC Controllers** with `[ApiController]` | One controller folder per module (§4). Attribute routing, attribute-based policies. |
| ORM | EF Core 10 | Code-first migrations. |
| Database | PostgreSQL 16+ | JSONB, generated columns, partial indexes, `citext`. |
| IDs | UUID v7 | Time-sortable; avoids the index fragmentation of v4. |
| Request validation | FluentValidation | Shape only. Domain rules live in the domain. |
| Auth | JWT bearer + refresh tokens | ASP.NET Core Identity for the user store. |
| Mapping | Hand-written `ToDto()` extensions | No AutoMapper. |
| Testing | xUnit, FluentAssertions, Testcontainers | Real Postgres in integration tests. |
| Observability | OpenTelemetry + Serilog | Structured logs; traces on the event-write path. |
| API docs | **Swashbuckle.AspNetCore** + Swagger UI | XML doc comments enabled (`<GenerateDocumentationFile>`). `[ProducesResponseType]` on every action. |

**Deliberately not used:** MediatR (call handlers directly), AutoMapper, a separate read database. Introduce them only against a concrete, observed pain.

---

## 4. Solution Structure

```
BasketballStats.sln
├── src/
│   ├── Hoops.Api/                        # Host: DI, auth, middleware, endpoint registration
│   │   ├── Program.cs
│   │   ├── Controllers/                  # One folder PER MODULE — never a flat dump
│   │   │   ├── Identity/
│   │   │   ├── Registry/
│   │   │   ├── Competitions/
│   │   │   ├── GameRecording/
│   │   │   └── Statistics/
│   │   ├── Auth/                         # JWT config, policies, requirement handlers
│   │   └── Middleware/                   # Exception → ProblemDetails, tenant resolution
│   │
│   ├── Hoops.Modules.Identity/           # Users, organisations, memberships, roles
│   ├── Hoops.Modules.Registry/           # Players, NIN hashing, identity tiers, consent,
│   │                                     #   search, merge, eligibility, provenance ledger
│   ├── Hoops.Modules.Competitions/       # Seasons, competitions, stages, teams, rosters,
│   │                                     #   venues, fixtures, standings
│   ├── Hoops.Modules.GameRecording/      # State machine, event log, validation, live state
│   ├── Hoops.Modules.Statistics/         # Projector, statlines, aggregates, leaderboards,
│   │                                     #   shot charts, lineup analytics
│   │
│   ├── Hoops.SharedKernel/               # Result<T>, DomainEvent, GameClock, CourtGeometry,
│   │                                     #   Guard, strongly-typed IDs
│   └── Hoops.Infrastructure/             # AppDbContext, EF configs, migrations, outbox
└── tests/
    ├── Hoops.UnitTests/                  # Domain rules, projector golden files
    ├── Hoops.IntegrationTests/           # Testcontainers Postgres, full API
    └── Hoops.ArchitectureTests/          # Module boundaries, tenant filter coverage
```

**Each module's internal layout:**

```
Hoops.Modules.X/
├── Domain/          # Entities, value objects, invariants. No EF attributes.
├── Application/     # Use-case services. Orchestration, transactions.
├── Contracts/       # DTOs + public interfaces. THE ONLY namespace other modules may reference.
└── ModuleExtensions.cs
```

**Boundary rule:** a module may reference another module's `Contracts` namespace and nothing else. Enforced by architecture test.

### 4.1 Controller conventions

**MVC dependencies stay out of the module class libraries.** Controllers live only in `Hoops.Api`, one folder per module. Modules expose use-case services through their `Application` layer; controllers call them and map the result. This keeps module projects referencing nothing but `Hoops.SharedKernel`, which is what makes them extractable later.

Every controller:

- Carries `[ApiController]` and `[Route("api/v1/[controller]")]`
- Declares an **explicit `[Authorize(Policy = ...)]` on every action** — never inherited silently from the class, never absent. `[AllowAnonymous]` must be written out where intended
- Declares `[ProducesResponseType]` for each status code it returns, typed
- Has XML doc comments on every public action; they become the Swagger description
- Returns `ActionResult<T>`, mapping `Result<T>` failures to `ProblemDetails` through a shared base-controller helper
- Is thin: parse, call one application service, map the response. No business logic, no `DbContext`

**Architecture test:** enumerate every public action method across all controllers and fail the build if any lacks an explicit `[Authorize]` or `[AllowAnonymous]`. With a national player registry and a seven-policy authorisation matrix, a silently unprotected endpoint is the highest-consequence bug in the system, and it is exactly the kind that survives code review. Make the compiler catch it instead.

`[ApiController]` also produces RFC 9457 `ProblemDetails` automatically on model-binding failure, which satisfies the error contract in §13 without extra work — configure `ProblemDetailsFactory` once so the shape matches your handwritten errors.

---

## 5. Domain Model & Database Schema

Conventions: `snake_case` identifiers, `uuid` (v7) primary keys, `timestamptz` stored in UTC, soft delete via nullable `deleted_at` on user-facing entities only. Every table has `created_at`, `updated_at`, `created_by_user_id` unless noted.

### 5.1 Identity & tenancy

```
organisations
  id                uuid PK
  name              text NOT NULL
  slug              citext NOT NULL UNIQUE
  country_code      char(2)
  default_timezone  text NOT NULL DEFAULT 'UTC'   -- IANA, e.g. 'Africa/Lagos'
  logo_url          text
  settings          jsonb NOT NULL DEFAULT '{}'
  deleted_at        timestamptz

users
  id                uuid PK
  email             citext NOT NULL UNIQUE
  password_hash     text NOT NULL
  full_name         text NOT NULL
  is_system_admin   boolean NOT NULL DEFAULT false
  email_confirmed   boolean NOT NULL DEFAULT false
  last_login_at     timestamptz

organisation_memberships
  id                uuid PK
  organisation_id   uuid FK NOT NULL
  user_id           uuid FK NOT NULL
  role              text NOT NULL   -- Owner|Admin|CompetitionManager|Statistician|Viewer
  invited_at        timestamptz
  accepted_at       timestamptz
  UNIQUE (organisation_id, user_id)

refresh_tokens
  id, user_id, token_hash, expires_at, revoked_at, replaced_by_token_id
```

### 5.2 Competition structure

```
seasons
  id                uuid PK
  organisation_id   uuid FK NOT NULL
  name              text NOT NULL          -- "2025/26"
  starts_on         date NOT NULL
  ends_on           date NOT NULL
  is_active         boolean NOT NULL DEFAULT true
  UNIQUE (organisation_id, name)

competitions
  id                uuid PK
  organisation_id   uuid FK NOT NULL
  season_id         uuid FK NOT NULL
  name              text NOT NULL
  slug              citext NOT NULL
  format            text NOT NULL     -- League|Knockout|GroupsThenKnockout|RoundRobin
  category          text              -- "U18 Boys", "Senior Women" — free text
  rule_set          jsonb NOT NULL    -- §5.3
  status            text NOT NULL     -- Draft|Registration|InProgress|Completed|Archived
  starts_on         date
  ends_on           date
  timezone          text NOT NULL
  UNIQUE (organisation_id, season_id, slug)

stages                                -- group stage, quarter-finals, etc.
  id                uuid PK
  competition_id    uuid FK NOT NULL
  name              text NOT NULL     -- "Group Stage", "Semi-Finals"
  stage_type        text NOT NULL     -- GroupStage|Knockout|Placement
  sequence          int NOT NULL
  UNIQUE (competition_id, sequence)

groups                                -- pools within a group stage
  id                uuid PK
  stage_id          uuid FK NOT NULL
  name              text NOT NULL     -- "Group A"

teams                                 -- canonical club, reusable across competitions
  id                uuid PK
  organisation_id   uuid FK NOT NULL
  name              text NOT NULL
  short_name        text NOT NULL     -- ≤12 chars, for scoreboards
  abbreviation      char(3)           -- "LAG"
  logo_url          text
  primary_colour    text              -- hex; the recording UI uses these for jersey buttons
  secondary_colour  text
  home_venue_id     uuid FK NULL
  deleted_at        timestamptz

competition_teams                     -- a team's entry into one competition
  id                uuid PK
  competition_id    uuid FK NOT NULL
  team_id           uuid FK NOT NULL
  group_id          uuid FK NULL
  display_name      text NULL         -- override, e.g. sponsored name for this event
  seed              int NULL
  status            text NOT NULL     -- Registered|Confirmed|Withdrawn|Disqualified
  UNIQUE (competition_id, team_id)

-- ══════════════════════════════════════════════════════════════════════════
-- REGISTRY — NOT tenant-scoped. No organisation_id. No global query filter.
-- ══════════════════════════════════════════════════════════════════════════

players                               -- canonical human being
  id                     uuid PK      -- THE playerId. Permanent. Public. The only key.
  first_name             text NOT NULL
  last_name              text NOT NULL
  middle_name            text NULL
  known_as               text NULL
  date_of_birth          date NOT NULL     -- required: drives age-group eligibility
  gender                 text NOT NULL     -- Male|Female (division eligibility)
  nationality            char(2) NOT NULL DEFAULT 'NG'
  state_of_origin        text NULL
  height_cm              int NULL
  dominant_hand          text NULL         -- Left|Right
  photo_object_key       text NULL         -- private bucket key, NEVER a public URL

  nin_hmac               bytea NULL        -- HMAC-SHA256(normalised NIN, server pepper)
  nin_verified           boolean NOT NULL DEFAULT false
  nin_verified_at        timestamptz NULL
  nin_verification_ref   text NULL
  nin_verification_provider text NULL

  identity_tier          smallint NOT NULL DEFAULT 0   -- DERIVED, never settable. §5A.2
  dob_evidence_type      text NULL         -- BirthCertificate|SchoolRecord|Passport|NIN|None
  dob_verified_at        timestamptz NULL

  is_minor               boolean GENERATED ALWAYS AS
                           (date_of_birth > (CURRENT_DATE - INTERVAL '18 years')) STORED
  guardian_name          text NULL
  guardian_phone         text NULL
  guardian_consent_id    uuid FK NULL

  registered_by_organisation_id uuid FK NOT NULL   -- provenance, NOT ownership
  registered_by_user_id  uuid FK NOT NULL
  status                 text NOT NULL DEFAULT 'Active'  -- Active|Suspended|Retired|Deceased
  merged_into_id         uuid FK NULL
  anonymised_at          timestamptz NULL  -- NDPA erasure; §13
  deleted_at             timestamptz

  UNIQUE (nin_hmac) WHERE nin_hmac IS NOT NULL AND merged_into_id IS NULL
  INDEX ON (lower(last_name), date_of_birth)
  INDEX ON (lower(last_name) gin_trgm_ops)      -- pg_trgm, fuzzy match
  INDEX ON (registered_by_organisation_id)

player_org_links          -- which orgs have engaged this player. Provenance + audit.
  id, player_id, organisation_id, first_linked_at, last_linked_at
  UNIQUE (player_id, organisation_id)

consent_records           -- NDPA lawful basis, per player
  id                uuid PK
  player_id         uuid FK NOT NULL
  consent_type      text NOT NULL     -- PlayerSelf|Guardian
  scope_version     text NOT NULL     -- which wording was agreed, e.g. 'v1-2026-08'
  granted_at        timestamptz NOT NULL
  withdrawn_at      timestamptz NULL
  evidence_object_key text NULL       -- scanned signed form
  captured_by_user_id uuid FK NOT NULL

player_eligibility_flags  -- suspensions follow the PLAYER, across organisations
  id, player_id, flag_type, scope, organisation_id NULL, competition_id NULL,
  reason, starts_on, ends_on NULL, raised_by_user_id, resolved_at NULL
  -- flag_type: Suspension|AgeDispute|IdentityDispute|MedicalHold
  -- scope:     Platform|Organisation|Competition
  INDEX ON (player_id) WHERE resolved_at IS NULL

registry_audit            -- EVERY search and sensitive read. Non-negotiable (§5A.4).
  id, actor_user_id, organisation_id, action, query_terms jsonb, player_id NULL,
  result_count, ip_address inet, occurred_at
  -- action: Search|ViewSensitive|Create|Edit|Merge|VerifyNin|Anonymise
  -- query_terms MUST NEVER contain a raw NIN

registry_ledger           -- tamper-evident identity history. §5A.6
  sequence          bigserial PK
  entry_type        text NOT NULL     -- Registered|DobEvidence|NinVerified|TierChanged|
                                      --   Merged|FlagRaised|Anonymised
  player_id         uuid NULL
  payload_hash      bytea NOT NULL    -- SHA-256 of the canonical entry payload
  previous_hash     bytea NOT NULL
  entry_hash        bytea NOT NULL    -- SHA-256(previous_hash || payload_hash || occurred_at)
  occurred_at       timestamptz NOT NULL
  UNIQUE (entry_hash)

-- ══════════════════════════════════════════════════════════════════════════
-- TENANT-SCOPED again from here
-- ══════════════════════════════════════════════════════════════════════════
-- ══════════════════════════════════════════════════════════════════════════

roster_entries                        -- player + competition_team + jersey
  id                  uuid PK
  competition_team_id uuid FK NOT NULL
  player_id           uuid FK NOT NULL  -- → players.id. Resolved from the registry, never typed.
  verified_tier       smallint NOT NULL -- identity_tier at the moment of registration
  jersey_number       text NOT NULL   -- TEXT: "00" and "0" are distinct in FIBA
  position            text NULL       -- PG|SG|SF|PF|C
  is_captain          boolean NOT NULL DEFAULT false
  status              text NOT NULL   -- Active|Injured|Suspended|Removed
  registered_at       timestamptz NOT NULL
  UNIQUE (competition_team_id, jersey_number) WHERE status <> 'Removed'
  UNIQUE (competition_team_id, player_id)

team_staff                            -- coaches; required for bench/coach technical fouls
  id, competition_team_id, full_name, role  -- HeadCoach|AssistantCoach|Manager

venues
  id                uuid PK
  organisation_id   uuid FK NOT NULL
  name              text NOT NULL
  address           text
  city              text
  court_count       int NOT NULL DEFAULT 1
  timezone          text NOT NULL
```

---

## 5A. The Player Registry

### 5A.1 Roster creation is search-first

**A roster entry can never be created from free text.** It is always created against a `player_id` resolved from the registry. The flow is fixed:

```
Organiser adds a player to a roster
        ↓
  Search registry  ──►  exact NIN match?  ──yes──►  link, done
        │                     │no
        │                     ▼
        │            strong match? (surname + DOB, or fuzzy name + DOB ±1yr)
        │                     │
        │        ┌────────────┴────────────┐
        │       yes                        no
        │        ▼                          ▼
        │  present candidates          "No match found"
        │  with photo + DOB                 │
        │  "Is this the same player?"       │
        │        │                          │
        │   ┌────┴────┐                     │
        │  yes        no ────────────────►  ▼
        │   │                        CREATE new player
        │   ▼                        (mandatory: name, DOB, gender)
        │  link                      (optional: NIN, photo)
        │                            (if minor: guardian consent REQUIRED)
        └──────────────────────────────────►  roster_entry created
```

The candidate step is where duplicates are actually prevented, and it must show **photo and date of birth** — "Chukwu, Emeka, 2004-03-11" beside a face is decidable at a registration desk; a list of similar names is not.

Both linking and creating write a `player_org_links` row and a `registry_audit` row. That is how *"when did this player play for a different team"* is answerable at the registry level, independently of roster history.

**Records are created only through this flow.** There is no public self-signup and no bulk import, so every player in the database traces to a real appearance in a real competition.

### 5A.2 Identity confidence tiers

Not every record deserves equal trust, and pretending otherwise is how age fraud enters a database and never leaves.

| Tier | Name | Evidence | Permitted use |
|---|---|---|---|
| **0** | `Asserted` | Organiser typed a name and DOB | Open-age competitions only |
| **1** | `Documented` | Birth certificate, school record, or passport reviewed | Age-group competitions |
| **2** | `NinVerified` | NIN verified through a licensed provider | Everything, including national selection |

`identity_tier` is **derived, never settable** — a function of `nin_verified` and `dob_evidence_type`, recomputed whenever either changes. Any endpoint accepting a tier as input is a bug.

A competition's rule set may specify `minimumIdentityTier`; roster lock then fails for players below it, naming them specifically.

**This is arguably the more valuable product.** Age fraud in Nigerian youth basketball is endemic and expensive. A platform that can answer *"what evidence backs this player's stated date of birth, and when was it recorded?"* is solving a problem federations and leagues actually lose sleep over — a stronger adoption driver than leaderboards.

#### NIN verification

`INinVerificationProvider` is an interface with a stubbed implementation until licensing is settled. Under the NIMC Act, verification runs through licensed channels; do not build a direct integration assuming access will be granted.

If the provider returns demographic data, use it to **cross-check, never overwrite**. A mismatch between the submitted DOB and the NIMC record raises an `AgeDispute` flag for human review. Silent correction would destroy the evidence trail that makes tier 2 mean anything.

### 5A.3 Photographs

Stored in S3-compatible object storage, **never in the database and never at a public URL**. The table holds `photo_object_key`; the API issues presigned URLs (≤15 minutes) to authorised callers only.

Photographs of identifiable minors are sensitive data under the NDPA. A permanently public bucket URL is a leak that cannot be recalled once indexed.

### 5A.4 Search safeguards

The registry is a national database of players, many of them children, readable by every organisation you onboard. Bulk enumeration must be structurally impossible, not merely discouraged.

- **Minimum specificity.** Either a NIN, or a surname of ≥3 characters **plus** a DOB or birth year. Bare wildcards rejected with `SEARCH_TOO_BROAD`.
- **Hard cap of 25 rows, no pagination.** If a query is too broad to answer, it is too broad to enumerate.
- **Rate limit** per user and per organisation, tuned to a busy registration desk rather than a scraper.
- **Audit everything.** `query_terms` must never contain a raw NIN.
- **Limited projection by default:** `{ playerId, fullName, dateOfBirth, gender, photoUrl, identityTier }`. Sensitive fields require a separate call, audited as `ViewSensitive`.

### 5A.5 Merge

Merging B into A sets `B.merged_into_id = A.id`, repoints every `roster_entries` row, unions `player_org_links`, carries over the higher `identity_tier`, and enqueues recomputation of A's career aggregates. Resolve `merged_into_id` transitively **on write**, never on read.

**Merge is platform-admin only.** An org admin who merges two players they believe are the same person may be destroying another organisation's history. Orgs *propose* merges with evidence; platform admins execute them. Record proposer, approver, and evidence in `audit_log`.

Expect the common case to be a duplicate created because someone had no NIN at registration and produced one later. Make the queue easy to work through.

### 5A.6 The provenance ledger

Every registration, DOB evidence review, NIN verification, tier change, merge, and eligibility flag appends one hash-chained row:

```
entry_hash = SHA256( previous_hash || payload_hash || occurred_at )
```

**Why this is worth building even though you are not selling the database.** Age and identity disputes are adjudicated *after* the fact, often under pressure from a losing party. A mutable `updated_at` column proves nothing — anyone with database access could have changed it. A hash chain lets you demonstrate that a player's date of birth was recorded on a given date with given evidence, **before** the dispute arose, and has not been altered since. `GET /registry/ledger/verify` recomputes the chain and reports the first divergence.

Keep the payload **minimal and non-personal** — entry type, player ID, and a hash. It is an integrity structure, not a second copy of the database, and must not become a route to reconstruct personal data after an NDPA erasure.

Publishing signed external snapshots is **not** required. Internal tamper-evidence is sufficient for dispute resolution; external notarisation only matters if the data is ever transferred to another controller.

### 5A.7 Consent and lawful basis

Registration of a minor without a linked `consent_records` row must **fail**, not warn. A large share of registrations will be under 18, and the NDPA requires verifiable guardian consent.

Paper capture — an organiser uploads a signed form to `evidence_object_key` — is far more realistic at a tournament registration desk than a digital consent link. Support both; expect paper.

`scope_version` records which wording each player agreed to, so that a later change in how the data is used can be checked per record rather than assumed. Have Nigerian counsel draft the wording before the first record is written; it is the one thing here that cannot be refactored later.

---

## 6. Game State Machine

```
                    ┌──────────────┐
                    │  Scheduled   │
                    └──────┬───────┘
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
   ┌──────────────┐ ┌────────────┐  ┌──────────────┐
   │ RosterLocked │ │ Postponed  │  │  Cancelled   │
   └──────┬───────┘ └─────┬──────┘  └──────────────┘
          │               │ (reschedule)
          │               └──────► Scheduled
          ▼
   ┌──────────────┐        ┌───────────┐
   │  InProgress  ├───────►│ Forfeited │
   └──────┬───────┘        └─────┬─────┘
          ▼                      │
   ┌──────────────┐              │
   │PendingReview │              │
   └──────┬───────┘              │
          ▼                      ▼
   ┌──────────────────────────────────┐
   │            Finalized             │
   └──────────────┬───────────────────┘
                  │ (Admin only, audited, reason required)
                  ▼
           ┌──────────────┐
           │  Amending    │──► PendingReview
           └──────────────┘
```

| Transition | Guard |
|---|---|
| `Scheduled → RosterLocked` | Both teams have ≥ `minRosterSize` active players; exactly `playersOnCourt` starters marked per team; rule set snapshotted; roster snapshot written |
| `RosterLocked → InProgress` | `GAME_START` accepted; `started_at` set |
| `InProgress → PendingReview` | Final `PERIOD_END` recorded; scores not tied unless `allowsTies`; `GAME_END` accepted |
| `PendingReview → Finalized` | Caller is `CompetitionManager` or above; projector runs; statlines persisted; aggregate recomputation enqueued via outbox |
| `Finalized → Amending` | `OrgAdmin`; reason required; affected aggregates invalidated |

**Overtime:** if scores are tied at the end of regulation and `allowsTies` is false, `GAME_END` is rejected and a new period must be started.

---

## 7. Event Catalogue

`event_type` is a string enum; `payload` carries type-specific extras.

### Game flow
| Type | Subtype | Team | Player | Notes |
|---|---|---|---|---|
| `GAME_START` | — | — | — | Reads starting fives from `game_roster_entries` |
| `PERIOD_START` | `Regulation` / `Overtime` | — | — | |
| `PERIOD_END` | — | — | — | Resets team fouls; validates clock at 0 |
| `GAME_END` | — | — | — | |
| `JUMP_BALL` | — | ✔ possession | ✔ | `payload: { opponentRosterEntryId }` |

### Scoring
| Type | Subtype | Player | Secondary | Notes |
|---|---|---|---|---|
| `FIELD_GOAL_MADE` | `TwoPoint` / `ThreePoint` | shooter | **assister** (optional) | Requires coordinates. `points` = 2 or 3 |
| `FIELD_GOAL_MISSED` | `TwoPoint` / `ThreePoint` | shooter | **blocker** (optional) | Requires coordinates |
| `FREE_THROW_MADE` | — | shooter | — | `payload: { attemptNumber, totalAttempts, sourceEventId }` |
| `FREE_THROW_MISSED` | — | shooter | — | Same payload |

Shot type for analytics, in `payload.shotType`: `JumpShot`, `Layup`, `Dunk`, `Hook`, `TipIn`, `FadeAway`, `StepBack`, `PullUp`, `AlleyOop`.

### Rebounds
| Type | Subtype | Player | Notes |
|---|---|---|---|
| `REBOUND` | `Offensive` / `Defensive` | ✔ | Must follow a missed shot or a missed final free throw |
| `TEAM_REBOUND` | `Offensive` / `Defensive` / `DeadBall` | — | Team-only; ball out of bounds off a miss |

### Possession loss
| Type | Player | Secondary | Notes |
|---|---|---|---|
| `TURNOVER` | committer | **stealer** (optional) | Subtypes: `BadPass`, `LostBall`, `Travelling`, `DoubleDribble`, `Carrying`, `OffensiveFoul`, `ThreeSeconds`, `FiveSeconds`, `EightSeconds`, `ShotClockViolation`, `OutOfBounds`, `BackCourt`, `Other` |

### Fouls
| Type | Subtype | Player | Secondary | Notes |
|---|---|---|---|---|
| `FOUL` | `Personal`, `Shooting`, `Offensive`, `Technical`, `Unsportsmanlike`, `Disqualifying`, `BenchTechnical`, `CoachTechnical` | offender (null for bench/coach) | **player fouled** (optional) | `payload: { freeThrowsAwarded, staffId? }` |

Foul accounting:
- `Personal`, `Shooting`, `Offensive`, `Unsportsmanlike`, `Disqualifying` → increment personal **and** team fouls.
- `Technical` on a player → increments personal and team fouls; reaching `technicalFoulLimit` disqualifies.
- `BenchTechnical` / `CoachTechnical` → team fouls only.
- Reaching `personalFoulLimit` marks the player `fouled_out`; the validator then rejects further non-void events for that player.

### Substitutions & stoppages
| Type | Player | Secondary | Notes |
|---|---|---|---|
| `SUBSTITUTION` | player **out** | player **in** | Clock must be stopped. One event per swap |
| `TIMEOUT` | — | — | `payload: { timeoutType: "Team"｜"Media"｜"Official" }` |
| `CLOCK_START` / `CLOCK_STOP` | — | — | Drives `seconds_played` |

### Administrative
| Type | Notes |
|---|---|
| `VOID` | `payload: { targetEventId, reason }`. Sets `is_voided` on the target in the same transaction |
| `NOTE` | Free-text annotation from the table. Ignored by the projector |

---

## 8. Court Geometry & Shot Coordinates

### Canonical frame

All coordinates are stored in one normalised frame so shot charts overlay correctly regardless of basket or period.

- Unit: **centimetres**, integer.
- Court: FIBA 28 m × 15 m.
- Origin `(0, 0)` = centre court. `x ∈ [-1400, 1400]` (length), `y ∈ [-750, 750]` (width).
- **The attacking basket is always at positive x.** The client transforms raw taps into this frame; the server validates bounds and **recomputes `shot_zone` and `shot_distance_cm` itself** — never trust client-derived values.

Landmarks (FIBA):

| Landmark | Value |
|---|---|
| Attacking hoop centre | `(1242.5, 0)` — 1.575 m from baseline |
| Backboard face | `x = 1280` — 1.20 m from baseline |
| Three-point arc radius | 675 cm from hoop centre |
| Corner three line | `｜y｜ = 660` |
| Corner break point | `x = 1242.5 − √(675² − 660²) ≈ 1099` |
| Restricted area radius | 125 cm from hoop centre |
| Paint width | 490 cm → `｜y｜ ≤ 245` |
| Paint depth | 580 cm from baseline → `x ≥ 820` |
| Free-throw line | `x = 820` |

### Zone classification

Pure function in `Hoops.SharedKernel/CourtGeometry.cs`:

```csharp
public static ShotZone Classify(int x, int y, RuleSet rules);
```

| Zone | Condition |
|---|---|
| `RestrictedArea` | distance from hoop ≤ 125 |
| `Paint` | inside key bounds, outside restricted area |
| `MidRange` | outside paint, inside the three-point line |
| `CornerThree` | `｜y｜ > 660` and `x > cornerBreakX` |
| `AboveBreakThree` | distance from hoop > 675, not a corner three |
| `Backcourt` | `x < 0` |

**Consistency check:** if `Classify()` returns a three-point zone but the subtype is `TwoPoint` (or vice versa), reject with `SHOT_ZONE_MISMATCH`. This catches a large class of scorer's-table mis-taps *at the moment they happen*, which is exactly the kind of validation that lets the recording UI be aggressive and one-tap without producing garbage history.

---

## 9. Statistics Derivation

### 9.1 The projector

```csharp
public interface IGameProjector
{
    GameProjection Project(GameContext context, IReadOnlyList<GameEvent> events);
}
```

**Requirements:**
- **Pure.** No I/O, no `DateTime.Now`, no randomness.
- **Deterministic.** Identical input always produces identical output.
- Processes events strictly in `sequence` order, skipping voided ones.
- Produces player statlines, team statlines, lineup stints, period states, running score, and the final `LiveGameState`.

**Two call sites, one implementation:**
1. **Live** — after each accepted event, re-project to produce `LiveGameState` for the recording app.
2. **Finalisation** — full replay from the log; results persisted.

For games of ~400–800 events, full re-projection on every write completes in single-digit milliseconds and eliminates an entire category of incremental-update bugs. **Start there.** Introduce incremental advancement only if profiling demands it, and if you do, add a test asserting incremental output equals full-replay output across the entire golden corpus.

### 9.2 Metric definitions

Pin these down so the implementation does not improvise:

```
Total rebounds        = offensive + defensive
Field goal %          = FGM / FGA                     (null when FGA = 0)
Three-point %         = 3PM / 3PA                     (null when 3PA = 0)
Free throw %          = FTM / FTA                     (null when FTA = 0)
Effective FG%         = (FGM + 0.5 × 3PM) / FGA
True shooting %       = PTS / (2 × (FGA + 0.44 × FTA))
FIBA Efficiency (EFF) = (PTS + REB + AST + STL + BLK + FoulsDrawn)
                      − (MissedFG + MissedFT + TO + BlocksAgainst + FoulsCommitted)
Plus/minus            = (team points − opponent points) while the player is on court
```

Two frequent bug sources, called out explicitly:
- **Three-point attempts are a subset of field goal attempts, not additive.** A made three increments FGM, FGA, 3PM, 3PA, and adds 3 to points.
- **Percentages are null, not zero, when the denominator is zero.** A player who took no threes has no three-point percentage; rendering `0%` is wrong and will show up in leaderboards.

**Minutes played** is derived from `CLOCK_START` / `CLOCK_STOP` and `SUBSTITUTION` events, accumulating only spans where the clock was running *and* the player was on court. Store `seconds_played`; format on display.

### 9.3 Leaderboards

Refreshed on finalisation:

1. Game finalises → domain event `GameFinalized` → **outbox row written in the same transaction**.
2. A `BackgroundService` drains the outbox:
   - Upsert `competition_player_aggregates` for every player in the game.
   - Recompute `is_qualified` for **every** player in the competition — the games-played threshold shifts as the competition progresses, so a finalised game changes other players' qualification status.
   - Upsert `player_career_aggregates`.
   - Recompute `competition_standings` for the affected group.

Leaderboard endpoints read the aggregate tables via the partial indexes in §5.6.

A **"recompute everything"** admin command must exist and be safe to run at any time. It is simultaneously the disaster-recovery path, the correctness audit, and the thing that makes ADR-001 real rather than aspirational.

**Qualification matters.** Without it, a player who appears in one game and scores 30 tops the points-per-game board forever. Totals leaderboards are unqualified; per-game-average leaderboards require qualification.

---

## 10. Validation Engine

Every event passes an ordered rule pipeline before persistence. This is what makes the recording UI safe to make fast and one-tap.

```csharp
public interface IEventRule
{
    RuleResult Evaluate(LiveGameState state, GameEvent candidate, RuleSet rules);
}
```

Failures return `400` with a machine-readable code so the client can render a precise message and, where possible, offer a one-tap correction.

### Tier 1 — reject
| Code | Rule |
|---|---|
| `GAME_NOT_IN_PROGRESS` | Status must be `InProgress` (except `GAME_START`) |
| `EVENT_BEFORE_GAME_START` | A non-setup event precedes `GAME_START` |
| `PLAYER_NOT_ON_ROSTER` | `gameRosterEntryId` must belong to this game |
| `WRONG_TEAM` | Actor's team must match `competitionTeamId` |
| `SHOT_OUT_OF_BOUNDS` | Coordinates outside court bounds |
| `ASSIST_SELF` | Assister ≠ shooter |
| `ASSIST_WRONG_TEAM` | Assister must be a teammate |
| `STEAL_SAME_TEAM` | Stealer must be an opponent |
| `BLOCK_SAME_TEAM` | Blocker must be an opponent |
| `FOUL_DRAWN_SAME_TEAM` | Fouled player must be an opponent (technicals excepted) |
| `FREE_THROW_SEQUENCE` | `attemptNumber` sequential and ≤ `totalAttempts` |
| `TIED_AT_GAME_END` | Cannot end tied unless `allowsTies` |

### Tier 2 — reject by default, overridable
| Code | Rule |
|---|---|
| `PLAYER_NOT_ON_COURT` | Actor must be on court for play events |
| `PLAYER_FOULED_OUT` | Actor has reached `personalFoulLimit` |
| `INVALID_LINEUP_SIZE` | Team must have exactly `playersOnCourt` after substitutions |
| `SUBSTITUTION_WHILE_LIVE` | Clock must be stopped |
| `CLOCK_NOT_MONOTONIC` | `gameClockMs` must not increase within a period |
| `CLOCK_OUT_OF_RANGE` | Clock exceeds period duration or is negative |
| `REBOUND_WITHOUT_MISS` | `REBOUND` must follow a miss or missed final free throw |
| `FREE_THROW_WITHOUT_SOURCE` | Must reference a foul or technical |
| `SHOT_ZONE_MISMATCH` | Coordinates contradict the two/three subtype |
| `TIMEOUT_LIMIT_EXCEEDED` | No timeouts remaining this half |
| `PERIOD_NOT_COMPLETE` | Ending a period with time remaining |

**Override mechanism:** `CompetitionManager` and above may submit with `?override=true` plus an `X-Override-Reason` header. Overridden events are flagged in `payload.overridden` and surfaced prominently on the review screen.

This matters more than it looks. Referees occasionally produce sequences that are legally impossible on paper, and clocks get operated by volunteers. **A system that cannot record what actually happened is worse than one that records it and flags it.** Tier 1 rules are things that are certainly data-entry errors; Tier 2 rules are things that are *probably* errors but might be reality.

---

## 11. API Surface

Base path `/api/v1`. Errors as RFC 9457 `application/problem+json`.

### Auth
```
POST   /auth/register
POST   /auth/login                     → { accessToken, refreshToken, organisations[] }
POST   /auth/refresh
POST   /auth/logout
GET    /auth/me
```

### Organisations
```
GET    /organisations                              # orgs the caller belongs to
POST   /organisations
GET    /organisations/{orgId}
PATCH  /organisations/{orgId}
GET    /organisations/{orgId}/members
POST   /organisations/{orgId}/members/invite
PATCH  /organisations/{orgId}/members/{userId}     # change role
DELETE /organisations/{orgId}/members/{userId}
```

Everything below is scoped by the `org` claim in the JWT **and** an explicit `{orgId}` path segment, validated to match.

### Competition management
```
GET    /organisations/{orgId}/seasons
POST   /organisations/{orgId}/seasons
GET    /organisations/{orgId}/competitions?seasonId=&status=
POST   /organisations/{orgId}/competitions
GET    /competitions/{id}
PATCH  /competitions/{id}
POST   /competitions/{id}/publish
GET    /competitions/{id}/stages
POST   /competitions/{id}/stages
POST   /stages/{id}/groups

GET    /organisations/{orgId}/teams?search=
POST   /organisations/{orgId}/teams
PATCH  /teams/{id}
GET    /competitions/{id}/teams
POST   /competitions/{id}/teams                    # enter a team into the competition
PATCH  /competition-teams/{id}                     # group, seed, status

# ── Player registry (NOT org-scoped; see ADR-003) ───────────────────────
POST   /registry/players/search        # body, not query string — never a NIN in a URL
                                       # { nin } OR { lastName, dateOfBirth|birthYear }
                                       # → limited projection, max 25 rows, audited
POST   /registry/players               # create; only via roster registration
                                       # { firstName, lastName, dateOfBirth, gender,
                                       #   nin?, guardianConsent?, photo? }
                                       # → 409 PLAYER_ALREADY_REGISTERED { playerId }
GET    /registry/players/{id}          # limited projection
GET    /registry/players/{id}/sensitive     # audited as ViewSensitive; restricted
PATCH  /registry/players/{id}          # core identity; blocked once verified
POST   /registry/players/{id}/photo    # presigned upload URL
GET    /registry/players/{id}/photo    # presigned read URL, ≤15 min
GET    /registry/players/{id}/history  # teams, orgs, competitions over time
GET    /registry/players/{id}/eligibility

POST   /registry/players/{id}/verify-nin      # platform admin; licensed provider
POST   /registry/players/{id}/dob-evidence    # upload + review → identity_tier
POST   /registry/players/{id}/anonymise       # NDPA erasure

POST   /registry/merge-proposals              # org proposes; { keepId, mergeId, evidence }
GET    /registry/merge-proposals              # platform admin queue
POST   /registry/merge-proposals/{id}/approve # platform admin ONLY — executes
POST   /registry/merge-proposals/{id}/reject

POST   /registry/players/{id}/flags           # suspension, age dispute, identity dispute
PATCH  /registry/flags/{id}/resolve
GET    /registry/ledger/verify                # recompute and validate the hash chain
GET    /registry/metrics                      # coverage, tier distribution, growth

GET    /competition-teams/{id}/roster
POST   /competition-teams/{id}/roster  # { playerId } ONLY — never free-text names
PATCH  /roster-entries/{id}
DELETE /roster-entries/{id}

GET    /organisations/{orgId}/venues
POST   /organisations/{orgId}/venues
```

### Fixtures
```
GET    /competitions/{id}/games?stageId=&status=&from=&to=
POST   /competitions/{id}/games                    # single fixture
POST   /competitions/{id}/games/generate           # round-robin / bracket generation
GET    /games/{id}
PATCH  /games/{id}                                 # reschedule, venue
DELETE /games/{id}                                 # only while Scheduled
POST   /games/{id}/officials
```

### Game recording
```
GET    /games/{id}/setup            # rosters, rule set, both teams, jersey map, colours
POST   /games/{id}/roster           # mark availability + starters
POST   /games/{id}/lock-roster      # → RosterLocked
POST   /games/{id}/start            # → InProgress
POST   /games/{id}/access-token     # short-lived, game-scoped token for a volunteer official

GET    /games/{id}/state            # LiveGameState — the recording app's home screen
POST   /games/{id}/events           # submit ONE event
POST   /games/{id}/events/batch     # ordered array (free-throw sequences, sub groups)
GET    /games/{id}/events?sinceSequence=           # resync after a dropped connection
POST   /games/{id}/events/{eventId}/void           # { reason }
POST   /games/{id}/events/undo-last                # voids highest non-voided sequence

POST   /games/{id}/end              # → PendingReview
GET    /games/{id}/review           # box score + anomaly warnings + overridden events
POST   /games/{id}/finalize         # → Finalized
POST   /games/{id}/reopen           # → Amending (admin, reason required)
POST   /games/{id}/forfeit          # { winningTeamId, reason }
```

**Event submission contract:**

```jsonc
// POST /api/v1/games/{id}/events
{
  "eventId": "0195f3a1-...",         // client-generated UUID v7 — idempotency key
  "lastKnownSequence": 142,          // optimistic conflict detection
  "eventType": "FIELD_GOAL_MADE",
  "eventSubtype": "ThreePoint",
  "period": 3,
  "gameClockMs": 412000,
  "shotClockMs": 8000,
  "competitionTeamId": "...",
  "gameRosterEntryId": "...",        // shooter
  "secondaryRosterEntryId": "...",   // assister
  "points": 3,
  "shotXCm": 980,
  "shotYCm": -690,
  "payload": { "shotType": "CatchAndShoot" },
  "clientRecordedAt": "2026-08-25T18:32:11.204Z"
}
```

| Status | Meaning |
|---|---|
| `201` | Accepted. Body: `{ sequence, state: LiveGameState }` — returning full state means the client never has to guess |
| `200` | An event with this `eventId` already exists. **Idempotent replay**, returns the original result. This is what makes retry-on-timeout safe |
| `409` | `lastKnownSequence` is stale. Body includes `currentSequence` and the missing events so the client self-heals |
| `400` | Validation failure with a rule code from §10 |

### Statistics & history — the selling point
```
GET  /games/{id}/box-score
GET  /games/{id}/play-by-play?period=
GET  /games/{id}/shot-chart?teamId=&playerId=
GET  /games/{id}/lineups

GET  /competitions/{id}/leaders?stat=points&per=total|game&limit=10
GET  /competitions/{id}/leaders/all              # top N for every headline stat, one call
GET  /competitions/{id}/standings
GET  /competitions/{id}/players/{playerId}/stats
GET  /competitions/{id}/teams/{teamId}/stats

GET  /players/{id}/career                        # totals + per-competition breakdown
GET  /players/{id}/games?competitionId=&limit=
GET  /players/{id}/shot-chart?competitionId=
GET  /organisations/{orgId}/records               # all-time single-game and career records

POST /admin/recompute/competitions/{id}
POST /admin/recompute/games/{id}
```

`GET /competitions/{id}/leaders/all` exists deliberately: it is the headline screen and should not cost six round trips.

---

## 12. Deferred Capabilities — Design For, Do Not Build

### 12.1 Offline support

**Not in v1.** These already-specified decisions keep the door open at near-zero cost:

- **Client-generated event IDs** — a client could queue events locally with final IDs.
- **Idempotent submission** — replaying a queue is safe by construction.
- **`client_recorded_at` on every event** — device-clock ordering survives a sync gap.
- **Sequence-based resync** (`GET /events?sinceSequence=`) — already the reconnect path.
- **The projector is pure** — it could run client-side to produce identical local state.

If offline becomes a requirement, the work is a client-side queue plus a conflict policy for the (rare) case of two scorer's tables on one game. **No server redesign.**

### 12.2 Real-time via SignalR

**Not in v1.** The provision:

- Every accepted event raises the in-process domain event `GameEventRecorded(gameId, sequence, LiveGameState)`.
- Adding SignalR means registering one additional handler that broadcasts to group `game:{gameId}`.
- Define the hub contract now, even unimplemented, so client teams can plan:
  ```
  Hub: /hubs/games
  Client → Server:  SubscribeToGame(gameId), UnsubscribeFromGame(gameId)
  Server → Client:  GameStateChanged(LiveGameState)
                    GameEventRecorded(GameEventDto)
                    GameStatusChanged(gameId, status)
  ```

**Do not build the hub.** Do build the domain event, and ensure nothing in the write path assumes a single consumer.

### 12.3 Other likely follow-ons

Public read-only competition pages; CSV/PDF box score export; FIBA LiveStats import; season-over-season player development views; officials' assignment scheduling.

---

## 13. Cross-Cutting Requirements

**Multi-tenancy safety.** Every tenant-scoped entity implements `ITenantScoped { Guid OrganisationId }`. `AppDbContext` applies a global query filter for all of them, driven by `ICurrentTenant` resolved from the JWT. An architecture test enumerates all `ITenantScoped` implementations and **fails the build** if any lacks a filter. This test is not optional; it is the only thing standing between a forgotten `.Where()` and a cross-tenant data leak.

**The registry exception.** Registry entities — `players`, `player_org_links`, `consent_records`, `player_eligibility_flags`, `registry_audit`, `registry_ledger`, and `player_career_aggregates` — are deliberately **not** tenant-scoped (ADR-003). The architecture test must therefore assert against an **explicit whitelist by type name**, not merely skip types lacking `ITenantScoped`. A new unfiltered entity added by mistake must fail the build; only the named seven may pass. An exception invisible in code is indistinguishable from a bug, and this is the one place where a bug leaks a national database.

**PII handling.** The NIN pepper lives in a secrets manager, injected at runtime, never in `appsettings.json` and never in source control. Rotating it requires re-HMAC from re-collected NINs, so treat rotation as a planned migration rather than an operational routine. Serilog must be configured with a destructuring policy that **redacts `nin`, `ninHmac`, `guardianPhone`, and `photoObjectKey` from every log line**, and an integration test must assert that submitting a NIN produces no log entry containing it.

**Data subject rights (NDPA).** Erasure conflicts directly with the immutability of `game_events`. The resolution: **anonymise the identity, retain the statistics.** Clear names, NIN hash, photo, and guardian details; set `anonymised_at`; retain `player_id` and every derived statline under a tombstoned record. Sporting results are a matter of legitimate record; the identity attached to them is not. Build `POST /registry/players/{id}/anonymise` in the registry phase, not as an afterthought.

---

## 14. Testing Strategy

| Layer | Approach |
|---|---|
| **Projector** | **Golden-file tests.** Store real event logs as JSON fixtures with expected statline output. This is the highest-value suite in the codebase — the entire product promise rests on the projector being correct |
| **Validation rules** | One unit test per rule code in §10, positive and negative |
| **Court geometry** | Property test: every point inside the arc classifies as two, every point outside as three; boundary and corner-break points asserted explicitly |
| **State machine** | Exhaustive transition table — every (status, transition) pair either succeeds or fails with a named reason |
| **API** | Integration tests on Testcontainers Postgres. At minimum one full happy path: create org → competition → teams → rosters → fixture → record a complete 4-period game → finalise → assert leaderboard |
| **Tenancy** | Integration test that org A cannot read any org B entity through any endpoint |
| **Idempotency** | Submit the same `eventId` twice; assert one row and byte-identical responses |
| **Architecture** | Module boundary enforcement; tenant filter coverage |

**Build a realistic seed fixture early:** one organisation, one 8-team tournament, full rosters, and three complete games' worth of events. It makes every later feature testable by hand, gives the client teams something to develop against, and doubles as demo data.

---

## 15. Implementation Phases

Build in this order. Each phase ends in a demonstrable, tested state.

**Phase 1 — Foundation.** Solution structure, `AppDbContext`, tenancy plumbing, auth (register/login/refresh), organisations and memberships, `ProblemDetails` middleware, health checks, first migration.

**Phase 2 — Competition management.** Seasons, competitions, rule sets, teams, competition entries, players (search + merge + duplicate detection), roster entries, venues. This alone makes the management app functional end to end.

**Phase 3 — Fixtures.** Games, scheduling, round-robin and bracket generation, officials, state machine through `RosterLocked`.

**Phase 4 — Event recording.** `game_events`, the validation pipeline, submission with idempotency and conflict detection, void/undo, `LiveGameState`, game-scoped tokens. **The projector is written here, and it is the most important code in the system.** Do not proceed past this phase until the golden-file suite is green.

**Phase 5 — Statistics.** Statline persistence on finalisation, lineup stints, plus/minus, competition aggregates, career aggregates, standings, the outbox worker, admin recompute commands.

**Phase 6 — Query surface.** Box scores, play-by-play, shot charts, leaderboards, career pages, all-time records. Index tuning against realistic volumes.

**Phase 7 — Hardening.** Rate limiting on the event endpoint, OpenTelemetry, structured logging on the write path, load test with a realistic tournament (8 concurrent courts), backup and restore rehearsal.

---

## 16. Open Questions for the Product Owner

1. **Does NBBF get powers above a normal organisation?** As the national federation it may want cross-organisation visibility, or the authority to raise `scope = Platform` eligibility flags that bind every league. Today those sit with platform admin — you. If NBBF should hold them, model it as a **grantable federation capability on an organisation**, not a hardcoded special case, so state associations or a second sport can be added later without a schema change.
2. **Who is platform admin?** Merge approval, NIN verification, and identity-tier authority sit above every organisation. Confirm this is your team, and that the merge queue has an owner with time to work it.
3. **NIN verification provider and licensing.** Which licensed channel, at what per-lookup cost? If priced per call, roster registration must batch or defer rather than calling on every addition.
4. **Consent wording.** Needs Nigerian counsel before the first record is written (§5A.7). Everything else here can be refactored later; this cannot.
5. **First design partner.** Records are created only through roster registration, so registry value is bounded by platform adoption. Which elite competition goes first, and does its season set the launch deadline?
6. **Cross-border players.** Non-Nigerians have no NIN and cannot reach tier 2. Parallel passport route, or cap at tier 1?
7. **Team historical records.** Is "most points by a team in a game, all time" required? Determines whether `team_game_statlines` needs its own aggregate table.
