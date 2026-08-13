# SINGULARITY — Android shell

A native Android wrapper for `SINGULARITY ENGINE`, the single-file HTML game in
`app/src/main/assets/game/index.html`.

The goal is not "a WebView that displays the game". It is that a player who did
not read this file cannot tell the difference between this and a native game.
Everything below is in service of that, and most of it is about removing tells
rather than adding features.

The game was written for this. Its stylesheet already documents the seam:

> **AN ANDROID WEBVIEW DOES NOT KNOW WHERE THE NOTCH IS.** […] in a packaged
> Android app every one of these reads ZERO however correctly the host sets
> `viewport-fit`. […] the host measures the real insets and writes `--sa-top`
> and friends onto `:root`, and this is the seam that lets it.

So this shell implements a contract the page already publishes, rather than
inventing one.

---

## Open it

```
Android Studio → Open → the `android/` directory
```

Gradle 8.14.3 (wrapper included), AGP 8.9.2, Kotlin 2.1.20, JDK 17.
`minSdk 26`, `compile/targetSdk 36`. No `local.properties` is committed —
Android Studio writes it on first open.

From the command line:

```bash
cd android
./gradlew assembleDebug
```

### ⚠ The one thing to check on first run

**This app ships without the `INTERNET` permission.** It does not need it: the
game is served out of the APK by `WebViewAssetLoader`, which answers the request
before any socket is opened, and an app that cannot reach the network cannot
leak anything.

That reasoning is sound but **it was not verified on a device** — see
*What was and wasn't verified* at the bottom. If the very first launch shows a
black screen, check Logcat for a `SingularityJS` line naming a load error. If it
names a network error, add this one line to `AndroidManifest.xml` and rebuild:

```xml
<uses-permission android:name="android.permission.INTERNET" />
```

That is the only foreseeable first-run failure, and that is the whole fix.

---

## The contract with the page

The page publishes `window.TURNKEY` and consumes two host objects. Nothing else
crosses the boundary.

| Direction | Name | Meaning |
|---|---|---|
| host → page | `TURNKEY.onBack()` | returns `true` = handled, keep the Activity alive |
| host → page | `TURNKEY.pause()` / `.resume()` | stops/starts the frame loop **and the solve clock** |
| host → page | `TURNKEY.setInsets(t,r,b,l)` | real display cutout, in **CSS pixels** |
| page → host | `AndroidHost.copy(text)` | clipboard |
| page → host | `AndroidHost.mail(to,subject,body)` | hand a cube report to a mail app |
| page → host | `AndroidGPG.submit(board,score)` / `.show(board)` | leaderboards |

Two details in that table are load-bearing and easy to get wrong:

**Insets are CSS pixels, not device pixels.** Android hands you physical pixels;
the page appends `px` and hands them to CSS. Everything is divided by
`displayMetrics.density` on the way out (`GameActivity.listenForInsets`). Skip
that and a notch inset is three times too big on a modern phone.

**Scores cross as strings.** The page's own note: *"A vault total in milliseconds
is small enough for a double today, but it lands in a Java long, and a string is
the one representation that cannot lose a digit on the way."* They are parsed
once, in `PlayGamesBridge.submit`, and a non-integer is dropped rather than
rounded into a fake record.

---

## The decisions that matter

### The game is served over `https://`, not `file://`

`loadUrl("file:///android_asset/…")` is the obvious approach and it quietly
breaks this game in three places, because a `file://` document has an opaque
origin and is not a secure context:

1. **The level generator stops being a Worker.** The page builds cubes on a
   Worker created from a `blob:` URL. Chromium will not create a worker from a
   blob on an opaque origin, so the page falls back — by its own design — to
   generating levels on the main thread behind a 900 ms `setTimeout`. The game
   still works. It just hitches, on the main thread, exactly when the player is
   waiting to play.
2. **The clipboard disappears.** `navigator.clipboard` is gated on a secure
   context. Share codes and cube reports are most of what this game does with
   text.
3. **`localStorage` gets vague.** Storage for opaque origins is at the browser's
   discretion, and the save — every vault best, the streak, every built cube — is
   one `localStorage` key.

`WebViewAssetLoader` serves the same asset from `https://appassets.androidplatform.net`,
a domain Google reserves for this and which never resolves in DNS. All three
problems go away and nothing leaves the device.

### The splash is held until the page has actually painted

The signature tell of a WebView app is the launch: black window, then a **white**
WebView, then the game. Three surfaces are painted the page's own black up front
(window, container, WebView), and the splash is held by
`setKeepOnScreenCondition` until `WebView.postVisualStateCallback` reports the
first real frame — not a guessed delay. A 6-second ceiling covers a page that
never paints at all.

`postVisualStateCallback` must be posted **after** `loadUrl`; posted before, it
reports on the blank page and fires immediately, which reintroduces the exact
flash it exists to prevent.

### The Activity is never recreated

The `configChanges` list on the Activity is long on purpose. Rotation, unfold,
theme change, display-size change and font-scale change all default to destroying
and rebuilding the Activity — which reloads the page and restarts the cube. Every
one of them is handled in `onConfigurationChanged` instead, and the run survives.

### Algorithmic darkening is explicitly off

Android's force-dark inverts and re-tints pages it thinks are light. This page's
entire design is a colour code — *"a colour on screen is a claim about what a
thing IS"* — and darkening rewrites that claim. Apps targeting API 33+ are opted
out by default; it is set explicitly so the answer does not silently change on a
future `targetSdk` bump.

### System font scale does not apply

`textZoom = 100`. A WebView multiplies text by the user's system font size, so a
phone set to "Largest" renders the HUD at 130% and every chip overflows its
border — and the page's own `fitHud()` would then fight the system every frame.
Native games render their HUD at the size they designed. This is a deliberate
accessibility trade-off, and it is the same one every native game makes.

### Long-press is *not* disabled

The reflex hardening step for a WebView game is
`setOnLongClickListener { true }` to kill text selection. It would also kill
**paste**, and FORGE exists to have share codes pasted into it. The page already
does this correctly and surgically — `user-select:none` on everything,
re-enabled on `.edIn` alone — so the native side keeps its hands off.

### The side edges are taken back from the back gesture

This game is played by dragging across a board. On gesture navigation the left
and right edges belong to the system, so the most natural input — a big
horizontal sweep — is also the most likely to throw the player out to the title
screen. `systemGestureExclusionRects` claims a 200 dp band (Android's per-edge
cap) centred vertically on each edge. The top and bottom of each edge stay system
gesture area, so back-by-swipe is still available.

### The renderer is allowed to die without taking the app with it

Chromium runs the page in a separate process. If it is killed, the default is
that the **whole app disappears** — no dialog, no crash report. `onRenderProcessGone`
returns `true`, detaches the corpse and builds a new WebView. `localStorage` lives
in the browser process, so the player loses the cube in front of them and nothing
else.

### The save is backed up

Everything the player owns is one `localStorage` key, which lives in
`app_webview/` off the data-dir root — a directory Auto Backup does **not** cover
by default. `backup_rules.xml` and `data_extraction_rules.xml` name it explicitly,
for both cloud restore and direct device transfer. Without them, a new phone is a
wiped save.

### Every `@JavascriptInterface` method runs on a binder thread

This is the defect WebView bridges ship with most often, because it does not look
like one. Chromium calls these methods on a private *JavaBridge* thread, so a
`startActivity` made directly inside one is a cross-thread call into
single-threaded UI code — it usually appears to work, then throws on some other
phone. Everything touching the UI toolkit is posted to the main looper. Only the
`PackageManager` query in `mail()` stays put, because it is thread-safe and its
answer is the return value.

---

## Play Games (optional, off by default)

The app is complete and shippable with this left alone. The page's BOARDS screen
reads **"BOARDS — LOCAL ONLY"** and shows personal bests, which is a designed
state in the page, not a degraded one.

To turn it on:

1. Put the numeric project id in `res/values/strings.xml` → `play_games_app_id`.
2. Fill in `res/values/play_games_boards.xml`. The page never learns a Google
   board id — it submits to logical names (`vault_07`, `daily_streak`) and this
   file owns the mapping, so boards can be re-pointed without touching the game.
   Entries left as `CHANGE_ME` are skipped at runtime.

While the id is the placeholder, `PlayGamesSdk.initialize` is never called, which
is what keeps the manifest's placeholder `APP_ID` inert.

**Availability is honest.** The page decides whether leaderboards exist with
`!!window.AndroidGPG`, so the object must not exist until they really do. But
`addJavascriptInterface` only takes effect on the next page load, and sign-in
resolves long after the page is up. The way through is something the page
volunteers about itself:

> THE HOST IS RESOLVED PER CALL, NOT CAPTURED ONCE. A WebView can install its
> bridge object after the document has run […]

So the bridge is registered before load under a private name and promoted to
`window.AndroidGPG` by one line of JavaScript, only once a player has actually
signed in.

---

## Updating the game

Replace `app/src/main/assets/game/index.html`. Two rules:

1. **Ship it verbatim.** The page reads its own source at runtime — it pulls the
   level generator out of `document.querySelector('script').textContent` and
   hands it to a Worker. Any step that minifies, rewrites or splits the HTML
   breaks level generation on the second cube and nowhere earlier.
2. **Never inject a `<script>` tag.** For the same reason: the page takes the
   *first* script element in the document. All host-to-page calls go through
   `evaluateJavascript`, which creates no element.

---

## What was and wasn't verified

Built in an environment where `dl.google.com` and Google's Maven are blocked by
egress policy, so the Android SDK and AGP could not be downloaded.

**Verified here:**

- Every XML file parses (`AndroidManifest.xml`, all resources).
- Every Kotlin source parses with no syntax or structural errors, checked with
  the Kotlin 2.0.21 compiler. Every unresolved symbol is an Android/AndroidX/
  Play-Services API or the generated `R` — no cross-file symbol in this project
  is unresolved.
- The game asset is byte-identical to the source file (`md5` match).
- The Gradle wrapper is real and self-consistent.

**Not verified:**

- **No compile, no APK, no device run.** The project has never been through AGP.
- Dependency versions (AGP 8.9.2, Kotlin 2.1.20, androidx.webkit 1.12.1,
  play-services-games-v2 20.1.2) were chosen for known-good compatibility with
  Gradle 8.14.3 but could not be resolved against a repository. Android Studio
  will offer upgrades; they are safe to take.
- The `INTERNET`-permission reasoning at the top of this file.
