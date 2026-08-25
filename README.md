# Nigerian Basketball Platform — API

One .NET Web API serving three products:

| Product | What it does |
|---|---|
| **Player Registry** | Canonical record of players. Identity verification, confidence tiers, consent |
| **Tournament Management** | Organisations, competitions, teams, rosters, fixtures |
| **Statistics Recording** | Live game event capture at the scorer's table |

Front ends live in separate repositories.

## Layout

```
src/
  Hoops.Api/                  Controllers, auth, middleware. The host.
  Hoops.SharedKernel/         Result<T>, domain primitives, court geometry.
  Hoops.Modules.Identity/     Users, organisations, memberships, roles.
  Hoops.Modules.Registry/     Players, identity tiers, consent, merge.
  Hoops.Modules.Competitions/ Seasons, competitions, teams, rosters, fixtures.
  Hoops.Modules.GameRecording/ Game state machine, event log, validation.
  Hoops.Modules.Statistics/   Projector, statlines, aggregates, leaderboards.
  Hoops.Infrastructure/       EF Core, migrations, repositories, outbox.
tests/
docs/                         Architecture + roadmap. THE CONTRACT.
```

## Running locally

```bash
docker compose up -d
dotnet run --project src/Hoops.Api
```

Swagger UI is at `/swagger` on the port shown at startup.

## Tests

```bash
dotnet test
```

## Before changing anything structural

Read `docs/platform-architecture.md`. It is the source of truth.
`docs/implementation-roadmap.md` is the build order.
