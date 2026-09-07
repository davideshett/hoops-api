# Deployment & Migration Strategy

Companion to `docs/platform-architecture.md`. Covers how the API is built, released, and migrated,
and how to rehearse a restore.

---

## 1. Configuration and secrets

Everything environment-specific is configuration; nothing secret is in source control.

| Setting | Where it comes from | Notes |
|---|---|---|
| `ConnectionStrings__Postgres` | Environment / secret store | — |
| `Jwt__SigningKey` | **Secret store only** | ≥ 32 bytes. Startup fails fast if absent or short. |
| `Registry__NinPepper` | **Secret store only** | ADR-008. See §4 on rotation — it is a migration, not an operation. |
| `Otlp__Endpoint` | Environment | Unset disables the exporter; spans are still produced. |
| `RateLimiting__EventsPerMinute` | Environment | Default 600 per game. Deliberately generous. |

Development is the only environment that supplies fallbacks (an obvious placeholder JWT key and NIN
pepper), and it does so in memory so nothing lands in a committed file.

---

## 2. Pipeline

`.github/workflows/ci.yml` runs two jobs on every push and pull request.

**build-and-test** — restore, build in Release, run all suites. Warnings are errors, so this is the
style gate as well as the correctness gate. Integration tests bring up their own Postgres through
Testcontainers, so no service container is configured.

**migration-check** — two things the test suite cannot catch:

1. `dotnet ef migrations has-pending-model-changes` fails the build if an entity changed without a
   migration. That drift is invisible locally until a deploy fails.
2. Generates the idempotent SQL script and publishes it as an artifact, proving the script the deploy
   will run actually generates.

---

## 3. Release and migration

Migrations are applied as **SQL, ahead of the new code**, not by the application at startup.

```bash
# 1. Produce the script (idempotent: safe to re-run, skips already-applied migrations)
dotnet ef migrations script --idempotent --output migrations.sql \
  --project src/Hoops.Infrastructure --startup-project src/Hoops.Api

# 2. Back up first — see §5
# 3. Apply
psql "$CONNECTION_STRING" -v ON_ERROR_STOP=1 -f migrations.sql

# 4. Deploy the new application version
```

> **On `Database.Migrate()` at startup.** `Program.cs` migrates on boot **only in Development**, so
> `docker compose up -d && dotnet run` is enough locally and the integration tests get a migrated
> database for free. It is deliberately disabled elsewhere: with more than one instance running,
> startup migration is a race between them.

**Compatibility.** Because migrations land before the code, every migration must be backwards
compatible with the *currently running* version for the length of the rollout. Additive changes are
safe. Destructive ones (dropping or renaming a column, tightening a constraint) are split across two
releases: release N adds and dual-writes, release N+1 removes.

**The event log is never migrated destructively.** `game_events` rows are append-only (ADR-001).
A change in how an event is interpreted belongs in the projector, not in an `UPDATE` — and because
every projection is rebuildable, reinterpreting history is a recompute, not a data migration.

---

## 4. NIN pepper rotation

Rotating `Registry__NinPepper` invalidates every stored `nin_hmac`, because the HMAC is computed with
it. There is no way to re-derive the hashes: the plaintext NINs were never stored (ADR-008). Rotation
therefore means **re-collecting NINs from players**, and is a planned migration with a product cost,
not an operational routine. Treat a leaked pepper as an incident requiring that plan.

---

## 5. Backup and restore

```bash
# Backup
pg_dump -Fc "$CONNECTION_STRING" -f "hoops-$(date -u +%Y%m%dT%H%M%SZ).dump"

# Restore into a fresh database
createdb hoops_restored
pg_restore -d hoops_restored --no-owner --clean --if-exists "hoops-....dump"
```

**A restore is only verified once the recompute test passes against it.** Loading without error
proves the file is readable; it does not prove the history is intact. The real check is that the
event log in the restored copy still reproduces the same statistics:

```sql
-- In the RESTORED database, discard everything derived.
TRUNCATE player_game_statlines, team_game_statlines, game_period_states, lineup_stints,
         competition_player_aggregates, player_career_aggregates, competition_standings CASCADE;
```

```bash
# Then rebuild from the logs alone and compare the API's output with production's.
curl -X POST ".../admin/recompute/competitions/{id}" -H "Authorization: Bearer $TOKEN"
```

`HardeningTests.A_restored_backup_passes_the_full_recompute_equality_test` performs exactly this
rehearsal on every CI run: dump, restore into a second database, truncate every derived table,
recompute, and assert the box score and standings come back byte-identical.

**Cadence.** Nightly full dumps with point-in-time recovery enabled; retention set by the NDPA
retention position (§13). Rehearse a restore on a schedule — an unrehearsed backup is a hypothesis.

---

## 6. Observability

- **Traces.** OpenTelemetry over OTLP. The event write path has its own span source
  (`Hoops.GameRecording`), tagged with `hoops.game_id`, `hoops.sequence`, `hoops.outcome`, and, on a
  rejection, `hoops.rule_code`. Health probes are filtered out.
- **Logs.** Serilog, structured. Every recording log line carries `gameId` and `userId` via a scope;
  a rejected event additionally logs the rule code, the client `eventId`, the period, the clock, and
  the roster entries involved — enough to reconstruct the tap without opening the database. A
  redaction policy strips `nin`, `ninHmac`, `guardianPhone`, and `photoObjectKey` from every line.
- **Health.** `/health` is liveness; `/health/ready` checks Postgres and is the readiness probe.

---

## 7. Rate limiting

`POST /events` and `/events/batch` are limited **per game**, not per user: two officials working the
same game share a budget, and a busy court never starves a quiet one. The default of 600 events per
minute per game is far above any human tapping rate — it exists to absorb a runaway client or a retry
storm, not to pace an official. A rejection returns `429` as problem-details with code `RATE_LIMITED`;
since submission is idempotent, the client can simply retry.
