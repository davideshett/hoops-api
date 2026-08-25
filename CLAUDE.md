# Nigerian Basketball Platform API — Working Agreements

## Before you start

Read `docs/platform-architecture.md` before changing anything structural, and
`docs/implementation-roadmap.md` for the current phase and its acceptance
criteria. If a shortcut conflicts with those documents, raise the conflict —
do not diverge silently, and do not edit the documents to match your code.

## Non-negotiables

1. `game_events` rows are NEVER updated (except is_voided) and NEVER deleted.
2. Projection tables must be fully rebuildable from `game_events` alone.
   If you write something to a projection that cannot be derived from the log,
   you have broken the product. There is a test for this.
3. The projector is a PURE function. No I/O, no DateTime.Now, no randomness.
4. Modules reference other modules ONLY through their `Contracts` namespace.
5. Every tenant-scoped entity implements ITenantScoped and has a global query
   filter. Registry entities are the ONLY intentional exception (ADR-003) and
   are named in an explicit whitelist. Never loosen that test — add to the list.
6. Statistics are never authored directly. Only events are authored.
7. The plaintext NIN is NEVER stored, logged, or placed in a URL. HMAC only.
   If you find yourself writing `player.Nin = ...`, stop and re-read ADR-008.
8. Rosters are built from an existing playerId. Never from a free-text name.

## Conventions

- MVC Controllers with [ApiController]. One folder per module under
  `src/Hoops.Api/Controllers/`. Controllers are thin: parse, call one
  application service, map the result. MVC types NEVER appear in module libraries.
- EVERY action carries an explicit [Authorize(Policy = ...)] or [AllowAnonymous].
  There is an architecture test for this. Do not disable it.
- EVERY action carries [ProducesResponseType] for each status it returns, plus
  XML doc comments — these become the Swagger documentation.
- No MediatR. No AutoMapper. Hand-written ToDto() extension methods.
- Result<T> for expected failures. Exceptions only for genuinely exceptional cases.
- snake_case in the database, PascalCase in C#. Configure via EF conventions.
- All timestamps are timestamptz in UTC. Game clock is milliseconds REMAINING.
- Jersey numbers are strings. "00" and "0" are different players in FIBA.

## Project reference graph — do not add references beyond this

```
Hoops.SharedKernel   → nothing
Hoops.Modules.*      → SharedKernel only (no Infrastructure, no MVC)
Hoops.Infrastructure → SharedKernel + all modules
Hoops.Api            → SharedKernel + Infrastructure + all modules
```

Repositories are declared as interfaces in each module's Application layer and
implemented in Infrastructure. This keeps modules extractable later.

## Testing

- Every new validation rule needs a positive and a negative unit test.
- Every projector change needs its golden files regenerated AND reviewed by a
  human. A regenerated golden file nobody reads is not a test.
- Run `dotnet test` before declaring any phase complete.

## Commands

```bash
docker compose up -d
dotnet run --project src/Hoops.Api
dotnet test
dotnet ef migrations add <Name> \
  --project src/Hoops.Infrastructure --startup-project src/Hoops.Api
```

## Scope discipline

Do ONE phase at a time. Stop at the phase boundary and report against the
acceptance criteria. Do not begin the next phase without being asked.
