// Thin client for the local pogo-service. Base URL is localhost-only.

export const SERVICE = "http://127.0.0.1:8723";
export const STREAM_URL = "ws://127.0.0.1:8723/stream";

export type LatLng = [number, number]; // [lat, lng]
export type Mode = "idle" | "walking" | "teleported";

export interface Status {
  pos: LatLng | null;
  mode: Mode;
  speed_mps: number;
  cooldown_remaining_s: number;
  safe: boolean;
  device: "connected" | "disconnected";
}

export interface TeleportResult extends Status {
  distance_m: number;
  cooldown_s: number;
}

export interface Favorite {
  name: string;
  lat: number;
  lng: number;
}

async function post<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(SERVICE + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    const detail = await res.json().catch(() => ({}));
    throw new ApiError(res.status, detail);
  }
  return res.json();
}

export class ApiError extends Error {
  constructor(public status: number, public detail: unknown) {
    super(`api ${status}`);
  }
}

export const api = {
  status: (): Promise<Status> => fetch(SERVICE + "/status").then((r) => r.json()),

  teleport: (lat: number, lng: number): Promise<TeleportResult> =>
    post("/teleport", { lat, lng }),

  walkStart: (route: LatLng[], speed_mps?: number): Promise<Status> =>
    post("/walk/start", { route, speed_mps }),

  walkStop: (): Promise<Status> => post("/walk/stop"),

  setSpeed: (speed_mps: number): Promise<Status> => post("/speed", { speed_mps }),

  reset: (): Promise<Status> => post("/reset"),

  route: async (from: LatLng, to: LatLng): Promise<LatLng[]> => {
    const q = `from=${from[0]},${from[1]}&to=${to[0]},${to[1]}`;
    const res = await fetch(`${SERVICE}/route?${q}`);
    if (!res.ok) throw new ApiError(res.status, await res.text());
    return (await res.json()).route as LatLng[];
  },

  favorites: (): Promise<Favorite[]> =>
    fetch(SERVICE + "/favorites").then((r) => r.json()).then((d) => d.favorites),

  addFavorite: (f: Favorite): Promise<Favorite[]> =>
    post<{ favorites: Favorite[] }>("/favorites", f).then((d) => d.favorites),
};
