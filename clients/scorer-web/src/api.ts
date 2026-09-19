import type { AuthResponse, Problem } from './types';

/** Thin client over the Hoops API: bearer auth, proactive refresh, problem-details errors. */

const BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5290';
const STORAGE = 'hoops.auth';

export class ApiError extends Error {
  readonly problem: Problem;
  constructor(problem: Problem) {
    super(`${problem.code}: ${problem.detail}`);
    this.problem = problem;
  }
}

interface Stored extends AuthResponse { email: string }

let auth: Stored | null = JSON.parse(localStorage.getItem(STORAGE) ?? 'null');

export const session = {
  get current() { return auth; },
  clear() { auth = null; localStorage.removeItem(STORAGE); },
};

function store(next: Stored) {
  auth = next;
  localStorage.setItem(STORAGE, JSON.stringify(next));
}

export async function login(email: string, password: string): Promise<void> {
  const response = await fetch(`${BASE}/api/v1/auth/login`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ email, password }),
  });
  if (!response.ok) throw new ApiError(await response.json());
  store({ ...(await response.json()), email });
}

/** Refresh when the access token is within five minutes of expiry — never mid-free-throw on a 401. */
async function ensureFresh(): Promise<void> {
  if (!auth) throw new ApiError({ status: 401, code: 'UNAUTHENTICATED', title: 'Unauthorized', detail: 'Not signed in.' });
  const expiresIn = new Date(auth.accessTokenExpiresAt).getTime() - Date.now();
  if (expiresIn > 5 * 60_000) return;

  const response = await fetch(`${BASE}/api/v1/auth/refresh`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ refreshToken: auth.refreshToken }),
  });
  if (!response.ok) { session.clear(); throw new ApiError(await response.json()); }
  store({ ...(await response.json()), email: auth.email });
}

export interface RequestOptions { headers?: Record<string, string> }

export async function request<T>(method: string, path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
  await ensureFresh();
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      authorization: `Bearer ${auth!.accessToken}`,
      ...(body !== undefined ? { 'content-type': 'application/json' } : {}),
      ...options.headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    const problem: Problem = await response.json().catch(() => ({
      status: response.status, code: 'HTTP_' + response.status, title: response.statusText, detail: response.statusText,
    }));
    throw new ApiError(problem);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const get = <T,>(path: string) => request<T>('GET', path);
export const post = <T,>(path: string, body?: unknown, options?: RequestOptions) => request<T>('POST', path, body, options);
