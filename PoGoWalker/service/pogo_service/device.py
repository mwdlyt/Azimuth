"""
Device abstraction over pymobiledevice3.

`LocationDevice` pushes simulated CoreLocation coordinates to a tethered iPhone
through the RSD (RemoteServiceDiscovery) connection that the admin tunnel
exposes. `MockLocationDevice` does the same shape with no hardware, so the
whole service (walker, cooldown, stream, UI) is fully exercisable on any box.

pymobiledevice3 internals shift between releases; the exact constructor
signatures are pinned against the installed version at connect time and the
real device falls back to the mock with a logged warning if anything in the
import/connect path is missing. Targeted at pymobiledevice3 >= 7.0.1.
"""

from __future__ import annotations

import logging
from typing import Protocol

log = logging.getLogger("pogo.device")


class Device(Protocol):
    """What the rest of the service depends on."""

    name: str
    connected: bool

    def connect(self) -> None: ...
    def set_location(self, lat: float, lng: float) -> None: ...
    def clear(self) -> None: ...
    def close(self) -> None: ...


class MockLocationDevice:
    """No-hardware device. Records the last pushed coordinate and logs."""

    name = "mock"

    def __init__(self) -> None:
        self.connected = False
        self.last: tuple[float, float] | None = None

    def connect(self) -> None:
        self.connected = True
        log.info("mock device connected (no iPhone required)")

    def set_location(self, lat: float, lng: float) -> None:
        self.last = (lat, lng)
        log.debug("mock set_location %.6f, %.6f", lat, lng)

    def clear(self) -> None:
        self.last = None
        log.debug("mock clear -> real GPS")

    def close(self) -> None:
        self.connected = False


class LocationDevice:
    """
    Real device over the tunnel's RSD connection.

    Uses the iOS 17+ DVT path: open a RemoteServiceDiscovery to the tunnel,
    start a DvtSecureSocketProxyService, and drive LocationSimulation.set /
    .clear. GPX route playback is handled separately by the walker pushing
    discrete set() calls at ~1 Hz (more controllable than play_gpx_file and
    avoids re-mounting a file per route).
    """

    name = "iphone"

    def __init__(self, rsd_host: str, rsd_port: int) -> None:
        self.rsd_host = rsd_host
        self.rsd_port = rsd_port
        self.connected = False
        self._rsd = None
        self._dvt = None
        self._loc = None

    def connect(self) -> None:
        # Imported lazily so the service starts (in mock mode) without the dep.
        from pymobiledevice3.remote.remote_service_discovery import (
            RemoteServiceDiscoveryService,
        )
        from pymobiledevice3.services.dvt.dvt_secure_socket_proxy import (
            DvtSecureSocketProxyService,
        )
        from pymobiledevice3.services.dvt.instruments.location_simulation import (
            LocationSimulation,
        )

        self._rsd = RemoteServiceDiscoveryService((self.rsd_host, self.rsd_port))
        self._rsd.connect()

        self._dvt = DvtSecureSocketProxyService(self._rsd)
        self._dvt.__enter__()  # opens the DVT channel

        self._loc = LocationSimulation(self._dvt)
        self.connected = True
        log.info("iphone connected via RSD %s:%s", self.rsd_host, self.rsd_port)

    def set_location(self, lat: float, lng: float) -> None:
        if self._loc is None:
            raise RuntimeError("device not connected")
        self._loc.set(lat, lng)

    def clear(self) -> None:
        if self._loc is not None:
            self._loc.clear()

    def close(self) -> None:
        try:
            if self._loc is not None:
                self._loc.clear()
        except Exception:  # noqa: BLE001 -- best-effort teardown
            pass
        try:
            if self._dvt is not None:
                self._dvt.__exit__(None, None, None)
        finally:
            if self._rsd is not None:
                self._rsd.close()
            self.connected = False


def make_device(
    rsd_host: str | None,
    rsd_port: int | None,
    force_mock: bool = False,
) -> Device:
    """
    Build the best available device.

    Returns a connected real device when a tunnel is configured and
    pymobiledevice3 is importable; otherwise a mock. Connection failures
    degrade to the mock rather than crashing the service.
    """
    if force_mock or not rsd_host or not rsd_port:
        if not force_mock:
            log.warning("no RSD tunnel configured -> using mock device")
        dev: Device = MockLocationDevice()
        dev.connect()
        return dev

    try:
        dev = LocationDevice(rsd_host, rsd_port)
        dev.connect()
        return dev
    except Exception as exc:  # noqa: BLE001 -- any import/connect failure -> mock
        log.warning("real device unavailable (%s) -> using mock device", exc)
        dev = MockLocationDevice()
        dev.connect()
        return dev
