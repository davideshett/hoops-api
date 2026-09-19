import { useMemo, useState } from 'react';
import { indexRoster, parseCsv } from './csv';
import { formatClock } from './clock';
import type { GameRosterEntry, SubmitEvent, TeamRoster } from './types';

const EXAMPLE = `period,clock,type,subtype,team,jersey,secondary,x,y,payload
1,10:00,PERIOD_START,Regulation,,,,,,
1,10:00,CLOCK_START,,,,,,,
1,9:42,FIELD_GOAL_MADE,TwoPoint,H,0,00,1100,-60,
1,9:20,FOUL,Shooting,A,0,0,,,freeThrowsAwarded=2
1,9:20,FREE_THROW_MADE,,H,0,,,,attemptNumber=1;totalAttempts=2
1,9:20,FREE_THROW_MISSED,,H,0,,,,attemptNumber=2;totalAttempts=2
1,9:20,REBOUND,Defensive,A,5,,,,
1,9:05,FIELD_GOAL_MISSED,ThreePoint,A,5,,540,0,
1,9:03,REBOUND,Offensive,A,6,,,,
1,8:58,FIELD_GOAL_MADE,TwoPoint,A,6,,1200,0,`;

export function CsvImport({ roster, home, away, onSubmit, onClose }: {
  roster: GameRosterEntry[]; home: TeamRoster; away: TeamRoster; onSubmit: (events: SubmitEvent[]) => void; onClose: () => void;
}) {
  const [text, setText] = useState(EXAMPLE);
  const index = useMemo(() => indexRoster(roster, { id: home.competitionTeamId, shortName: home.shortName }, { id: away.competitionTeamId, shortName: away.shortName }), [roster, home, away]);
  const rows = useMemo(() => parseCsv(text, index), [text, index]);
  const errors = rows.filter((r) => r.error);
  const events = rows.flatMap((r) => (r.event ? [r.event] : []));

  return (
    <div className="modal-backdrop">
      <div className="modal wide">
        <h2>Import events from CSV</h2>
        <p className="muted small">One event per line: <code>period,clock,type,subtype,team,jersey,secondary,x,y,payload</code>. Team is H/A or a short name; jerseys resolve through this game's roster; x,y are the canonical court frame in cm. Rows are submitted in order through the same queue as taps, so validation and overrides apply exactly as if you had tapped them.</p>
        <textarea value={text} onChange={(e) => setText(e.target.value)} rows={12} spellCheck={false} />
        <input type="file" accept=".csv,text/csv" onChange={(e) => e.target.files?.[0]?.text().then(setText)} />
        <div className="preview">
          {rows.slice(0, 200).map((r) => (
            <div key={r.line} className={`feed-row ${r.error ? 'rejected' : 'accepted'}`}>
              line {r.line}: {r.error ?? `P${r.event!.period} ${formatClock(r.event!.gameClockMs)} ${r.event!.eventType}${r.event!.eventSubtype ? '/' + r.event!.eventSubtype : ''}`}
            </div>
          ))}
        </div>
        <div className="row">
          <button className="primary" disabled={events.length === 0 || errors.length > 0} onClick={() => onSubmit(events)}>
            Submit {events.length} events
          </button>
          {errors.length > 0 && <span className="error">{errors.length} row(s) need fixing first</span>}
          <button onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  );
}
