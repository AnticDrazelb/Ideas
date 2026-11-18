# nx1000-remote

**Magic-Lantern-style features for the Samsung NX1000 — over Wi-Fi, no firmware hack.**

The NX1000's firmware is a dead end (it runs VxWorks on a DRIMe III chip; nobody has
ever run custom code on it — see `../nx.md`). But the camera already exposes a rich
remote-control API over Wi-Fi: Samsung's **Remote ViewFinder** protocol, which is just
UPnP/SOAP. The old phone app is gone, but the camera still speaks it. This tool talks
to it directly and adds the features the NX1000 never shipped with:

| Feature | How |
|---|---|
| **Intervalometer / timelapse** | loops the `Shot` action on a timer |
| **Bulb / long exposure, any length** | `StartShot` → hold N seconds → `StopShot` |
| **Exposure bracketing, any width** | `SetEV` then `Shot`, across your EV list |
| **Full manual control** | `SetISO` / `SetShutterSpeed` / `SetAperture` / `SetEV` |
| **Live view + histogram / clip warning** | pulls the MJPEG `StreamUrl`, scores each frame |

No brick risk: it only sends the camera commands it already accepts. Pure Python
standard library (Pillow optional, only for the histogram). Runs on a laptop, or on a
phone via Termux.

## Protocol provenance

Every action and signature comes from the reverse-engineered service description in
[`ge0rg/samsung-nx-hacks`](https://github.com/ge0rg/samsung-nx-hacks)
(`xml/Remote-ViewFinder/smp_3_.xml`) — e.g. `StartShot(DriveValue)`,
`SetShutterSpeed(ShutterSpeedValue)`, `GetInfomation(GPSINFO) -> StreamUrl`. The tool
doesn't hard-code camera value tables; it asks the camera for them at runtime via
`X_GetFeatureList` (the `info` command).

## Use

```sh
# 1. On the camera: start Wi-Fi "Remote Viewfinder" / "MobileLink", join the same network.
# 2. Find it and confirm the control endpoint:
./nx_remote.py discover --dump

# 3. Ask the camera which ISO/shutter/EV strings it actually accepts:
./nx_remote.py info

# 4. Do things:
./nx_remote.py shot                         # one frame
./nx_remote.py bulb 30                       # 30-second exposure
./nx_remote.py interval 300 5                # 300 frames, one every 5 s (25 min timelapse)
./nx_remote.py bracket -2 0 +2               # 3-frame EV bracket (use strings `info` reports)
./nx_remote.py set iso ISO400                # manual control
./nx_remote.py liveview --frames 20 --histogram
```

If discovery can't find the camera, pass its IP: `./nx_remote.py --ip 192.168.0.1 info`.

## Status — honest

The controller is **structurally complete and tested against the documented protocol**
(discovery, SOAP round-trips, bulb timing, MJPEG carving and histogram all verified with
a local mock camera). What it has **not** yet met is a real NX1000, and the repo's
protocol capture is from a cousin body (NX3000), so four values may need pinning:

- the RVF service's `controlURL` + `serviceType` → `discover` resolves these live;
- the exact ISO/shutter/aperture/EV enum strings → `info` prints them;
- `--state` — the `SetOperationState` value that enters remote mode (default `1`);
- `--drive` — the `DriveValue` used to open/close a bulb frame (default `Single`).

Run `./nx_remote.py discover --dump` and `./nx_remote.py info` against your camera and
send me the output; I'll pin the defaults so it's plug-and-play.
