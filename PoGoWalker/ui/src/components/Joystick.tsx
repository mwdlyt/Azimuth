import { PointerEvent as ReactPointerEvent, useRef, useState } from "react";
import { api } from "../lib/api";
import { useStore } from "../store";
import { kmhToMps } from "../lib/speed";

// Free-form walking: holding a direction starts a walk toward a far point on
// that bearing from the current position; releasing stops. Reuses the walker
// loop (and its jitter) rather than a separate push path.
const FAR_M = 2000;
const R = 6371000;

export default function Joystick() {
  const pad = useRef<HTMLDivElement>(null);
  const [knob, setKnob] = useState({ x: 0, y: 0 });
  const status = useStore((s) => s.status);
  const speedKmh = useStore((s) => s.speedKmh);

  const drive = async (nx: number, ny: number) => {
    const from = status.pos;
    if (!from) return;
    // screen y is down; convert dx/dy to a compass bearing (rad)
    const brng = Math.atan2(nx, -ny);
    const lat1 = (from[0] * Math.PI) / 180;
    const lon1 = (from[1] * Math.PI) / 180;
    const dr = FAR_M / R;
    const lat2 = Math.asin(
      Math.sin(lat1) * Math.cos(dr) + Math.cos(lat1) * Math.sin(dr) * Math.cos(brng),
    );
    const lon2 =
      lon1 +
      Math.atan2(
        Math.sin(brng) * Math.sin(dr) * Math.cos(lat1),
        Math.cos(dr) - Math.sin(lat1) * Math.sin(lat2),
      );
    const to: [number, number] = [(lat2 * 180) / Math.PI, (lon2 * 180) / Math.PI];
    await api.walkStart([from, to], kmhToMps(speedKmh));
  };

  const onPointer = (e: ReactPointerEvent) => {
    const el = pad.current;
    if (!el || e.buttons === 0) return;
    const r = el.getBoundingClientRect();
    const cx = r.left + r.width / 2;
    const cy = r.top + r.height / 2;
    let dx = e.clientX - cx;
    let dy = e.clientY - cy;
    const max = r.width / 2 - 14;
    const mag = Math.hypot(dx, dy) || 1;
    if (mag > max) {
      dx = (dx / mag) * max;
      dy = (dy / mag) * max;
    }
    setKnob({ x: dx, y: dy });
    drive(dx / max, dy / max);
  };

  const release = () => {
    setKnob({ x: 0, y: 0 });
    api.walkStop().catch(() => {});
  };

  return (
    <div>
      <span className="text-[10px] font-bold tracking-widest text-muted">
        JOYSTICK
      </span>
      <div className="mt-1 flex justify-center">
        <div
          ref={pad}
          onPointerDown={onPointer}
          onPointerMove={onPointer}
          onPointerUp={release}
          onPointerLeave={release}
          className="relative h-28 w-28 touch-none rounded-full border border-edge bg-panel2"
        >
          <div
            className="absolute left-1/2 top-1/2 h-7 w-7 rounded-full bg-accent shadow-[0_0_10px_#2dd4bf]"
            style={{
              transform: `translate(calc(-50% + ${knob.x}px), calc(-50% + ${knob.y}px))`,
            }}
          />
        </div>
      </div>
      {!status.pos && (
        <p className="mt-1 text-center text-xs text-muted">
          Set a location first.
        </p>
      )}
    </div>
  );
}
