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

- **The clock is a field you type, not a timer.** Period and `mm:ss` carry over between events;
  edit them when the reading changes (−10s / −1s nudges for video). After every accepted event the
  field snaps to the server's clock, so a CSV import or a reload leaves it where the game actually is.
- **Tap a player, then an action.** Shots ask for a court location, then the assister (made) or
  blocker (missed). Fouls ask for the type, free throws awarded, and who was fouled — then the fouled
  player is pre-selected with the free-throw sequence ready. Subs: tap the player out, then in.
- **Every event goes through one queue**, persisted in `localStorage`, sent one at a time with the
  last known sequence. A lost response is retried safely (client-generated UUID v7 ids). A rejection
  pauses the queue: tier-1 shows the message; tier-2 offers *Override and record* with a reason.
- **Import CSV** takes a sheet of events (`period,clock,type,subtype,team,jersey,secondary,x,y,payload`)
  and feeds it through the same queue, so validation and overrides apply exactly as for taps.
  Jerseys resolve through the game roster; team is `H`/`A` or a short name.
- **Roles.** A statistician can set up, lock, record and end a game. Finalising is a competition
  manager's review step; the app says so instead of showing a 403.

Everything the app shows — score, fouls, timeouts, who is on court — comes from the server's
`state` on each response. It never computes a statistic itself.
