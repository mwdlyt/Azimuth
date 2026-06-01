"""
FastAPI service for PoGo Walker. Localhost only.

Endpoints (see README / spec section 3):
  GET  /status      device state, pos, mode, cooldown
  POST /teleport    set location + arm cooldown
  POST /walk/start  begin route-following walk
  POST /walk/stop   halt walker, hold position
  POST /speed       live speed update
  POST /reset       clear simulation -> real GPS
  GET  /route       proxy hosted walking-directions (key stays server-side)
  WS   /stream      ~1 Hz status broadcast
"""

from __future__ import annotations

import asyncio
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Query, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from .config import settings
from .routing import RoutingError, walking_route
from .state import CooldownActive, Favorite, state

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(name)s %(message)s")
log = logging.getLogger("pogo.api")


@asynccontextmanager
async def lifespan(app: FastAPI):
    state.attach_device()
    log.info("device: %s (%s)", state.device.name, state.device_status)
    yield
    await state.shutdown()


app = FastAPI(title="PoGo Walker Service", version="0.1.0", lifespan=lifespan)

# The UI runs from the Tauri shell / localhost dev server.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:1420", "http://127.0.0.1:1420", "tauri://localhost"],
    allow_methods=["*"],
    allow_headers=["*"],
)


# -- request models ------------------------------------------------------


class TeleportBody(BaseModel):
    lat: float = Field(ge=-90, le=90)
    lng: float = Field(ge=-180, le=180)


class WalkStartBody(BaseModel):
    route: list[tuple[float, float]] = Field(min_length=1)
    speed_mps: float | None = Field(default=None, ge=0)


class SpeedBody(BaseModel):
    speed_mps: float = Field(ge=0)


class FavoriteBody(BaseModel):
    name: str
    lat: float = Field(ge=-90, le=90)
    lng: float = Field(ge=-180, le=180)


# -- endpoints -----------------------------------------------------------


@app.get("/status")
async def status() -> dict:
    return state.snapshot()


@app.post("/teleport")
async def teleport(body: TeleportBody) -> dict:
    try:
        result = await state.teleport(body.lat, body.lng)
    except CooldownActive as exc:
        raise HTTPException(
            status_code=409,
            detail={"error": "cooldown_active", "remaining_s": exc.remaining_s},
        )
    return {**state.snapshot(), **result}


@app.post("/walk/start")
async def walk_start(body: WalkStartBody) -> dict:
    route = [(lat, lng) for lat, lng in body.route]
    await state.start_walk(route, body.speed_mps)
    return state.snapshot()


@app.post("/walk/stop")
async def walk_stop() -> dict:
    await state.stop_walk()
    if state.mode == "walking":
        state.mode = "teleported" if state.pos else "idle"
    return state.snapshot()


@app.post("/speed")
async def speed(body: SpeedBody) -> dict:
    state.set_speed(body.speed_mps)
    return state.snapshot()


@app.post("/reset")
async def reset() -> dict:
    await state.reset()
    return state.snapshot()


@app.get("/route")
async def route(
    from_: str = Query(alias="from"),
    to: str = Query(...),
) -> dict:
    """`?from=lat,lng&to=lat,lng` -> pedestrian polyline [[lat,lng], ...]."""
    try:
        fy, fx = (float(v) for v in from_.split(","))
        ty, tx = (float(v) for v in to.split(","))
    except ValueError:
        raise HTTPException(400, "from/to must be 'lat,lng'")
    try:
        poly = await walking_route((fy, fx), (ty, tx))
    except RoutingError as exc:
        raise HTTPException(502, f"routing failed: {exc}")
    return {"route": [list(p) for p in poly]}


@app.get("/favorites")
async def get_favorites() -> dict:
    return {"favorites": [vars(f) for f in state.favorites]}


@app.post("/favorites")
async def add_favorite(body: FavoriteBody) -> dict:
    state.favorites.append(Favorite(body.name, body.lat, body.lng))
    return {"favorites": [vars(f) for f in state.favorites]}


@app.websocket("/stream")
async def stream(ws: WebSocket) -> None:
    await ws.accept()
    try:
        while True:
            await ws.send_json(state.snapshot())
            await asyncio.sleep(settings.stream_tick_s)
    except WebSocketDisconnect:
        pass


def run() -> None:
    import uvicorn

    uvicorn.run(app, host=settings.host, port=settings.port, log_level="info")


if __name__ == "__main__":
    run()
