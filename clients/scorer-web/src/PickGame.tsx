import { useEffect, useState } from 'react';
import { get, session } from './api';
import type { Competition, CompetitionTeam, Game, Me, Team } from './types';

export interface Picked { orgId: string; role: string; game: Game; teamNames: Record<string, string> }

export function PickGame({ onPick, onSignOut }: { onPick: (p: Picked) => void; onSignOut: () => void }) {
  const [me, setMe] = useState<Me | null>(null);
  const [orgId, setOrgId] = useState('');
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [competitionId, setCompetitionId] = useState('');
  const [games, setGames] = useState<Game[]>([]);
  const [teamNames, setTeamNames] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { get<Me>('/api/v1/auth/me').then((m) => { setMe(m); setOrgId(m.organisations[0]?.id ?? ''); }).catch((e) => setError(String(e))); }, []);

  useEffect(() => {
    if (!orgId) return;
    get<Competition[]>(`/api/v1/organisations/${orgId}/competitions`).then((c) => { setCompetitions(c); setCompetitionId(c[0]?.id ?? ''); });
  }, [orgId]);

  useEffect(() => {
    if (!orgId || !competitionId) return;
    (async () => {
      // Fixtures carry team IDS; one lookup per competition gives the names (guide §11).
      const [gameList, entered, teams] = await Promise.all([
        get<Game[]>(`/api/v1/organisations/${orgId}/competitions/${competitionId}/games`),
        get<CompetitionTeam[]>(`/api/v1/organisations/${orgId}/competitions/${competitionId}/teams`),
        get<Team[]>(`/api/v1/organisations/${orgId}/teams`),
      ]);
      const byTeam = new Map(teams.map((t) => [t.id, t]));
      const names: Record<string, string> = {};
      for (const ct of entered) names[ct.id] = ct.displayName || byTeam.get(ct.teamId)?.name || ct.id.slice(0, 8);
      setTeamNames(names);
      setGames(gameList.sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt)));
    })().catch((e) => setError(String(e)));
  }, [orgId, competitionId]);

  const recordable = new Set(['Scheduled', 'RosterLocked', 'InProgress']);

  return (
    <div className="page">
      <header className="bar">
        <h1>Pick a game</h1>
        <span className="muted">{session.current?.email}</span>
        <button onClick={onSignOut}>Sign out</button>
      </header>
      {error && <p className="error">{error}</p>}
      <div className="row">
        <label>Organisation
          <select value={orgId} onChange={(e) => setOrgId(e.target.value)}>
            {me?.organisations.map((o) => <option key={o.id} value={o.id}>{o.name} ({o.role})</option>)}
          </select>
        </label>
        <label>Competition
          <select value={competitionId} onChange={(e) => setCompetitionId(e.target.value)}>
            {competitions.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </label>
      </div>
      <table className="fixtures">
        <thead><tr><th>When</th><th>Home</th><th>Away</th><th>Status</th><th /></tr></thead>
        <tbody>
          {games.map((g) => (
            <tr key={g.id} className={recordable.has(g.status) ? '' : 'muted'}>
              <td>{new Date(g.scheduledAt).toLocaleString()}</td>
              <td>{teamNames[g.homeCompetitionTeamId]}</td>
              <td>{teamNames[g.awayCompetitionTeamId]}</td>
              <td>{g.status}</td>
              <td>{recordable.has(g.status) && <button className="primary" onClick={() => onPick({ orgId, role: me?.organisations.find((o) => o.id === orgId)?.role ?? '', game: g, teamNames })}>
                {g.status === 'Scheduled' ? 'Set up' : 'Open'}</button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
