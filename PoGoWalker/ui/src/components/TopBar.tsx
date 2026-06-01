import { useStore } from "../store";
import { formatMMSS, mpsToKmh } from "../lib/speed";

export default function TopBar() {
  const status = useStore((s) => s.status);
  const wsConnected = useStore((s) => s.connected);

  const cd = status.cooldown_remaining_s;
  const safe = status.safe;

  return (
    <header className="flex items-center gap-6 border-b border-edge bg-panel px-5 py-3">
      {/* Cooldown — the headline */}
      <div className="flex flex-col">
        <span className="text-[10px] font-bold tracking-widest text-muted">
          COOLDOWN
        </span>
        <span
          className={
            "font-mono text-3xl font-bold leading-none " +
            (safe ? "text-green-400" : "text-red-400")
          }
        >
          {safe ? "SAFE TO PLAY" : formatMMSS(cd)}
        </span>
      </div>

      <div className="h-8 w-px bg-edge" />

      <Stat label="MODE" value={modeLabel(status.mode)} />
      <Stat
        label="SPEED"
        value={`${mpsToKmh(status.speed_mps).toFixed(1)} km/h`}
      />
      <Stat
        label="POSITION"
        value={
          status.pos
            ? `${status.pos[0].toFixed(5)}, ${status.pos[1].toFixed(5)}`
            : "—"
        }
        mono
      />

      <div className="ml-auto flex items-center gap-4">
        <Dot
          on={status.device === "connected"}
          label={status.device === "connected" ? "iPhone" : "No device"}
        />
        <Dot on={wsConnected} label={wsConnected ? "Service" : "Offline"} />
      </div>
    </header>
  );
}

function Stat({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex flex-col">
      <span className="text-[10px] font-bold tracking-widest text-muted">
        {label}
      </span>
      <span className={"text-sm " + (mono ? "font-mono" : "")}>{value}</span>
    </div>
  );
}

function Dot({ on, label }: { on: boolean; label: string }) {
  return (
    <div className="flex items-center gap-2 text-xs text-muted">
      <span
        className={
          "h-2.5 w-2.5 rounded-full " + (on ? "bg-green-400" : "bg-red-500")
        }
      />
      {label}
    </div>
  );
}

function modeLabel(m: string): string {
  return m === "walking" ? "Walking" : m === "teleported" ? "Teleported" : "Idle";
}
