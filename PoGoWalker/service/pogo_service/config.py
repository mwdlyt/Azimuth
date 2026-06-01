"""Service configuration, sourced from environment variables."""

import os
from dataclasses import dataclass


def _get_bool(name: str, default: bool) -> bool:
    val = os.getenv(name)
    if val is None:
        return default
    return val.strip().lower() in ("1", "true", "yes", "on")


def _get_float(name: str, default: float) -> float:
    val = os.getenv(name)
    return float(val) if val else default


def _get_int(name: str, default: int) -> int:
    val = os.getenv(name)
    return int(val) if val else default


@dataclass
class Settings:
    # FastAPI bind -- localhost only, never expose this.
    host: str = os.getenv("POGO_HOST", "127.0.0.1")
    port: int = _get_int("POGO_PORT", 8723)

    # Device tunnel. The RSD (RemoteServiceDiscovery) address + port printed by
    # `pymobiledevice3 lockdown start-tunnel`. If unset, the service either
    # auto-discovers (if a tunnel is running) or falls back to the mock device.
    rsd_host: str | None = os.getenv("POGO_RSD_HOST") or None
    rsd_port: int | None = _get_int("POGO_RSD_PORT", 0) or None

    # Force the in-memory mock device (no iPhone required). Auto-enabled when
    # pymobiledevice3 is unavailable.
    use_mock_device: bool = _get_bool("POGO_USE_MOCK", False)

    # Heartbeat: re-push a held teleport position every N seconds so iOS 26
    # doesn't drift back to real GPS. 0 disables.
    heartbeat_s: float = _get_float("POGO_HEARTBEAT_S", 45.0)

    # Walk loop tick (seconds). ~1 Hz matches real GPS cadence.
    walk_tick_s: float = _get_float("POGO_WALK_TICK_S", 1.0)

    # Stream broadcast interval.
    stream_tick_s: float = _get_float("POGO_STREAM_TICK_S", 1.0)

    # Default speeds (m/s).
    default_speed_mps: float = _get_float("POGO_DEFAULT_SPEED", 1.4)  # ~5 km/h
    # In-game hard speed lock (~35 km/h). Above this PoGO ignores movement.
    speed_lock_mps: float = _get_float("POGO_SPEED_LOCK", 10.0)

    # Routing provider key (kept server-side, never shipped to the client).
    ors_api_key: str | None = os.getenv("ORS_API_KEY") or None
    ors_base_url: str = os.getenv(
        "ORS_BASE_URL", "https://api.openrouteservice.org/v2/directions"
    )

    # Cooldown safety buffer (fraction).
    cooldown_buffer: float = _get_float("POGO_COOLDOWN_BUFFER", 0.10)
    # Refuse teleports while a cooldown is active (recommended on).
    lock_during_cooldown: bool = _get_bool("POGO_LOCK_DURING_COOLDOWN", True)


settings = Settings()
