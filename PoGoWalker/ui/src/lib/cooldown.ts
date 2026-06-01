// Client-side mirror of the service cooldown table, for instant teleport
// previews. The service remains the source of truth on /teleport.
import { LatLng } from "./api";
import { formatMMSS } from "./speed";

const TABLE: [number, number][] = [
  [1, 30], [2, 60], [4, 120], [6, 240], [8, 300], [10, 360],
  [12, 480], [15, 600], [18, 660], [26, 900], [42, 1140],
  [65, 1320], [81, 1500], [100, 1860], [250, 2700], [500, 3600],
  [750, 4800], [1000, 5400], [1500, 7200],
];
const CAP = 7200;

export function cooldownSeconds(km: number, buffer = 0.1): number {
  let base: number;
  if (km <= TABLE[0][0]) base = TABLE[0][1];
  else if (km >= TABLE[TABLE.length - 1][0]) base = CAP;
  else {
    base = CAP;
    for (let i = 0; i < TABLE.length - 1; i++) {
      const [k0, s0] = TABLE[i];
      const [k1, s1] = TABLE[i + 1];
      if (km >= k0 && km <= k1) {
        base = s0 + ((s1 - s0) * (km - k0)) / (k1 - k0);
        break;
      }
    }
  }
  return Math.min(CAP, Math.round(base * (1 + buffer)));
}

function haversineKm(a: LatLng, b: LatLng): number {
  const R = 6371;
  const p1 = (a[0] * Math.PI) / 180;
  const p2 = (b[0] * Math.PI) / 180;
  const dp = ((b[0] - a[0]) * Math.PI) / 180;
  const dl = ((b[1] - a[1]) * Math.PI) / 180;
  const h =
    Math.sin(dp / 2) ** 2 + Math.cos(p1) * Math.cos(p2) * Math.sin(dl / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(h));
}

export function cooldownPreview(from: LatLng, to: LatLng): string {
  const km = haversineKm(from, to);
  const cd = cooldownSeconds(km);
  return `${formatMMSS(cd)}  (${km.toFixed(1)} km)`;
}
