# PoGo Walker

A Windows desktop tool that sets a real, system-level simulated location on a
tethered iPhone (iOS 26) — map-based teleport, route-following "walk" along real
pedestrian paths, a live speed modifier, and a Pokémon GO cooldown timer in the
header.

> **Read once.** This sets location at the CoreLocation layer via Apple's
> developer/debug services — the same path Xcode uses — so it changes location
> system-wide, including Find My. Using it to play Pokémon GO is a **Niantic ToS
> violation** (not a crime in most jurisdictions). Niantic's detection is
> behavioral: impossible travel, robotic movement, and **automation are the
> loudest flags**. The jitter, speed caps, and cooldown logic here are
> *harm-reduction, not invisibility*. Use a burner account, never your main.

## Architecture

One Tauri app. A React/MapLibre UI talks to a localhost-only FastAPI service
that drives the phone via `pymobiledevice3`. The only out-of-app pieces are
Apple's usbmux driver (from the Apple Devices app / iTunes) and outbound HTTPS
for walking directions + map tiles.

```
PoGoWalker/
  service/                 Python device service (FastAPI + pymobiledevice3)
    pogo_service/
      geo.py               great-circle math (haversine / bearing / destination)
      cooldown.py          community cooldown table + interpolation
      walker.py            route interpolation, live speed, jitter
      routing.py           OpenRouteService foot-walking client (key stays here)
      device.py            pymobiledevice3 wrapper + mock fallback
      tunnel.py            admin tunnel runner + RSD parsing
      state.py             central state machine (teleport / walk / heartbeat)
      main.py              FastAPI endpoints + WebSocket /stream
    tests/                 geo, cooldown, walker
    build_sidecar.py       PyInstaller -> Tauri externalBin
  ui/                      React + TS + Vite + Zustand + Tailwind + MapLibre
    src/components/        TopBar, MapView, ControlPanel, SpeedSlider,
                           Favorites, Joystick, Toast
    src/lib/               api client, speed/zone helpers, cooldown preview
    src/store.ts           Zustand store + WS stream wiring
  src-tauri/               Tauri 2 shell: launches the sidecar + the one UAC
                           elevated tunnel at startup
```

### The one wrinkle (unavoidable on iOS 26)

Developer services need an admin-level tunnel (it spins up a virtual network
interface). The app fires **exactly one UAC prompt at startup** to launch the
tunnel; the UI and FastAPI service run unprivileged and reach the device through
the tunnel's RSD (RemoteServiceDiscovery) address over localhost.

## Prerequisites

- **Apple Devices app or iTunes** (provides the usbmux driver).
- **pymobiledevice3 ≥ 7.0.1** (bundled in the sidecar).
- iPhone with **Developer Mode ON** (Settings → Privacy & Security → Developer
  Mode → toggle → restart).
- **Personalized developer image mounted** — `pymobiledevice3 mounter
  auto-mount` (needs internet on first run for the TSS-signed image). If
  auto-mount can't find the image, create `Xcode_iOS_DDI_Personalized` inside
  the hidden `~/.pymobiledevice3` folder.

## Prove the pipe first (Phase 0)

Nothing else matters until `simulate-location set` moves Find My from the CLI:

```bash
# Developer Mode ON + reboot, then:
pymobiledevice3 mounter auto-mount                 # phone UNLOCKED

# ADMIN shell — leave it open; note the RSD address + port it prints:
pymobiledevice3 lockdown start-tunnel
#   -> RSD Address: fd03:daf1:235f::1   RSD Port: 56870

pymobiledevice3 developer dvt simulate-location set \
    --rsd fd03:daf1:235f::1 56870 -- 40.7128 -74.0060

pymobiledevice3 developer dvt simulate-location clear --rsd <host> <port>
```

On Windows use `python -m pymobiledevice3 ...` if the bare command isn't found.

## Run in development

**Service** (works without an iPhone — falls back to a mock device):

```bash
cd service
pip install -r requirements.txt
# optional: export the RSD endpoint from the tunnel above
#   export POGO_RSD_HOST=fd03:daf1:235f::1 POGO_RSD_PORT=56870
python -m service        # FastAPI on http://127.0.0.1:8723
```

Smoke-test it:

```bash
curl localhost:8723/status
curl -X POST localhost:8723/teleport -H 'content-type: application/json' \
     -d '{"lat":40.7128,"lng":-74.006}'
```

**UI**:

```bash
cd ui
npm install
npm run dev              # http://localhost:1420
```

**Full app** (Tauri shell + sidecar + the UAC tunnel):

```bash
cd ui && npm install && cd ..
npm --prefix ui run tauri dev
```

## Tests

```bash
cd service
pip install -r requirements.txt
python -m pytest          # geo, cooldown, walker
```

## Package as one installer (Phase 5)

```bash
cd service
pip install -r requirements.txt pyinstaller
python build_sidecar.py   # -> src-tauri/binaries/pogo-service-<triple>.exe
cd ..
npm --prefix ui run tauri build
```

## Service API

| Method | Path          | Body                                  | Effect                                            |
|--------|---------------|---------------------------------------|---------------------------------------------------|
| GET    | `/status`     | —                                     | device state, pos, mode, cooldown                 |
| POST   | `/teleport`   | `{lat,lng}`                           | set location → arm cooldown → hold via heartbeat  |
| POST   | `/walk/start` | `{route:[[lat,lng]...], speed_mps}`   | begin route-following walk                        |
| POST   | `/walk/stop`  | —                                     | halt walker, hold position                        |
| POST   | `/speed`      | `{speed_mps}`                         | live speed update                                 |
| POST   | `/reset`      | —                                     | clear simulation → real GPS                       |
| GET    | `/route`      | `?from=lat,lng&to=lat,lng`            | proxy hosted walking-directions (key server-side) |
| GET    | `/favorites`  | —                                     | list saved coords                                 |
| POST   | `/favorites`  | `{name,lat,lng}`                      | save a coord                                      |
| WS     | `/stream`     | —                                     | ~1 Hz status broadcast                            |

## Safety knobs

- **Speed bands** — 🟢 ≤25 km/h, 🟠 25–35 (warning), 🔴 >35 (PoGO ignores
  movement + high flag risk). The in-game lock is ~35 km/h (~10 m/s).
- **Cooldown** — community-estimated table, +10% buffer, rounds up, capped at 2h.
  Advisory only; teleports lock until it clears (configurable).
- **Heartbeat** — re-pushes a held teleport every ~45 s so iOS 26 doesn't drift.
- **Jitter** — ±3 m positional + ±0.1 s timing noise on the walk loop.

## Out of scope for v1

Untethered mode (needs the host tunnel alive), reading true hardware GPS over
the debug tunnel, and offline/embedded routing. See the project spec for the
detail on each.
```
