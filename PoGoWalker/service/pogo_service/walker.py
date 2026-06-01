"""
Route-following walker.

Interpolates a position along a pedestrian polyline at a live-adjustable speed,
pushing ~1 Hz (real-GPS cadence) with small positional + timing jitter. The
jitter and speed handling are harm-reduction against automation detection, not
invisibility -- a perfect constant-speed polyline is exactly what gets flagged.
"""

from __future__ import annotations

import asyncio
import math
import random
from typing import Awaitable, Callable

from .geo import Point, bearing, destination, haversine

# Meters per degree of latitude (constant enough for jitter purposes).
_M_PER_DEG_LAT = 111_320.0


def _jitter(pos: Point, meters: float = 3.0) -> Point:
    """Add up to +/- `meters` of positional noise to humanize the track."""
    dlat = random.uniform(-meters, meters) / _M_PER_DEG_LAT
    # Longitude degrees shrink with latitude.
    cos_lat = max(0.01, abs(math.cos(math.radians(pos[0]))))
    dlon = random.uniform(-meters, meters) / (_M_PER_DEG_LAT * cos_lat)
    return (pos[0] + dlat, pos[1] + dlon)


async def walk_route(
    route: list[Point],
    push: Callable[[Point], Awaitable[None]],
    get_speed: Callable[[], float],
    should_stop: Callable[[], bool],
    tick: float = 1.0,
) -> None:
    """
    Walk `route` until its end or `should_stop()`.

    - `push(pos)`        : async, sends a coordinate to the device.
    - `get_speed()`      : returns the current speed in m/s (live modifier).
    - `should_stop()`    : returns True to halt and hold position.
    - `tick`             : nominal seconds between pushes (jittered +/-0.1 s).
    """
    if len(route) < 2:
        if route:
            await push(route[0])
        return

    # Precompute segments with cumulative start-offset for O(1)-ish lookup.
    segs: list[tuple[Point, float, float, float]] = []  # (start, offset, len, brng)
    offset = 0.0
    for a, b in zip(route, route[1:]):
        d = haversine(a, b)
        segs.append((a, offset, d, bearing(a, b)))
        offset += d
    total = offset

    traveled = 0.0
    seg_i = 0
    while traveled < total and not should_stop():
        v = max(0.0, get_speed())
        traveled = min(traveled + v * tick, total)

        # Advance the segment cursor to the one containing `traveled`.
        while seg_i + 1 < len(segs) and segs[seg_i][1] + segs[seg_i][2] < traveled:
            seg_i += 1
        a, seg_off, _seg_len, brng = segs[seg_i]
        pos = destination(a, brng, traveled - seg_off)

        await push(_jitter(pos))
        await asyncio.sleep(max(0.05, tick + random.uniform(-0.1, 0.1)))

    # Settle on the final vertex if we ran to completion.
    if traveled >= total and not should_stop():
        await push(route[-1])
