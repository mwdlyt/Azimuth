"""
PoGo Walker device service.

A localhost-only FastAPI service that drives a tethered iPhone's simulated
location via pymobiledevice3, with route-following walk, teleport cooldown,
and a 1 Hz WebSocket status stream.
"""

__version__ = "0.1.0"
