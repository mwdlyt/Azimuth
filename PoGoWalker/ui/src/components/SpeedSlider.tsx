import { useStore } from "../store";
import { ZONE_COLOR, ZONE_LABEL, zoneForKmh } from "../lib/speed";

export default function SpeedSlider() {
  const speedKmh = useStore((s) => s.speedKmh);
  const setSpeedKmh = useStore((s) => s.setSpeedKmh);
  const commitSpeed = useStore((s) => s.commitSpeed);

  const zone = zoneForKmh(speedKmh);

  return (
    <div>
      <div className="mb-1 flex items-baseline justify-between">
        <span className="text-[10px] font-bold tracking-widest text-muted">SPEED</span>
        <span className="font-mono text-sm" style={{ color: ZONE_COLOR[zone] }}>
          {speedKmh.toFixed(1)} km/h
        </span>
      </div>

      {/* zone bands: green 0-25, amber 25-35, red 35-50 */}
      <div className="mb-1 flex h-1.5 overflow-hidden rounded-full">
        <div className="bg-green-500" style={{ width: `${(25 / 50) * 100}%` }} />
        <div className="bg-amber-500" style={{ width: `${(10 / 50) * 100}%` }} />
        <div className="bg-red-500" style={{ width: `${(15 / 50) * 100}%` }} />
      </div>

      <input
        type="range"
        min={0}
        max={50}
        step={0.5}
        value={speedKmh}
        onChange={(e) => setSpeedKmh(parseFloat(e.target.value))}
        onMouseUp={() => commitSpeed()}
        onTouchEnd={() => commitSpeed()}
        className="w-full accent-accent"
      />

      <p className="mt-1 text-xs" style={{ color: ZONE_COLOR[zone] }}>
        {ZONE_LABEL[zone]}
      </p>
    </div>
  );
}
