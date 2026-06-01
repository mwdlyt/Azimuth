import { ReactNode, useState } from "react";
import { useStore } from "../store";
import SpeedSlider from "./SpeedSlider";
import Favorites from "./Favorites";
import Joystick from "./Joystick";
import { cooldownPreview } from "../lib/cooldown";

export default function ControlPanel() {
  const uiMode = useStore((s) => s.uiMode);
  const setUiMode = useStore((s) => s.setUiMode);
  const status = useStore((s) => s.status);
  const pendingRoute = useStore((s) => s.pendingRoute);
  const pendingTeleport = useStore((s) => s.pendingTeleport);
  const startWalk = useStore((s) => s.startWalk);
  const stopWalk = useStore((s) => s.stopWalk);
  const teleport = useStore((s) => s.teleport);
  const reset = useStore((s) => s.reset);
  const saveFavorite = useStore((s) => s.saveFavorite);
  const speedKmh = useStore((s) => s.speedKmh);

  const [favName, setFavName] = useState("");

  const eta =
    pendingRoute && speedKmh > 0
      ? pendingRoute.distance_m / (speedKmh / 3.6)
      : null;

  return (
    <aside className="flex w-80 flex-col gap-4 overflow-y-auto border-l border-edge bg-panel p-4">
      <h1 className="text-lg font-bold text-accent">PoGo Walker</h1>

      {/* Mode toggle */}
      <div className="flex rounded-lg bg-panel2 p-1">
        <Toggle active={uiMode === "walk"} onClick={() => setUiMode("walk")}>
          Walk
        </Toggle>
        <Toggle active={uiMode === "teleport"} onClick={() => setUiMode("teleport")}>
          Teleport
        </Toggle>
      </div>

      <SpeedSlider />

      {/* Action area depends on mode */}
      {uiMode === "walk" ? (
        <div className="rounded-lg border border-edge bg-panel2 p-3">
          {pendingRoute ? (
            <>
              <Row label="Distance" value={`${(pendingRoute.distance_m / 1000).toFixed(2)} km`} />
              <Row label="ETA" value={eta ? fmtDuration(eta) : "—"} />
              <button className="btn-accent mt-2 w-full" onClick={() => startWalk()}>
                ▶ Start Walk
              </button>
            </>
          ) : (
            <p className="text-xs text-muted">
              Click the map to route a pedestrian path from your current
              location.
            </p>
          )}
          {status.mode === "walking" && (
            <button className="btn-warn mt-2 w-full" onClick={() => stopWalk()}>
              ⏹ Stop Walk
            </button>
          )}
        </div>
      ) : (
        <div className="rounded-lg border border-edge bg-panel2 p-3">
          {pendingTeleport ? (
            <>
              <Row
                label="Target"
                value={`${pendingTeleport[0].toFixed(4)}, ${pendingTeleport[1].toFixed(4)}`}
              />
              {status.pos && (
                <Row
                  label="Est. cooldown"
                  value={cooldownPreview(status.pos, pendingTeleport)}
                />
              )}
              <button
                className="btn-accent mt-2 w-full disabled:opacity-40"
                disabled={!status.safe}
                onClick={() => teleport()}
              >
                ⚡ Teleport
              </button>
              {!status.safe && (
                <p className="mt-1 text-xs text-red-400">
                  Locked until cooldown clears.
                </p>
              )}
            </>
          ) : (
            <p className="text-xs text-muted">
              Click the map to drop a teleport pin.
            </p>
          )}
        </div>
      )}

      <button className="btn-ghost w-full" onClick={() => reset()}>
        ↺ Reset to Real GPS
      </button>

      {/* Save favorite */}
      <div className="flex gap-2">
        <input
          className="input flex-1"
          placeholder="Favorite name"
          value={favName}
          onChange={(e) => setFavName(e.target.value)}
        />
        <button
          className="btn-ghost"
          onClick={() => {
            if (favName.trim()) {
              saveFavorite(favName.trim());
              setFavName("");
            }
          }}
        >
          Save
        </button>
      </div>

      <Favorites />
      <Joystick />
    </aside>
  );
}

function Toggle({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      className={
        "flex-1 rounded-md py-1.5 text-sm font-medium transition " +
        (active ? "bg-accent text-black" : "text-muted hover:text-white")
      }
    >
      {children}
    </button>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between py-0.5 text-sm">
      <span className="text-muted">{label}</span>
      <span className="font-mono">{value}</span>
    </div>
  );
}

function fmtDuration(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.round(seconds % 60);
  return m > 0 ? `${m}m ${s}s` : `${s}s`;
}
