"""
Pokemon GO teleport cooldown estimator.

WARNING: this table is community-reverse-engineered, NOT published by Niantic,
and sources disagree. Always rounds up and applies a safety buffer. Re-verify
against a current community chart before trusting it for long jumps.
"""

# (distance_km, seconds) -- community-estimated, unofficial
COOLDOWN_TABLE: list[tuple[float, int]] = [
    (1, 30), (2, 60), (4, 120), (6, 240), (8, 300), (10, 360),
    (12, 480), (15, 600), (18, 660), (26, 900), (42, 1140),
    (65, 1320), (81, 1500), (100, 1860), (250, 2700), (500, 3600),
    (750, 4800), (1000, 5400), (1500, 7200),
]
CAP = 7200  # 2 hours, regardless of distance


def cooldown_seconds(km: float, buffer: float = 0.10) -> int:
    """
    Estimated cooldown in seconds for a teleport of `km` kilometers.

    Linearly interpolates between table rows, applies `buffer` (default 10%)
    headroom, rounds up, and clamps to CAP.
    """
    if km <= COOLDOWN_TABLE[0][0]:
        base: float = COOLDOWN_TABLE[0][1]
    elif km >= COOLDOWN_TABLE[-1][0]:
        base = CAP
    else:
        base = CAP
        for (k0, s0), (k1, s1) in zip(COOLDOWN_TABLE, COOLDOWN_TABLE[1:]):
            if k0 <= km <= k1:
                base = s0 + (s1 - s0) * (km - k0) / (k1 - k0)
                break
    return min(CAP, round(base * (1 + buffer)))
