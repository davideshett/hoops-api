// Shapes as the API returns them (camelCase JSON). Kept to the fields the app uses.

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

export interface Me {
  userId: string;
  email: string;
  organisations: { id: string; name: string; role: string }[];
}

export interface Competition { id: string; name: string; seasonId: string; status: string }
export interface CompetitionTeam { id: string; teamId: string; displayName?: string | null; name?: string; shortName?: string }
export interface Team { id: string; name: string; shortName: string; abbreviation?: string | null }

export interface Game {
  id: string;
  competitionId: string;
  homeCompetitionTeamId: string;
  awayCompetitionTeamId: string;
  scheduledAt: string;
  status: string;
}

export interface RuleSet {
  numberOfPeriods: number;
  periodDurationSeconds: number;
  overtimeDurationSeconds: number;
  playersOnCourt: number;
  personalFoulLimit: number;
}

export interface AvailablePlayer {
  rosterEntryId: string;
  playerId: string;
  fullName: string;
  jerseyNumber: string;
  position?: string | null;
  isCaptain: boolean;
}

export interface TeamRoster {
  competitionTeamId: string;
  name: string;
  shortName: string;
  abbreviation?: string | null;
  players: AvailablePlayer[];
}

export interface GameSetup { game: Game; ruleSet: RuleSet; teams: TeamRoster[] }

export interface GameRosterEntry {
  id: string; // gameRosterEntryId — what every event references
  competitionTeamId: string;
  playerId: string;
  fullName: string;
  jerseyNumber: string;
  position?: string | null;
  isStarter: boolean;
}

export interface LiveState {
  lastSequence: number;
  currentPeriod: number;
  gameClockMs: number;
  clockRunning: boolean;
  gameStarted: boolean;
  gameEnded: boolean;
  periodEnded: boolean;
  score: Record<string, number>;
  teamFouls: Record<string, number>;
  timeoutsRemaining: Record<string, number>;
  onCourt: Record<string, string[]>;
}

export interface GameEvent {
  eventId: string;
  sequence: number;
  eventType: string;
  eventSubtype?: string | null;
  period: number;
  gameClockMs: number;
  competitionTeamId?: string | null;
  gameRosterEntryId?: string | null;
  secondaryRosterEntryId?: string | null;
  points?: number | null;
  shotZone?: string | null;
  isVoided: boolean;
  payload?: Record<string, string>;
}

export interface EventAccepted { sequence: number; event: GameEvent; state: LiveState }

/** What the app builds and queues. Mirrors SubmitEventRequest. */
export interface SubmitEvent {
  eventId: string;
  lastKnownSequence?: number;
  eventType: string;
  eventSubtype?: string | null;
  period: number;
  gameClockMs: number;
  competitionTeamId?: string | null;
  gameRosterEntryId?: string | null;
  secondaryRosterEntryId?: string | null;
  shotXCm?: number | null;
  shotYCm?: number | null;
  clientRecordedAt: string;
  payload?: Record<string, string>;
  /** Client-side only: set when the official confirmed a tier-2 override. */
  override?: { reason: string };
}

export interface Problem {
  status: number;
  code: string;
  title: string;
  detail: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
