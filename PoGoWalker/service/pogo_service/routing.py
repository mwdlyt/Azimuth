"""
Walking-directions client.

Default provider: OpenRouteService `foot-walking`. The API key lives here in the
service (never in the front-end bundle); the `/route` endpoint proxies this so
the key never touches the client. Returns a polyline as [(lat, lng), ...].

If no key is configured the client falls back to a straight-line "route" (two
points), so the walker and UI still function for local dev without a provider.
"""

from __future__ import annotations

import logging

import httpx

from .config import settings
from .geo import Point

log = logging.getLogger("pogo.routing")


class RoutingError(RuntimeError):
    pass


async def walking_route(start: Point, end: Point) -> list[Point]:
    """Pedestrian polyline from start to end as [(lat, lng), ...]."""
    if not settings.ors_api_key:
        log.warning("no ORS_API_KEY -> returning straight-line route")
        return [start, end]

    url = f"{settings.ors_base_url}/foot-walking/geojson"
    # ORS takes [lon, lat] pairs.
    body = {"coordinates": [[start[1], start[0]], [end[1], end[0]]]}
    headers = {
        "Authorization": settings.ors_api_key,
        "Content-Type": "application/json",
    }

    async with httpx.AsyncClient(timeout=20.0) as client:
        resp = await client.post(url, json=body, headers=headers)
        if resp.status_code != 200:
            raise RoutingError(
                f"ORS {resp.status_code}: {resp.text[:200]}"
            )
        data = resp.json()

    try:
        coords = data["features"][0]["geometry"]["coordinates"]
    except (KeyError, IndexError) as exc:
        raise RoutingError(f"unexpected ORS response shape: {exc}") from exc

    # GeoJSON is [lon, lat]; convert to (lat, lng).
    return [(lat, lon) for lon, lat in coords]
