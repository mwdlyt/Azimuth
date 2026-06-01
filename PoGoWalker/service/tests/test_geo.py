import math

from pogo_service.geo import bearing, destination, haversine, route_length

NYC = (40.7128, -74.0060)
PHILLY = (39.9526, -75.1652)


def test_haversine_zero():
    assert haversine(NYC, NYC) == 0.0


def test_haversine_known_distance():
    # NYC -> Philadelphia is ~130 km.
    d = haversine(NYC, PHILLY)
    assert 125_000 < d < 135_000


def test_destination_roundtrip():
    # Walking `dist` along the bearing toward b lands ~on b.
    d = haversine(NYC, PHILLY)
    b = bearing(NYC, PHILLY)
    end = destination(NYC, b, d)
    assert haversine(end, PHILLY) < 1.0  # within a meter


def test_destination_north():
    # Heading due north 111.32 km ~= 1 degree of latitude.
    end = destination((0.0, 0.0), 0.0, 111_320.0)
    assert abs(end[0] - 1.0) < 0.01
    assert abs(end[1]) < 1e-6


def test_route_length():
    route = [NYC, PHILLY, NYC]
    assert math.isclose(route_length(route), 2 * haversine(NYC, PHILLY), rel_tol=1e-9)
