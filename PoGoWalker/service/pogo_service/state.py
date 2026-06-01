"""
Shared application state.

Owns the device handle, the current maintained position, the active mode, the
walker task, the teleport-hold heartbeat, and the cooldown clock. All mutation
goes through here so the API handlers and the stream stay consistent.
"""

from __future__ import annotations

import asyncio
import logging
import time
from dataclasses import dataclass, field
from typing import Literal

from .config import settings
from .cooldown import cooldown_seconds
from .device import Device, make_device
from .geo import Point, haversine
from .walker import walk_route

log = logging.getLogger("pogo.state")

Mode = Literal["idle", "walking", "teleported"]


@dataclass
class Favorite:
    name: str
    lat: float
    lng: float


class AppState:
    def __init__(self) -> None:
        self.device: Device | None = None
        self.pos: Point | None = None
        self.mode: Mode = "idle"
        self.speed_mps: float = settings.default_speed_mps
        self.cooldown_until: float = 0.0  # epoch seconds
        self.favorites: list[Favorite] = []

        self._lock = asyncio.Lock()
        self._walk_task: asyncio.Task | None = None
        self._heartbeat_task: asyncio.Task | None = None
        self._stop_walk = False

    # -- lifecycle -------------------------------------------------------

    def attach_device(self) -> None:
        self.device = make_device(
            settings.rsd_host, settings.rsd_port, settings.use_mock_device
        )

    async def shutdown(self) -> None:
        await self.stop_walk()
        self._cancel_heartbeat()
        if self.device is not None:
            self.device.close()

    # -- derived ---------------------------------------------------------

    @property
    def cooldown_remaining_s(self) -> int:
        return max(0, round(self.cooldown_until - time.time()))

    @property
    def safe(self) -> bool:
        return self.cooldown_remaining_s == 0

    @property
    def device_status(self) -> str:
        return "connected" if (self.device and self.device.connected) else "disconnected"

    def snapshot(self) -> dict:
        return {
            "pos": list(self.pos) if self.pos else None,
            "mode": self.mode,
            "speed_mps": round(self.speed_mps, 3),
            "cooldown_remaining_s": self.cooldown_remaining_s,
            "safe": self.safe,
            "device": self.device_status,
        }

    # -- device push -----------------------------------------------------

    async def _push(self, pos: Point) -> None:
        """Set the device location and record it as the maintained position."""
        if self.device is None:
            return
        # pymobiledevice3 calls are blocking; keep the loop responsive.
        await asyncio.to_thread(self.device.set_location, pos[0], pos[1])
        self.pos = pos

    # -- teleport --------------------------------------------------------

    async def teleport(self, lat: float, lng: float) -> dict:
        """Jump to a point, compute + arm the cooldown, and hold via heartbeat."""
        async with self._lock:
            if not self.safe and settings.lock_during_cooldown:
                raise CooldownActive(self.cooldown_remaining_s)

            await self.stop_walk()
            target: Point = (lat, lng)

            dist_m = haversine(self.pos, target) if self.pos else 0.0
            cd = cooldown_seconds(dist_m / 1000.0, settings.cooldown_buffer)

            await self._push(target)
            self.mode = "teleported"
            self.cooldown_until = time.time() + cd
            self._start_heartbeat()

            log.info("teleport %.5f,%.5f dist=%.0fm cooldown=%ss", lat, lng, dist_m, cd)
            return {"distance_m": round(dist_m, 1), "cooldown_s": cd}

    # -- walk ------------------------------------------------------------

    async def start_walk(self, route: list[Point], speed_mps: float | None) -> None:
        async with self._lock:
            await self.stop_walk()
            self._cancel_heartbeat()  # walking re-pushes continuously
            if speed_mps is not None:
                self.speed_mps = speed_mps
            self._stop_walk = False
            self.mode = "walking"
            if route:
                self.pos = self.pos or route[0]
            self._walk_task = asyncio.create_task(self._run_walk(route))

    async def _run_walk(self, route: list[Point]) -> None:
        try:
            await walk_route(
                route,
                push=self._push,
                get_speed=lambda: self.speed_mps,
                should_stop=lambda: self._stop_walk,
                tick=settings.walk_tick_s,
            )
        except asyncio.CancelledError:
            raise
        except Exception:  # noqa: BLE001
            log.exception("walk loop crashed")
        finally:
            if self.mode == "walking":
                # Reached the end (or errored) without an explicit teleport.
                self.mode = "teleported" if self.pos else "idle"
                self._start_heartbeat()

    async def stop_walk(self) -> None:
        self._stop_walk = True
        task = self._walk_task
        self._walk_task = None
        if task and not task.done():
            task.cancel()
            try:
                await task
            except asyncio.CancelledError:
                pass

    def set_speed(self, speed_mps: float) -> None:
        self.speed_mps = max(0.0, speed_mps)

    # -- reset -----------------------------------------------------------

    async def reset(self) -> None:
        async with self._lock:
            await self.stop_walk()
            self._cancel_heartbeat()
            if self.device is not None:
                await asyncio.to_thread(self.device.clear)
            self.pos = None
            self.mode = "idle"
            self.cooldown_until = 0.0
            log.info("reset -> real GPS")

    # -- heartbeat (teleport hold) --------------------------------------

    def _start_heartbeat(self) -> None:
        self._cancel_heartbeat()
        if settings.heartbeat_s <= 0:
            return
        self._heartbeat_task = asyncio.create_task(self._heartbeat())

    def _cancel_heartbeat(self) -> None:
        if self._heartbeat_task and not self._heartbeat_task.done():
            self._heartbeat_task.cancel()
        self._heartbeat_task = None

    async def _heartbeat(self) -> None:
        """Re-push the held position so iOS 26 doesn't drift back to real GPS."""
        try:
            while True:
                await asyncio.sleep(settings.heartbeat_s)
                if self.pos is not None and self.mode == "teleported":
                    await self._push(self.pos)
                    log.debug("heartbeat re-push %.5f,%.5f", *self.pos)
        except asyncio.CancelledError:
            pass


class CooldownActive(Exception):
    """Raised when a teleport is attempted during an active cooldown."""

    def __init__(self, remaining_s: int) -> None:
        self.remaining_s = remaining_s
        super().__init__(f"cooldown active, {remaining_s}s remaining")


state = AppState()
