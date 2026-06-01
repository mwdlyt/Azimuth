from pogo_service.cooldown import CAP, cooldown_seconds


def test_short_hop_floor():
    # <= 1 km uses the first row (30s) + 10% buffer, rounded.
    assert cooldown_seconds(0.5) == 33
    assert cooldown_seconds(1.0) == 33


def test_interpolation_midpoint():
    # 3 km sits between (2,60) and (4,120) -> 90s base, +10% = 99.
    assert cooldown_seconds(3.0) == 99


def test_long_jump_caps():
    assert cooldown_seconds(2000) == CAP
    assert cooldown_seconds(9999) == CAP


def test_cap_never_exceeded():
    # The buffer must not push any value past CAP.
    for km in (0, 1, 50, 500, 1500, 5000):
        assert cooldown_seconds(km) <= CAP


def test_buffer_zero():
    assert cooldown_seconds(2.0, buffer=0.0) == 60
