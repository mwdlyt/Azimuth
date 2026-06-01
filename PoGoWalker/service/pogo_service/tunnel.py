"""
Admin tunnel helper.

iOS 17+ (so iOS 26) developer services need an elevated tunnel that spins up a
virtual network interface. The packaged app launches this elevated once at
startup (the single UAC prompt); this helper can also start it from Python for
local dev and parse the RSD (RemoteServiceDiscovery) host/port from its output.

Only the tunnel needs admin; everything else (this service, the walker, the UI)
runs unprivileged and reaches the device through the RSD address over
localhost.
"""

from __future__ import annotations

import logging
import re
import subprocess
import sys
import threading

log = logging.getLogger("pogo.tunnel")

# Matches lines like "RSD Address: fd03:daf1:235f::1" / "RSD Port: 56870"
_ADDR_RE = re.compile(r"RSD Address:\s*([0-9a-fA-F:.]+)")
_PORT_RE = re.compile(r"RSD Port:\s*(\d+)")


class TunnelHandle:
    """A running `start-tunnel` subprocess with its parsed RSD endpoint."""

    def __init__(self, proc: subprocess.Popen) -> None:
        self.proc = proc
        self.rsd_host: str | None = None
        self.rsd_port: int | None = None
        self._ready = threading.Event()

    def wait_ready(self, timeout: float = 30.0) -> bool:
        """Block until the RSD host+port have been parsed, or timeout."""
        return self._ready.wait(timeout)

    def stop(self) -> None:
        if self.proc.poll() is None:
            self.proc.terminate()


def start_tunnel() -> TunnelHandle:
    """
    Spawn `pymobiledevice3 lockdown start-tunnel` and scrape its RSD endpoint.

    NOTE: this process must run elevated (admin) to create the tunnel interface.
    In the packaged app the elevation is handled by the Tauri shell at startup;
    when run from a normal dev shell you must already be in an admin terminal.
    """
    cmd = [sys.executable, "-m", "pymobiledevice3", "lockdown", "start-tunnel"]
    log.info("starting tunnel: %s", " ".join(cmd))
    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
    )
    handle = TunnelHandle(proc)

    def _pump() -> None:
        assert proc.stdout is not None
        for line in proc.stdout:
            log.debug("tunnel: %s", line.rstrip())
            if (m := _ADDR_RE.search(line)) and handle.rsd_host is None:
                handle.rsd_host = m.group(1)
            if (m := _PORT_RE.search(line)) and handle.rsd_port is None:
                handle.rsd_port = int(m.group(1))
            if handle.rsd_host and handle.rsd_port and not handle._ready.is_set():
                log.info(
                    "tunnel ready: RSD %s:%s", handle.rsd_host, handle.rsd_port
                )
                handle._ready.set()

    threading.Thread(target=_pump, name="tunnel-reader", daemon=True).start()
    return handle
