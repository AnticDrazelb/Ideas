# Getting it onto Play

The build side is done — `./build-android.sh -aab` produces an upload. Everything
below is the part Google needs from you, in the order it bites.

---

## 1. The upload key, and why this is the scary one

```sh
keytool -genkey -v -keystore upload.keystore -alias upload \
        -keyalg RSA -keysize 2048 -validity 10000
```

**Back this file up somewhere you will still have in five years.** It is the one
thing in this whole process that cannot be recovered or reissued by you: lose it
and you cannot ship an update to the listing again, ever. Google can re-key you
if you enrol in Play App Signing, and that is worth doing, but do not rely on it
as your only copy.

It is read from the environment, never from this repository — a keystore
committed next to its password is not a keystore:

```sh
export SE_KEYSTORE=~/keys/upload.keystore
export SE_KEYSTORE_PASS='…'
export SE_KEY_ALIAS=upload
export SE_KEY_PASS='…'
export SE_VERSION_NAME=0.1.0
export SE_VERSION_CODE=1
./build-android.sh -aab
```

`SE_VERSION_CODE` must go **up** on every upload. Play rejects a code it has seen
before, and it tells you so *after* the upload rather than before it.

---

## 2. Decide the package name before you upload anything

Currently `com.singularity.engine`, set in `Editor/ProjectSetup.cs`.

**This is permanent.** The application id can never change for the life of a
listing — a different id is a different app, with a different URL and no reviews.
If `singularity.engine` is not a domain you control, pick something that is; the
convention is a reverse domain you own.

---

## 3. Data safety: the good news

This game **collects nothing and sends nothing**. No analytics, no ads, no
crash reporting, no network permission at all — `forceInternetPermission` is off
and the manifest has no `INTERNET`. The daily is computed from the date, the
cubes are computed from their numbers.

So the Data Safety form is all "no", and you do not strictly need a privacy
policy URL for a no-collection app — though Play increasingly asks for one
regardless, and a single page saying "this app collects no data" satisfies it.

If you later wire `DailyBoards.Submit` to Play Games, that changes: sign-in
collects an identifier and the form has to say so.

---

## 4. What Play will check that you cannot see locally

- **Target API level.** Play enforces a minimum target that rises every August.
  `ProjectSetup` uses `AndroidApiLevelAuto`, which takes the highest SDK you have
  installed — so keep your Android SDK current or set it explicitly.
- **64-bit.** Already handled: ARM64 only, via IL2CPP.
- **App bundle, not APK.** New apps must be `.aab`. `-aab` does this.
- **Content rating questionnaire.** An abstract puzzle with no text entry, no
  chat and no purchases rates everywhere.

---

## 5. Store listing assets you still have to make

None of these are code and none of them exist yet:

| | |
|---|---|
| App icon | **done** — generated from the same palette, `Singularity → Regenerate App Icon` |
| Feature graphic | 1024×500, required |
| Phone screenshots | at least 2, and the game is worth screenshotting |
| Short description | 80 characters |
| Full description | 4000 characters |

The pitch is already written and it is the first line of the title screen:

> You are a black hole inside a broken machine. Fold the engine until its
> circuits align, then collapse into the core.

---

## 6. Before you press publish

Ship to **internal testing** first. It is instant, it takes the same bundle, and
it is the only way to find out whether the thing installs and runs from a store
download rather than from `adb install` — which is a genuinely different code
path, because a store build is signed differently and may be optimised
differently.

Then closed testing, then production. Play now requires a period of closed
testing with real testers before a new personal developer account can go to
production; check the current rule, it has changed twice.

---

## The honest status

The Unity build has still never been run by anybody. Everything above is ready
and none of it is verified. The order that finds problems fastest is:

1. `./build-android.sh` → `adb install` → does it open
2. play a vault, use the Forge, check the daily
3. `-aab` → internal testing → does it survive a store round trip
4. everything in this document
