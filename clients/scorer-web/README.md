# Hoops Scorer — statistics collection client

A browser app for recording a game against the Hoops API, live at the table **or after the fact**
from a video or a sheet. It is a client of the public API only; it has no server of its own.

```bash
# API first (against a seeded database — see tools/Hoops.Seeder/README.md)
dotnet run --project src/Hoops.Api

# then
cd clients/scorer-web && npm install && npm run dev      # http://localhost:5173
```

Sign in as `anambra.stats@hoops.local` / `Seed-Password-1` (a statistician) or `admin@hoops.local`.
Point it elsewhere with `VITE_API_URL`.

## How it works

- **There is no clock.** You record what happened in each quarter, in order — nothing more. The
  API requires a clock reading on every event, so the app sends the quarter's full length for every
  play and 0:00 for the quarter end; a reading that never moves is a valid log. **What this costs:**
  minutes played, plus/minus and lineup durations are 0 for games recorded here. Scoring, shooting,
  rebounds, assists, steals, blocks, fouls, standings and leaderboards are unaffected. If you have
  clock readings (from a video, say) the CSV import's optional `clock` column takes them.
- **Quarters.** Q1–Q4, then OT1, OT2… The API calls them periods.
- **Tap a player, then an action.** Shots ask for a court location, then the assister (made) or
  blocker (missed). Fouls ask for the type, free throws awarded, and who was fouled — then the fouled
  player is pre-selected with the free-throw sequence ready. Subs: tap the player out, then in.
- **Steals and blocks** are attributes of another event, not events (ADR-002): a steal is the second
  player on a turnover, a block the second player on a missed shot. Record them either way round —
  from the victim (Turnover → tap the stealer; 2PT ✗ → tap the blocker) or from the defender
  (select the defender → **Steal** → tap who lost it; **Block** → tap the shooter → tap the court).
- **Every event goes through one queue**, persisted in `localStorage`, sent one at a time with the
  last known sequence. A lost response is retried safely (client-generated UUID v7 ids). A rejection
  pauses the queue: tier-1 shows the message; tier-2 offers *Override and record* with a reason.
- **Import CSV** takes a sheet of events (`quarter,clock,type,subtype,team,jersey,secondary,x,y,payload`, clock optional)
  and feeds it through the same queue, so validation and overrides apply exactly as for taps.
  Jerseys resolve through the game roster; team is `H`/`A` or a short name.
- **Roles.** A statistician can set up, lock, record and end a game. Finalising is a competition
  manager's review step; the app says so instead of showing a 403.

Everything the app shows — score, fouls, timeouts, who is on court — comes from the server's
`state` on each response. It never computes a statistic itself.
