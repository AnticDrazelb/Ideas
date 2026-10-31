# TrimPlay

TrimPlay is PS4 Remote Play for the **TrimUI Smart Pro** running **CrossMix OS**.
Your console streams its picture and sound to the handheld over Wi-Fi, and the
Smart Pro's buttons and sticks act as a DualShock 4.

TrimPlay is a small SDL2 app built on **libchiaki** from
[chiaki-ng](https://github.com/streetpea/chiaki-ng), the open-source Remote Play
client. libchiaki handles console discovery, pairing, the encrypted stream and
audio. TrimPlay adds:

- a menu for handheld controls (no keyboard or touchscreen needed),
- H.264 decoding tuned for the Smart Pro's Allwinner A133P (four Cortex-A53 cores),
- controller mapping onto the Smart Pro's buttons.

> **Status:** I haven't tested it on real hardware. The menus, pairing flow, controls
> and the video decode-and-display path all run on a desktop, using a stand-in for
> the console. The parts to check on the device are
> listed under [Device checklist](#device-checklist).

---

## Install

1. **Build** the app (see [Building](#building)). The quickest route is the
   GitHub Action: open the **Actions** tab, then **TrimPlay**, then download the `TrimPlay` artifact.
2. Unzip it so you get `SD:/Apps/TrimPlay/` containing `launch.sh`,
   `config.json`, `trimplay` and the `lib/` and `res/` folders.
3. On a PC, edit `SD:/Apps/TrimPlay/trimplay.ini` and paste in your
   `psn_account_id` (see below).
4. On the Smart Pro, open **Apps**, then **TrimPlay**.

### Getting your account ID

To pair, the PS4 needs the **PSN account ID** of the user that will play. This is
an internal number, not your sign-in name, and you only need it once. In
`trimplay.ini` it's written as base64: 12 characters ending in `=`, for example
`AbCdEfGhIjk=`.

Ways to get it:

- Install **chiaki-ng** on a PC, Steam Deck or phone. During setup it can sign
  in to PSN and fill in the account ID, which you can copy from its settings.
- Or use one of the small "PSN account ID" helper scripts or pages the
  chiaki and Remote Play community link to. These ask you to sign in to PSN
  and print the base64 ID.

## Pairing (once per console)

1. Your PS4 must be fully on, not in rest mode, and on the same network as the Smart Pro.
2. On the PS4, go to **Settings**, then **Remote Play Connection Settings**, then **Add Device**.
   An 8-digit number appears.
3. In TrimPlay, select the console and press **X** (pair). Enter the number with
   the D-pad: up/down changes a digit, left/right moves between digits. Press **A**.
4. The pairing keys are saved to `hosts.ini`. Treat that file like a password.

Optional, so the Smart Pro can wake the PS4 from rest mode: on the PS4, go to
**Settings**, then **Power Saving Settings**, then **Set Features Available in Rest Mode**, and turn on both
**Stay Connected to the Internet** and **Enable Turning On PS4 from Network**.

## Controls

### Console list

| Button | Action |
|---|---|
| A | Connect. Wakes the console first if it's in rest mode. |
| X | Pair |
| Y | Send wake-up only |
| SELECT | Forget this pairing |
| START | Button test |
| MENU | Quit TrimPlay |

### While streaming

Face buttons map **by position**, the same layout as a DualShock:

| Smart Pro | PS4 |
|---|---|
| B (bottom) | Cross |
| A (right) | Circle |
| Y (left) | Square |
| X (top) | Triangle |
| D-pad, sticks, L1/R1/L2/R2, L3/R3 | Same |
| SELECT | Share |
| START | Options |
| MENU (tap) | PS button |
| MENU (hold) | PS held (quick menu) |
| MENU + SELECT | Touchpad click |
| MENU + L1 | Show or hide stats (fps, decode time, lost frames) |
| **Hold MENU + START (1 s)** | **Disconnect** |

The Smart Pro's L2/R2 are digital, so they send fully pressed or fully released.

If your PS4 user has a login passcode, TrimPlay asks for it after connecting.

## Settings (`trimplay.ini`)

| Key | Default | Notes |
|---|---|---|
| `psn_account_id` | – | Needed for pairing only |
| `host` | – | Console IP. Set this if the console never appears in the list. |
| `resolution` | 720 | 360 / 540 / 720 (1080 needs a PS4 Pro and is wasted on a 720p screen) |
| `fps` | 30 | 30 or 60 |
| `bitrate` | 0 | kbps; 0 = preset |
| `decode_threads` | 4 | |
| `decode_frame_threads` | 0 | 1 = faster decode, about 2 frames more lag |
| `fast_decode` | 0 | 1 = skip deblocking: much faster, slightly blockier |
| `audio_latency_ms` | 120 | Queued audio beyond this is dropped to stay in sync |
| `swap_ab` | 0 | 1 = Smart Pro A acts as Cross |
| `show_stats` | 0 | Start with the stats overlay on |

**Performance tuning:** start at 720p/30. Turn on stats (MENU+L1) and watch
`decode … ms`. Each frame has a time budget of 33 ms at 30 fps or 16 ms at 60 fps. If decode
time is over budget, try these in order:

1. `fast_decode=1`
2. `decode_frame_threads=1`
3. `resolution=540`

Use 5 GHz Wi-Fi if you can, and keep the PS4 on Ethernet.

## Device checklist

These depend on the CrossMix firmware and couldn't be checked without the device:

- **Display and SDL2:** TrimPlay uses the firmware's own `libSDL2` (not bundled),
  just as other CrossMix apps do. `log.txt` shows which renderer it got (look for `Renderer:`).
- **Buttons:** open the console list and press **START** to get the button test. Every
  button you press should light up. If one doesn't, the raw number it shows
  goes into the `[joystick]` or `[keys]` section of `trimplay.ini`.
- **MENU button:** if CrossMix takes over MENU for its own overlay, map another
  button to `menu` in `[joystick]`.
- **Decode speed** at 720p/30 and 720p/60 (see Performance tuning).
- **Rumble:** this needs the firmware's SDL to expose the motor.

Everything is logged to `SD:/Apps/TrimPlay/log.txt`.

## Building

You need Docker. The build runs in a Debian bullseye arm64 container. bullseye's
old glibc runs on the Smart Pro's root filesystem. The container builds:

- a minimal FFmpeg (H.264 decoder only),
- libchiaki from a pinned chiaki-ng commit,
- TrimPlay itself.

It then bundles the libraries and zips the app folder.

On an x86 PC, enable ARM emulation once:

```bash
docker run --privileged --rm tonistiigi/binfmt --install arm64
```

Then build:

```bash
sh trimplay/build/build.sh
```

The output is `trimplay/dist/TrimPlay.zip`. Under emulation the first build takes
a while, and later builds reuse the cached image. The GitHub workflow in
`.github/workflows/trimplay.yml` does the same thing on every push that touches `trimplay/`.

For a desktop test build (Linux), point CMake at a chiaki-ng checkout and run with a window:

```bash
cmake -S trimplay -B build-desktop -DCHIAKI_DIR=/path/to/chiaki-ng && cmake --build build-desktop --target trimplay
```

```bash
TRIMPLAY_WINDOWED=1 TRIMPLAY_HOME=$PWD/trimplay/package ./build-desktop/trimplay
```

On a desktop the keyboard works as the controller: arrows, X/C/Z/S for the face buttons,
Return for START, Backspace for SELECT and Esc for MENU.

## Code layout

| File | What it does |
|---|---|
| `src/main.c` | Screens: console list, PIN entry, pairing, waking, streaming controls, button test |
| `src/stream.c` | Session setup, H.264 decode → SDL YUV texture, Opus → SDL audio queue, rumble |
| `src/input.c` | Game controller, raw joystick or keyboard → logical buttons |
| `src/store.c` | Reads and writes `trimplay.ini` and `hosts.ini` |
| `src/ui.c` | Text drawing with a small texture cache |

## Licence

libchiaki and chiaki-ng are AGPL-3.0 (see `COPYING-chiaki-ng` in the build).
Because TrimPlay links libchiaki, TrimPlay is AGPL-3.0 too. Not affiliated with
Sony. PlayStation, PS4 and Remote Play are Sony Interactive Entertainment trademarks.
