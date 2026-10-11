export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";
export const PLAYER_STATE_EVENT = "millionnika:player-state";

export type PlayerStateEventDetail = {
  healthCurrent?: number;
  healthMaximum?: number;
  fightActive?: boolean;
  clear?: boolean;
};

export function publishPlayerState(detail: PlayerStateEventDetail = {}) {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent(PLAYER_STATE_EVENT, { detail }));
  }
}

let pendingCsrf: Promise<Response> | null = null;

// Fetch a fresh identity-bound token before each mutation, including login/logout.
// Share only an in-flight fetch to avoid competing initial antiforgery cookies.
export async function apiFetch(url: string, init: RequestInit = {}): Promise<Response> {
  const method = (init.method ?? "GET").toUpperCase();
  if (["GET", "HEAD", "OPTIONS"].includes(method)) return fetch(url, init);
  if (new URL(url).origin !== new URL(API_URL).origin) throw new Error("Unexpected API origin");
  if (!pendingCsrf) {
    pendingCsrf = fetch(`${API_URL}/api/auth/csrf`, { credentials: "include", cache: "no-store" });
  }
  const pending = pendingCsrf;
  let tokenResponse: Response;
  try { tokenResponse = (await pending).clone(); }
  finally { if (pendingCsrf === pending) pendingCsrf = null; }
  if (!tokenResponse.ok) return tokenResponse;
  const { token } = await tokenResponse.json() as { token: string };
  const headers = new Headers(init.headers);
  headers.set("X-CSRF-Token", token);
  const response = await fetch(url, { ...init, headers, credentials: "include" });
  if (response.ok || response.status === 409 || response.status === 429) {
    publishPlayerState(url.endsWith("/api/auth/logout") ? { clear: true } : {});
  }
  return response;
}

export type ValidationProblem = {
  errors?: Record<string, string[]>;
  message?: string;
};

export async function getErrorMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as ValidationProblem;
    const messages = Object.values(problem.errors ?? {}).flat();
    return messages[0] ?? problem.message ?? "Не удалось выполнить запрос.";
  } catch {
    return "Сервер временно недоступен.";
  }
}
