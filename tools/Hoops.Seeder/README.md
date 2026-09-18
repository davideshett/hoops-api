# Hoops Seeder

Populates a running API with a realistic dataset (implementation-roadmap §3). It drives the
**HTTP API**, not the database, so it can only create data the system would accept — and a
successful run is an end-to-end smoke test of every module working together.

## Run

```bash
# 1. Start the API against an EMPTY database
dotnet run --project src/Hoops.Api

# 2. Seed it (≈15 seconds)
dotnet run --project tools/Hoops.Seeder
```

Options: `--api http://localhost:5290` (default) and `--db <connection string>` (defaults to the
local dev database; used for one statement only, see below).

To re-seed, reset the database first — the seeder refuses to run twice:

```bash
docker exec postgres psql -U postgres -c "DROP DATABASE hoops WITH (FORCE);" -c "CREATE DATABASE hoops;"
```

## What you get

| | |
|---|---|
| **Users** | `admin@hoops.local` — platform admin and owner of both orgs. `anambra.stats@hoops.local`, `lagos.stats@hoops.local` — statisticians. Password for all: `Seed-Password-1` |
| **Organisations** | Anambra Basketball Association (senior men) and Lagos Schools League (U18) |
| **Competitions** | One season and one league each, 8 teams × 12 players, with venues |
| **Shared players** | 6 registry records rostered in **both** organisations — found by NIN search, never re-created. Their `/career` spans both. |
| **Identity tiers** | Every third Anambra adult is NIN-verified (tier 2, via the stub provider); half the rest have DOB evidence (tier 1); the remainder are tier 0 |
| **Minors** | Every Lagos schoolboy plus the six shared players are under 18 and carry guardian consent |
| **Games** | 28 round-robin fixtures in Anambra; the first 3 fully recorded (~330 events each) and finalised, so standings, box scores, leaderboards, shot charts and play-by-play are all populated |
| **Override** | Game 3 contains a rebound recorded after a made basket with `override=true` — `REBOUND_WITHOUT_MISS` is tier 2 — so the review screen has something to flag |
| **Duplicate** | One Anambra player re-registered in Lagos without a NIN, on a roster, with a **pending** merge proposal. Approve it as the admin and watch the career totals combine. |

Games are scripted from a fixed seed, so every run produces the same three scorelines.

## Things worth trying afterwards

- `GET /competitions/{id}/leaders?stat=points&per=game` is **empty**, and that is correct: with 3 of
  28 fixtures played, nobody has reached the 75 % qualification threshold. `per=total` lists everyone.
- Approve the merge proposal, then `GET /players/{keepId}/career` — the games follow the survivor.
- `GET /registry/ledger/verify` — the hash chain over every registry write the seeder just made.
- Reopen a finalised game, void an event, finalise again: standings recompute through the outbox.

## The one direct database write

Platform admin has no endpoint by design (§16 — who holds it is a governance decision, not an API
call). The seeder grants it with a single `UPDATE users SET is_system_admin = true`, which is why it
takes `--db`. Everything else goes through the API.
