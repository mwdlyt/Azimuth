// Speed helpers + Pokemon GO safe-zone bands.

export const KMH_PER_MPS = 3.6;

export type Zone = "green" | "amber" | "red";

// 🟢 up to ~25 km/h, 🟠 25-35 (warning), 🔴 >35 (won't count in-game + flags).
export function zoneForKmh(kmh: number): Zone {
  if (kmh > 35) return "red";
  if (kmh >= 25) return "amber";
  return "green";
}

export const ZONE_COLOR: Record<Zone, string> = {
  green: "#22c55e",
  amber: "#f59e0b",
  red: "#ef4444",
};

export const ZONE_LABEL: Record<Zone, string> = {
  green: "OK",
  amber: "Warning — fast travel may flag",
  red: "Won't count in-game · high flag risk",
};

export const mpsToKmh = (m: number) => m * KMH_PER_MPS;
export const kmhToMps = (k: number) => k / KMH_PER_MPS;

export function formatMMSS(totalSeconds: number): string {
  const s = Math.max(0, Math.round(totalSeconds));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${m}:${r.toString().padStart(2, "0")}`;
}
