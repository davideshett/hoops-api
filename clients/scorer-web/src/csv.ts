import { parseClock, uuidv7 } from './clock';
import type { GameRosterEntry, SubmitEvent } from './types';

/**
 * CSV import — recording after the game from a sheet. One row per event:
 *
 *   period,clock,type,subtype,team,jersey,secondary,x,y,payload
 *   1,10:00,PERIOD_START,Regulation,,,,,,
 *   1,9:42,FIELD_GOAL_MADE,TwoPoint,H,7,11,1100,-60,
 *   1,9:20,FOUL,Shooting,A,5,7,,,freeThrowsAwarded=2
 *   1,9:20,FREE_THROW_MADE,,H,7,,,,attemptNumber=1;totalAttempts=2
 *
 * team is H or A (or a team's short name); jersey/secondary resolve through the game roster;
 * payload is key=value pairs separated by ';'. Column order is fixed; a header row is optional.
 */

export interface CsvRow { line: number; event?: SubmitEvent; error?: string; raw: string }

export interface RosterIndex {
  home: { id: string; shortName: string };
  away: { id: string; shortName: string };
  byTeamAndJersey: Map<string, GameRosterEntry>;
}

export function indexRoster(entries: GameRosterEntry[], home: { id: string; shortName: string }, away: { id: string; shortName: string }): RosterIndex {
  const byTeamAndJersey = new Map<string, GameRosterEntry>();
  for (const e of entries) byTeamAndJersey.set(`${e.competitionTeamId}|${e.jerseyNumber}`, e);
  return { home, away, byTeamAndJersey };
}

export function parseCsv(text: string, roster: RosterIndex): CsvRow[] {
  const rows: CsvRow[] = [];
  const lines = text.split(/\r?\n/);
  lines.forEach((raw, i) => {
    const line = i + 1;
    if (!raw.trim() || /^period\s*,/i.test(raw)) return;
    const cols = raw.split(',').map((c) => c.trim());
    const [periodText, clockText, type, subtype, teamText, jersey, secondary, x, y, payloadText] = cols;

    const period = Number(periodText);
    const clock = parseClock(clockText ?? '');
    if (!Number.isInteger(period) || period < 1) return rows.push({ line, raw, error: `bad period '${periodText}'` });
    if (clock === null) return rows.push({ line, raw, error: `bad clock '${clockText}' (mm:ss)` });
    if (!type) return rows.push({ line, raw, error: 'missing event type' });

    const team = resolveTeam(teamText, roster);
    if (teamText && !team) return rows.push({ line, raw, error: `unknown team '${teamText}'` });

    const actor = jersey ? resolvePlayer(team?.id, jersey, roster) : null;
    if (jersey && !actor) return rows.push({ line, raw, error: `no #${jersey} on ${teamText}` });

    // Which team the secondary is on depends on the event: an assister is a TEAMMATE of the shooter;
    // a stealer, blocker, or player fouled is an OPPONENT. Both teams have a #12, so guessing is
    // not an option — the server rejects the wrong one (STEAL_SAME_TEAM etc.), but better not to send it.
    let secondaryEntry = null;
    if (secondary) {
      const upper = type.toUpperCase();
      const other = team?.id === roster.home.id ? roster.away.id : roster.home.id;
      const sameTeam = upper === 'FIELD_GOAL_MADE' || upper === 'SUBSTITUTION';
      secondaryEntry = resolvePlayer(sameTeam ? team?.id : other, secondary, roster);
      if (!secondaryEntry) return rows.push({ line, raw, error: `no #${secondary} on the ${sameTeam ? 'same' : 'opposing'} team` });
    }

    const payload: Record<string, string> = {};
    for (const pair of (payloadText ?? '').split(';').filter(Boolean)) {
      const [k, v] = pair.split('=');
      if (k && v !== undefined) payload[k.trim()] = v.trim();
    }

    rows.push({
      line, raw,
      event: {
        eventId: uuidv7(),
        eventType: type.toUpperCase(),
        eventSubtype: subtype || null,
        period,
        gameClockMs: clock,
        competitionTeamId: team?.id ?? null,
        gameRosterEntryId: actor?.id ?? null,
        secondaryRosterEntryId: secondaryEntry?.id ?? null,
        shotXCm: x ? Number(x) : null,
        shotYCm: y ? Number(y) : null,
        clientRecordedAt: new Date().toISOString(),
        payload: Object.keys(payload).length ? payload : undefined,
      },
    });
  });
  return rows;
}

function resolveTeam(text: string | undefined, roster: RosterIndex) {
  if (!text) return null;
  const t = text.toUpperCase();
  if (t === 'H' || t === 'HOME' || t === roster.home.shortName.toUpperCase()) return roster.home;
  if (t === 'A' || t === 'AWAY' || t === roster.away.shortName.toUpperCase()) return roster.away;
  return null;
}

function resolvePlayer(teamId: string | undefined, jersey: string, roster: RosterIndex) {
  return teamId ? roster.byTeamAndJersey.get(`${teamId}|${jersey}`) ?? null : null;
}
