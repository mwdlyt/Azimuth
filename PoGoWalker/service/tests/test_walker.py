import asyncio

import pytest

from pogo_service.geo import haversine
from pogo_service.walker import walk_route

# A short straight leg: ~111 m due east near the equator.
A = (0.0, 0.0)
B = (0.0, 0.001)


@pytest.mark.asyncio
async def test_walker_reaches_end():
    pushed: list[tuple[float, float]] = []

    async def push(p):
        pushed.append(p)

    # Fast speed + tiny tick so the test finishes quickly.
    await walk_route(
        [A, B],
        push=push,
        get_speed=lambda: 50.0,
        should_stop=lambda: False,
        tick=0.01,
    )

    assert pushed, "walker should push at least once"
    # Final push settles on the endpoint (within jitter tolerance).
    assert haversine(pushed[-1], B) < 10.0


@pytest.mark.asyncio
async def test_walker_stops_on_signal():
    pushed = []
    stop = {"v": False}

    async def push(p):
        pushed.append(p)
        if len(pushed) >= 3:
            stop["v"] = True

    await asyncio.wait_for(
        walk_route(
            [A, B],
            push=push,
            get_speed=lambda: 1.0,  # slow, so it won't finish before stop
            should_stop=lambda: stop["v"],
            tick=0.01,
        ),
        timeout=2.0,
    )
    # Stopped early -> did not settle on B.
    assert haversine(pushed[-1], B) > 10.0
