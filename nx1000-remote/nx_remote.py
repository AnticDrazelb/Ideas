#!/usr/bin/env python3
"""
nx_remote.py — a Magic-Lantern-style remote controller for Samsung NX Wi-Fi cameras
(NX1000/NX1100/NX20/NX210 and the NX300-era bodies that share the "Remote ViewFinder"
protocol). Pure Python standard library — runs on any laptop, or on a phone via Termux.
No firmware hack, no brick risk: it drives the camera from outside over Wi-Fi using the
exact UPnP/SOAP actions the camera already exposes to Samsung's old phone app.

It gives the NX1000 the features it never shipped with:
  * intervalometer / timelapse           (loops `Shot`)
  * long exposures / bulb beyond the dial (`StartShot` ... wait ... `StopShot`)
  * exposure bracketing, arbitrary width  (`SetEV`/`SetShutterSpeed` then `Shot`)
  * full manual control from the keyboard (`SetISO`/`SetShutterSpeed`/`SetAperture`/`SetEV`)
  * live-view frame grab + optional histogram (the `StreamUrl` from `GetInfomation`)

Protocol source: the reverse-engineered SCPD in ge0rg/samsung-nx-hacks
(xml/Remote-ViewFinder/smp_3_.xml). Action names and signatures there are authoritative:
    Shot() -> AFSHOTRESULT, AVAILSHOTS, RemainRecTime
    StartShot(DriveValue) / StopShot(DriveValue)      # shutter press / release = bulb
    SetShutterSpeed(ShutterSpeedValue) -> Aperture,EV
    SetAperture(ApertureValue) / SetEV(EVValue) / SetISO(ISOValue)
    SetDrive(DriveValue)
    GetInfomation(GPSINFO) -> GETINFORMATIONRESULT, StreamUrl
    X_GetFeatureList() -> FeatureList                 # the camera's valid value tables
    HeartBeat(Duration)                               # keepalive, call periodically
    SetOperationState(StateEvent)                     # put camera into remote mode

WHAT STILL HAS TO BE CONFIRMED ON A REAL NX1000 (the repo's capture is from a cousin
body, the NX3000, so these are the knobs most likely to differ):
  * the RVF service's controlURL + serviceType namespace  -> `discover` finds these live
  * the exact enum strings for ISO/shutter/aperture/EV     -> `info` prints what the
    camera reports via X_GetFeatureList; pass those strings verbatim to the set-* cmds
  * the StateEvent value that enters remote mode           -> --state, default "1"
  * the DriveValue string for a single bulb frame          -> --drive, default "Single"
Run `./nx_remote.py discover --dump` first and send me the output; I'll pin the defaults.
"""

import argparse
import re
import socket
import sys
import threading
import time
import urllib.request
from urllib.parse import urljoin, urlparse

SSDP_ADDR = ("239.255.255.250", 1900)
# The RVF service is identified not by a fixed name (it varies by model) but by the
# fact that its SCPD contains these camera-control actions. We match on that.
RVF_MARKER_ACTIONS = ("StartShot", "Shot", "SetShutterSpeed")


# --------------------------------------------------------------------------- #
# Discovery: find the camera and its Remote-ViewFinder control endpoint.
# --------------------------------------------------------------------------- #
def ssdp_search(timeout=4):
    """Multicast M-SEARCH; return list of device-description LOCATION URLs."""
    msg = (
        "M-SEARCH * HTTP/1.1\r\n"
        "HOST: 239.255.255.250:1900\r\n"
        'MAN: "ssdp:discover"\r\n'
        "MX: 2\r\n"
        "ST: ssdp:all\r\n\r\n"
    ).encode()
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 2)
    s.settimeout(timeout)
    locations = []
    try:
        s.sendto(msg, SSDP_ADDR)
        end = time.time() + timeout
        while time.time() < end:
            try:
                data, addr = s.recvfrom(65535)
            except socket.timeout:
                break
            m = re.search(rb"(?im)^LOCATION:\s*(.+?)\s*$", data)
            if m:
                loc = m.group(1).decode(errors="replace")
                if loc not in locations:
                    locations.append(loc)
    finally:
        s.close()
    return locations


def http_get(url, timeout=8):
    req = urllib.request.Request(url, headers={"User-Agent": "nx-remote/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", errors="replace")


def _services(desc_xml):
    """Yield (serviceType, controlURL, scpdURL) tuples from a device description."""
    for blk in re.findall(r"<service>(.*?)</service>", desc_xml, re.S):
        st = re.search(r"<serviceType>([^<]+)</serviceType>", blk)
        cu = re.search(r"<controlURL>([^<]*)</controlURL>", blk)
        sp = re.search(r"<SCPDURL>([^<]*)</SCPDURL>", blk)
        if st and cu:
            yield st.group(1).strip(), cu.group(1).strip(), (sp.group(1).strip() if sp else "")


def find_camera(ip=None, dump=False):
    """Return (control_url, service_type) for the RVF service, or raise."""
    locations = []
    if ip:
        # Samsung NX WiFi advertises its root description over SSDP; if the user gives an
        # IP we still need the description URL. Try the common ones, then fall back to SSDP.
        for port in (7676, 52235, 1900, 80):
            for path in ("/smp_2_", "/DeviceDescription.xml", "/description.xml", "/"):
                locations.append(f"http://{ip}:{port}{path}")
    else:
        locations = ssdp_search()

    if dump:
        print("[discover] candidate description URLs:")
        for L in locations:
            print("   ", L)

    tried = set()
    for loc in locations:
        if loc in tried:
            continue
        tried.add(loc)
        try:
            desc = http_get(loc)
        except Exception as e:
            if dump:
                print(f"[discover] {loc} -> {e.__class__.__name__}: {e}")
            continue
        base = loc
        fn = re.search(r"<friendlyName>([^<]+)</friendlyName>", desc)
        if dump and fn:
            print(f"[discover] {loc} -> friendlyName={fn.group(1)!r}")
        for st, cu, sp in _services(desc):
            if dump:
                print(f"            service {st}  control={cu}  scpd={sp}")
            # Confirm by fetching the SCPD and checking for the control actions.
            if sp:
                try:
                    scpd = http_get(urljoin(base, sp))
                except Exception:
                    scpd = ""
                if all(a in scpd for a in RVF_MARKER_ACTIONS):
                    return urljoin(base, cu), st
            # Heuristic fallback if SCPD not reachable: serviceType often names it.
            if re.search(r"(?i)rvf|remote|datacontrol|camera", st) and cu:
                return urljoin(base, cu), st
    raise SystemExit(
        "Could not locate the Remote-ViewFinder service.\n"
        "Make sure the camera is in its Wi-Fi 'Remote Viewfinder'/'MobileLink' mode and\n"
        "this machine is on the same network, then run:  nx_remote.py discover --dump\n"
        "and send me the output."
    )


# --------------------------------------------------------------------------- #
# SOAP transport.
# --------------------------------------------------------------------------- #
class Camera:
    def __init__(self, control_url, service_type, drive="Single", verbose=False):
        self.control_url = control_url
        self.service_type = service_type
        self.drive = drive
        self.verbose = verbose
        self._hb_stop = None

    def soap(self, action, args=None, timeout=15):
        args = args or {}
        body_args = "".join(f"<{k}>{v}</{k}>" for k, v in args.items())
        envelope = (
            '<?xml version="1.0" encoding="utf-8"?>'
            '<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" '
            's:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">'
            "<s:Body>"
            f'<u:{action} xmlns:u="{self.service_type}">{body_args}</u:{action}>'
            "</s:Body></s:Envelope>"
        ).encode()
        req = urllib.request.Request(
            self.control_url,
            data=envelope,
            headers={
                "Content-Type": 'text/xml; charset="utf-8"',
                "SOAPACTION": f'"{self.service_type}#{action}"',
                "User-Agent": "nx-remote/1.0",
                "Connection": "close",
            },
            method="POST",
        )
        if self.verbose:
            print(f"  -> {action}({args})")
        with urllib.request.urlopen(req, timeout=timeout) as r:
            raw = r.read().decode("utf-8", errors="replace")
        out = dict(re.findall(r"<([A-Za-z_]+)>([^<]*)</\1>", raw))
        # strip SOAP scaffolding keys
        for k in ("Envelope", "Body"):
            out.pop(k, None)
        if self.verbose:
            print(f"     {out or raw[:200]}")
        return out

    # -- session ----------------------------------------------------------- #
    def enter_remote_mode(self, state="1"):
        try:
            self.soap("SetOperationState", {"StateEvent": state})
        except Exception as e:
            print(f"[warn] SetOperationState failed ({e}); continuing anyway")

    def start_heartbeat(self, period=10):
        def loop():
            while not self._hb_stop.wait(period):
                try:
                    self.soap("HeartBeat", {"Duration": str(period * 1000)})
                except Exception:
                    pass
        self._hb_stop = threading.Event()
        threading.Thread(target=loop, daemon=True).start()

    def stop_heartbeat(self):
        if self._hb_stop:
            self._hb_stop.set()

    # -- primitives -------------------------------------------------------- #
    def info(self):
        out = {}
        try:
            out.update(self.soap("GetInfomation", {"GPSINFO": ""}))
        except Exception as e:
            print(f"[warn] GetInfomation: {e}")
        try:
            fl = self.soap("X_GetFeatureList")
            out["FeatureList"] = fl.get("FeatureList", "")
        except Exception as e:
            print(f"[warn] X_GetFeatureList: {e}")
        return out

    def shot(self):
        return self.soap("Shot")

    def bulb(self, seconds):
        """Open the shutter, hold, close — a true long exposure of any length."""
        self.soap("StartShot", {"DriveValue": self.drive})
        t0 = time.time()
        try:
            while time.time() - t0 < seconds:
                time.sleep(min(0.5, seconds - (time.time() - t0)))
        finally:
            self.soap("StopShot", {"DriveValue": self.drive})
        return {"exposed_s": round(time.time() - t0, 2)}

    def set_iso(self, v):      return self.soap("SetISO", {"ISOValue": v})
    def set_shutter(self, v):  return self.soap("SetShutterSpeed", {"ShutterSpeedValue": v})
    def set_aperture(self, v): return self.soap("SetAperture", {"ApertureValue": v})
    def set_ev(self, v):       return self.soap("SetEV", {"EVValue": v})

    def liveview_url(self):
        out = self.soap("GetInfomation", {"GPSINFO": ""})
        return out.get("StreamUrl", "")


# --------------------------------------------------------------------------- #
# High-level programs: intervalometer, bracketing, live view.
# --------------------------------------------------------------------------- #
def run_interval(cam, count, period):
    print(f"[interval] {count} frames, one every {period}s")
    for i in range(count):
        t0 = time.time()
        r = cam.shot()
        avail = r.get("AVAILSHOTS", "?")
        print(f"  frame {i+1}/{count}  avail_shots={avail}")
        if i < count - 1:
            wait = period - (time.time() - t0)
            if wait > 0:
                time.sleep(wait)
    print("[interval] done")


def run_bracket(cam, ev_list):
    """Shoot one frame at each EV offset. ev_list e.g. ['-2','0','+2'] (camera's own strings)."""
    print(f"[bracket] {len(ev_list)} frames at EV {ev_list}")
    for ev in ev_list:
        cam.set_ev(ev)
        time.sleep(0.3)
        cam.shot()
        print(f"  shot at EV={ev}")
    print("[bracket] done")


def run_liveview(cam, frames, outdir, histogram):
    import os
    url = cam.liveview_url()
    if not url:
        raise SystemExit("Camera returned no StreamUrl (is it in remote-viewfinder mode?)")
    print(f"[liveview] stream = {url}")
    os.makedirs(outdir, exist_ok=True)
    # The NX live view is MJPEG: a stream of JPEGs between 0xFFD8 ... 0xFFD9 markers.
    req = urllib.request.Request(url, headers={"User-Agent": "nx-remote/1.0"})
    buf = b""
    saved = 0
    with urllib.request.urlopen(req, timeout=15) as r:
        while saved < frames:
            chunk = r.read(8192)
            if not chunk:
                break
            buf += chunk
            while True:
                a = buf.find(b"\xff\xd8")
                b = buf.find(b"\xff\xd9", a + 2)
                if a < 0 or b < 0:
                    break
                jpg = buf[a:b + 2]
                buf = buf[b + 2:]
                path = os.path.join(outdir, f"frame_{saved:04d}.jpg")
                with open(path, "wb") as f:
                    f.write(jpg)
                msg = f"  saved {path} ({len(jpg)} B)"
                if histogram:
                    msg += "  " + _histogram(jpg)
                print(msg)
                saved += 1
                if saved >= frames:
                    break
    print(f"[liveview] {saved} frame(s) saved to {outdir}/")


def _histogram(jpg_bytes):
    """A tiny luminance histogram. Uses Pillow if present; else reports it's unavailable."""
    try:
        import io
        from PIL import Image
    except Exception:
        return "(histogram needs Pillow: pip install pillow)"
    try:
        im = Image.open(io.BytesIO(jpg_bytes)).convert("L")
    except Exception:
        return "(frame not decodable)"
    hist = im.histogram()
    buckets = [sum(hist[i:i + 32]) for i in range(0, 256, 32)]
    peak = max(buckets) or 1
    bars = "".join("▁▂▃▄▅▆▇█"[min(7, int(v / peak * 7))] for v in buckets)
    clipped = hist[255] / (im.width * im.height) * 100
    return f"hist[{bars}] clip={clipped:.1f}%"


# --------------------------------------------------------------------------- #
# CLI
# --------------------------------------------------------------------------- #
def build_cam(a):
    control, st = find_camera(ip=a.ip, dump=getattr(a, "dump", False))
    print(f"[camera] control={control}\n[camera] serviceType={st}")
    cam = Camera(control, st, drive=a.drive, verbose=a.verbose)
    cam.enter_remote_mode(a.state)
    cam.start_heartbeat()
    return cam


def main():
    p = argparse.ArgumentParser(description="Remote controller for Samsung NX Wi-Fi cameras")
    p.add_argument("--ip", help="camera IP (skip SSDP discovery)")
    p.add_argument("--drive", default="Single", help="DriveValue for bulb frames (default: Single)")
    p.add_argument("--state", default="1", help="StateEvent to enter remote mode (default: 1)")
    p.add_argument("--verbose", action="store_true")
    sub = p.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("discover", help="find the camera and print its services")
    s.add_argument("--dump", action="store_true", help="print everything found")

    sub.add_parser("info", help="print camera info + feature list (valid ISO/shutter/etc.)")
    sub.add_parser("shot", help="take one photo")

    s = sub.add_parser("bulb", help="long exposure of N seconds")
    s.add_argument("seconds", type=float)

    s = sub.add_parser("interval", help="intervalometer / timelapse")
    s.add_argument("count", type=int)
    s.add_argument("period", type=float, help="seconds between shots")

    s = sub.add_parser("bracket", help="exposure bracket across EV offsets")
    s.add_argument("ev", nargs="+", help="EV strings the camera accepts, e.g. -2 0 +2")

    s = sub.add_parser("set", help="set one manual value")
    s.add_argument("which", choices=["iso", "shutter", "aperture", "ev"])
    s.add_argument("value")

    s = sub.add_parser("liveview", help="grab live-view frames (+ optional histogram)")
    s.add_argument("--frames", type=int, default=1)
    s.add_argument("--out", default="liveview")
    s.add_argument("--histogram", action="store_true")

    a = p.parse_args()

    if a.cmd == "discover":
        control, st = find_camera(ip=a.ip, dump=True)
        print(f"\nRVF control URL : {control}\nRVF serviceType : {st}")
        return

    cam = build_cam(a)
    try:
        if a.cmd == "info":
            for k, v in cam.info().items():
                print(f"{k}: {v}")
        elif a.cmd == "shot":
            print(cam.shot())
        elif a.cmd == "bulb":
            print(cam.bulb(a.seconds))
        elif a.cmd == "interval":
            run_interval(cam, a.count, a.period)
        elif a.cmd == "bracket":
            run_bracket(cam, a.ev)
        elif a.cmd == "set":
            fn = {"iso": cam.set_iso, "shutter": cam.set_shutter,
                  "aperture": cam.set_aperture, "ev": cam.set_ev}[a.which]
            print(fn(a.value))
        elif a.cmd == "liveview":
            run_liveview(cam, a.frames, a.out, a.histogram)
    finally:
        cam.stop_heartbeat()


if __name__ == "__main__":
    main()
