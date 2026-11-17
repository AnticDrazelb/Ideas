# NX

*Can a Samsung NX1000 get a Magic Lantern–style firmware? Short answer, the reasons,
and three things that could actually work.*

---

## Short answer

**Not realistically, and nobody has done it.** No one has ever run their own code on
an NX1000. The camera doesn't run Linux (see below). Every Samsung NX hack you'll find
online is for other models.

## Why the NX1000 is the hard one

Samsung's NX line split down the middle, and the NX1000 sits on the wrong side:

| Model(s) | Chip | OS | Hack status |
|---|---|---|---|
| NX20, NX200, NX210, **NX1000**, NX1100 | DRIMe III | Firmware contains a **VxWorks** copyright (not Linux) | Nothing. No code execution has ever been shown |
| NX300, NX2000, NX30 | DRIMe IV | Linux (Tizen 1.0) | Rooted: telnet, scripts run from the SD card, firmware mods |
| NX500, NX1 | DRIMe V | Linux (Tizen) | **NX-KS mods**: the real "Magic Lantern for Samsung" |

The table comes from the community device list (ge0rg/samsung-nx-hacks). It lists the
NX1000 as DRIMe III with "VXWorks copyright in firmware" and Linux ❌. The project has
mostly been inactive since 2015.

The Linux models were easy to hack for three reasons:
1. On boot they ran a script from the SD card (`autoexec.sh`), so you got a root shell
   for free.
2. Samsung published GPL source, so you had a map.
3. The camera's own UI and settings were ordinary Linux processes you could poke at.

The NX1000 has none of these. Magic Lantern's way in on Canon was a boot flag that
loads `autoexec.bin` from the card. For the NX1000, someone would first have to find
an equivalent, by:
- dumping and unpacking the ~100 MB firmware file (format undocumented),
- loading it into Ghidra as ARM and mapping a proprietary real-time OS with no symbols,
- finding a code-execution hole (a firmware-update signature check, a card-boot path,
  a Wi-Fi service bug),
- and then writing hooks into a live real-time OS without bricking the only body you own.

That is years of work by a small team that already knows how to do this, which is
roughly what Magic Lantern took. It's on a ~2012 body that maybe a handful of people
would ever install it on. As a weekend project: no.

## What's actually achievable

### 1. Make the one you have less slow (now, free)

Most of the slowness is the camera being 2012-era, not a bug, but some of it is tunable:
- **Firmware v1.15**: update to Samsung's last release
  (`NX1000_NX1100_FW_v1.15.zip`). Skip 1.12, which Samsung withdrew.
- **Shoot JPEG, not RAW+JPEG.** The buffer and card writes are the bottleneck. RAW+JPEG
  is where it chokes.
- **Fast SD card.** The camera can't use UHS-I speeds, but a modern card with fast random
  writes still clears the buffer quicker than an old Class 4/6 card.
- **Turn off image review** after each shot, and turn off any auto Wi-Fi or "i-Function"
  extras you don't use.
- **Format in-camera**, not on a computer, and do it often.
- **Use single AF (S), not continuous**, and prefocus or switch to manual focus for
  anything time-critical. Contrast-detect AF is the slowest part of this camera, and
  no firmware fixes that.

### 2. Add the features from outside: a phone becomes the brain

The NX1000 has Wi-Fi and Samsung's "Remote Viewfinder" mode: a phone gets a live view,
changes some settings and fires the shutter. If that protocol can be captured and
replayed (Wireshark on a laptop playing the phone's role), you get Magic Lantern–style
features **without touching the firmware**:
- intervalometer and timelapse,
- bulb ramping, and long exposures beyond the camera's built-in limit,
- exposure bracketing beyond what the camera allows,
- focus-stack assist (if focus can be driven remotely; to verify),
- zebras or a histogram computed on the phone from the live-view stream.

No risk of bricking the camera, it runs in a browser or small app, and it's a project
that can actually be finished. **First thing to check:** what the Remote Viewfinder
traffic looks like (plain HTTP/UPnP vs. something proprietary), and which settings it
exposes.

### 3. Switch to a hackable NX body and keep the lenses

Every NX body uses the same lens mount. Used prices for these bodies are low.
- **NX300 / NX2000**: rooted Linux, scripts from the SD card, a much faster chip, and a
  touchscreen. The cheapest jump.
- **NX500**: NX-KS mods (ottokiksmaler/nx500_nx1_modding). This is the closest thing
  to Magic Lantern any Samsung has: bitrate hacks, custom key mapping, extra video
  modes, and scripts.
- **NX1**: same mods, flagship body. Expensive by comparison.

If what you want is "this camera, but fast and with extra features", an NX500 with
NX-KS is exactly that, and someone already built it.

## Recommendation

Do (1) today. If the tinkering itch is the point, (2) is a real project that fits how
this repo works: no server, a phone app, done in weeks. If the goal is just a better
camera, (3) with an NX500.

## Open questions to check on the actual camera
- Firmware version currently installed (Menu → Settings → Device information).
- Remote Viewfinder: does it still connect from a current phone without Samsung's
  discontinued app? What's on the wire?
- Is there a wired remote option on the NX1000 at all, or only Wi-Fi?
