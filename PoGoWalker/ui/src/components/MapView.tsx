import { useEffect, useRef } from "react";
import maplibregl from "maplibre-gl";
import { useStore } from "../store";
import { LatLng } from "../lib/api";

// Free demo style. Swap for a MapTiler key style in production:
//   `https://api.maptiler.com/maps/streets/style.json?key=YOUR_KEY`
const STYLE = "https://demotiles.maplibre.org/style.json";
const HOME: [number, number] = [-74.006, 40.7128]; // [lng, lat]

export default function MapView() {
  const mapEl = useRef<HTMLDivElement>(null);
  const map = useRef<maplibregl.Map | null>(null);
  const marker = useRef<maplibregl.Marker | null>(null);
  const pinMarker = useRef<maplibregl.Marker | null>(null);

  const onMapClick = useStore((s) => s.onMapClick);

  // init once
  useEffect(() => {
    if (!mapEl.current || map.current) return;
    const m = new maplibregl.Map({
      container: mapEl.current,
      style: STYLE,
      center: HOME,
      zoom: 14,
    });
    map.current = m;

    m.on("load", () => {
      addLineSource(m, "route-preview", "#2dd4bf", 4);
      addLineSource(m, "traversed", "#7c5cfc", 3);
    });

    m.on("click", (e) => {
      onMapClick([e.lngLat.lat, e.lngLat.lng]);
    });

    // current-position marker
    const el = document.createElement("div");
    el.className =
      "h-4 w-4 rounded-full border-2 border-white bg-accent shadow-[0_0_12px_#2dd4bf]";
    marker.current = new maplibregl.Marker({ element: el });

    return () => {
      m.remove();
      map.current = null;
    };
  }, [onMapClick]);

  // reactive updates
  const pos = useStore((s) => s.status.pos);
  const traversed = useStore((s) => s.traversed);
  const pendingRoute = useStore((s) => s.pendingRoute);
  const pendingTeleport = useStore((s) => s.pendingTeleport);

  useEffect(() => {
    const m = map.current;
    if (!m || !marker.current) return;
    if (pos) {
      marker.current.setLngLat([pos[1], pos[0]]).addTo(m);
    } else {
      marker.current.remove();
    }
  }, [pos]);

  useEffect(() => {
    setLine(map.current, "traversed", traversed);
  }, [traversed]);

  useEffect(() => {
    setLine(map.current, "route-preview", pendingRoute?.polyline ?? []);
  }, [pendingRoute]);

  useEffect(() => {
    const m = map.current;
    if (!m) return;
    if (pendingTeleport) {
      if (!pinMarker.current) {
        pinMarker.current = new maplibregl.Marker({ color: "#ef4444" });
      }
      pinMarker.current.setLngLat([pendingTeleport[1], pendingTeleport[0]]).addTo(m);
    } else {
      pinMarker.current?.remove();
    }
  }, [pendingTeleport]);

  return <div ref={mapEl} className="absolute inset-0" />;
}

function addLineSource(m: maplibregl.Map, id: string, color: string, width: number) {
  m.addSource(id, {
    type: "geojson",
    data: { type: "Feature", geometry: { type: "LineString", coordinates: [] }, properties: {} },
  });
  m.addLayer({
    id,
    type: "line",
    source: id,
    layout: { "line-cap": "round", "line-join": "round" },
    paint: { "line-color": color, "line-width": width, "line-opacity": 0.9 },
  });
}

function setLine(m: maplibregl.Map | null, id: string, pts: LatLng[]) {
  if (!m || !m.getSource(id)) return;
  const src = m.getSource(id) as maplibregl.GeoJSONSource;
  src.setData({
    type: "Feature",
    properties: {},
    geometry: { type: "LineString", coordinates: pts.map(([lat, lng]) => [lng, lat]) },
  });
}
