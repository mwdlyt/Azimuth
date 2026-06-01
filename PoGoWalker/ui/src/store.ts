import { create } from "zustand";
import {
  api,
  ApiError,
  Favorite,
  LatLng,
  Status,
  STREAM_URL,
} from "./lib/api";
import { kmhToMps } from "./lib/speed";

export type UiMode = "walk" | "teleport";

interface PendingRoute {
  to: LatLng;
  polyline: LatLng[];
  distance_m: number;
}

interface AppStore {
  // live status from the WS stream
  status: Status;
  connected: boolean; // websocket connected (not the phone)

  // UI-local state
  uiMode: UiMode;
  speedKmh: number;
  favorites: Favorite[];
  pendingRoute: PendingRoute | null;
  pendingTeleport: LatLng | null;
  traversed: LatLng[];
  toast: string | null;

  // actions
  connect: () => void;
  setUiMode: (m: UiMode) => void;
  setSpeedKmh: (kmh: number) => void;
  commitSpeed: () => Promise<void>;
  onMapClick: (p: LatLng) => Promise<void>;
  startWalk: () => Promise<void>;
  stopWalk: () => Promise<void>;
  teleport: () => Promise<void>;
  reset: () => Promise<void>;
  loadFavorites: () => Promise<void>;
  saveFavorite: (name: string) => Promise<void>;
  gotoFavorite: (f: Favorite) => Promise<void>;
  setToast: (t: string | null) => void;
}

const IDLE: Status = {
  pos: null,
  mode: "idle",
  speed_mps: 1.4,
  cooldown_remaining_s: 0,
  safe: true,
  device: "disconnected",
};

export const useStore = create<AppStore>((set, get) => ({
  status: IDLE,
  connected: false,
  uiMode: "walk",
  speedKmh: 5,
  favorites: [],
  pendingRoute: null,
  pendingTeleport: null,
  traversed: [],
  toast: null,

  connect: () => {
    const open = () => {
      const ws = new WebSocket(STREAM_URL);
      ws.onopen = () => set({ connected: true });
      ws.onmessage = (e) => {
        const status = JSON.parse(e.data) as Status;
        set((s) => ({
          status,
          // record the live track when moving
          traversed:
            status.pos && status.mode !== "idle"
              ? [...s.traversed.slice(-2000), status.pos]
              : s.traversed,
        }));
      };
      ws.onclose = () => {
        set({ connected: false });
        setTimeout(open, 1500); // auto-reconnect
      };
      ws.onerror = () => ws.close();
    };
    open();
    get().loadFavorites().catch(() => {});
  },

  setUiMode: (m) => set({ uiMode: m, pendingRoute: null, pendingTeleport: null }),

  setSpeedKmh: (kmh) => set({ speedKmh: kmh }),

  commitSpeed: async () => {
    await api.setSpeed(kmhToMps(get().speedKmh));
  },

  onMapClick: async (p) => {
    const { uiMode, status } = get();
    if (uiMode === "teleport") {
      set({ pendingTeleport: p, pendingRoute: null });
      return;
    }
    // walk mode: need an origin to route from
    const from = status.pos;
    if (!from) {
      set({
        pendingTeleport: p,
        toast: "No current location yet — teleport once to set a start point.",
      });
      return;
    }
    try {
      const polyline = await api.route(from, p);
      const distance_m = polylineLength(polyline);
      set({ pendingRoute: { to: p, polyline, distance_m }, pendingTeleport: null });
    } catch {
      set({ toast: "Routing failed. Check the directions API key." });
    }
  },

  startWalk: async () => {
    const pr = get().pendingRoute;
    if (!pr) return;
    await api.walkStart(pr.polyline, kmhToMps(get().speedKmh));
    set({ pendingRoute: null, traversed: [] });
  },

  stopWalk: async () => {
    await api.walkStop();
  },

  teleport: async () => {
    const p = get().pendingTeleport;
    if (!p) return;
    try {
      await api.teleport(p[0], p[1]);
      set({ pendingTeleport: null, traversed: [] });
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        const d = e.detail as { remaining_s?: number };
        set({ toast: `Cooldown active — ${d.remaining_s ?? "?"}s remaining.` });
      } else {
        set({ toast: "Teleport failed." });
      }
    }
  },

  reset: async () => {
    await api.reset();
    set({ traversed: [], pendingRoute: null, pendingTeleport: null });
  },

  loadFavorites: async () => {
    set({ favorites: await api.favorites() });
  },

  saveFavorite: async (name) => {
    const p = get().status.pos ?? get().pendingTeleport;
    if (!p) {
      set({ toast: "Nothing to save — set a location first." });
      return;
    }
    set({ favorites: await api.addFavorite({ name, lat: p[0], lng: p[1] }) });
  },

  gotoFavorite: async (f) => {
    set({ uiMode: "teleport", pendingTeleport: [f.lat, f.lng] });
  },

  setToast: (t) => set({ toast: t }),
}));

// haversine length of a polyline, meters (kept client-side for the preview)
function polylineLength(poly: LatLng[]): number {
  const R = 6371000;
  let total = 0;
  for (let i = 1; i < poly.length; i++) {
    const [la1, lo1] = poly[i - 1];
    const [la2, lo2] = poly[i];
    const p1 = (la1 * Math.PI) / 180;
    const p2 = (la2 * Math.PI) / 180;
    const dp = ((la2 - la1) * Math.PI) / 180;
    const dl = ((lo2 - lo1) * Math.PI) / 180;
    const h =
      Math.sin(dp / 2) ** 2 +
      Math.cos(p1) * Math.cos(p2) * Math.sin(dl / 2) ** 2;
    total += 2 * R * Math.asin(Math.sqrt(h));
  }
  return total;
}
