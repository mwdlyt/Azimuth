"""Great-circle geo math. All distances in meters, bearings in radians."""

import math

R = 6371000.0  # mean Earth radius, meters

Point = tuple[float, float]  # (lat, lng)


def haversine(a: Point, b: Point) -> float:
    """Distance in meters between two (lat, lng) points."""
    (lat1, lon1), (lat2, lon2) = a, b
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dp = math.radians(lat2 - lat1)
    dl = math.radians(lon2 - lon1)
    h = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * R * math.asin(math.sqrt(h))


def bearing(a: Point, b: Point) -> float:
    """Initial bearing in radians from a to b."""
    (lat1, lon1), (lat2, lon2) = a, b
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dl = math.radians(lon2 - lon1)
    y = math.sin(dl) * math.cos(p2)
    x = math.cos(p1) * math.sin(p2) - math.sin(p1) * math.cos(p2) * math.cos(dl)
    return math.atan2(y, x)


def destination(a: Point, brng: float, dist: float) -> Point:
    """Point reached from a, heading brng (radians), dist meters."""
    lat1, lon1 = math.radians(a[0]), math.radians(a[1])
    dr = dist / R
    lat2 = math.asin(
        math.sin(lat1) * math.cos(dr) + math.cos(lat1) * math.sin(dr) * math.cos(brng)
    )
    lon2 = lon1 + math.atan2(
        math.sin(brng) * math.sin(dr) * math.cos(lat1),
        math.cos(dr) - math.sin(lat1) * math.sin(lat2),
    )
    return (math.degrees(lat2), math.degrees(lon2))


def route_length(route: list[Point]) -> float:
    """Total length in meters of a polyline."""
    return sum(haversine(a, b) for a, b in zip(route, route[1:]))
