# SINGULARITY — Android

The game is one offline HTML file. This wraps it in a WebView, gives it the
three things a browser cannot provide — Play Games leaderboards, a mail
client and the system clipboard — and nothing else.

Open the `android/` folder in Android Studio. It will build once the SDK is
installed; there is nothing to generate and no code to fill in except the
leaderboard ids.

## What is where

    app/src/main/assets/index.html        the game, verbatim
    app/src/main/java/.../MainActivity.kt WebView, insets, back, lifecycle
    app/src/main/java/.../HostBridge.kt   window.AndroidHost — mail, copy, share
    app/src/main/java/.../GpgBridge.kt    window.AndroidGPG — submit, show
    app/src/main/java/.../Boards.kt       logical name -> Play Games id
    app/src/main/res/values/leaderboards.xml   the ids, and nothing else
    tools/gpg-boards.js                   creates all 35 boards over the API

`assets/index.html` is a copy. When the game changes, copy it again — that is
the only thing that ever needs syncing.

## The four decisions worth knowing about

**The game is served over https, not file://.** `WebViewAssetLoader` serves
the asset from `https://appassets.androidplatform.net/`, an origin that never
leaves the device. This matters: a `file://` page is not a *secure context*,
so `navigator.clipboard` does not exist there and `localStorage` is treated
inconsistently across WebView versions. Nothing touches the network.

**`domStorageEnabled` is not optional.** The game keeps every record — levels
cleared, times, streaks, built cubes — in one `localStorage` key. Without
that flag the save silently never survives a restart. No crash, no log.

**Insets are pushed in, not read out.** `env(safe-area-inset-*)` reads zero
inside a WebView however correctly `viewport-fit` is set, because the WebView
is a View inside somebody else's Activity rather than Chromium's own window.
`MainActivity` measures the real cutout and calls `TURNKEY.setInsets()`.

**The manifest `<queries>` block is load bearing.** On Android 11+ without it
the app cannot see any mail client at all: the chooser comes up empty and the
report button appears to do nothing.

## Setting up Play Games

1. **Play Console → your game → Play Games Services → Configuration.**
   Create the game, then put the numeric project id into
   `res/values/strings.xml` as `game_services_project_id`. It has to be a
   *string* resource — inline in the manifest as a bare numeral the merger
   treats it as an integer and the SDK rejects it at runtime.

2. **Add an OAuth2 client** for your signing certificate (both the debug and
   the upload/release SHA-1). Play Games will not authenticate a build whose
   certificate it does not know, and the failure looks like "no leaderboards"
   rather than like an error.

3. **Create the leaderboards.** Thirty-five of them, and the sort order is
   part of what each one means:

       node tools/gpg-boards.js --dry     # see what it will create
       GPG_TOKEN=... GPG_APP=... node tools/gpg-boards.js > /tmp/lb.xml

   Paste the output over `res/values/leaderboards.xml`. Doing it by hand in
   the console works too; the script exists because thirty-five forms is
   thirty-five chances to set one board backwards.

4. **Publish the Play Games configuration.** Leaderboards do not exist for
   anybody but testers until the configuration is published.

An entry left empty in `leaderboards.xml` means "not configured yet" — the
bridge logs it and declines rather than calling the SDK with a blank id. A
half-configured build runs fine and simply has fewer boards.

### Why 35 and not 70

Play Games caps a game at **70 leaderboards for its lifetime**. This uses 35:
thirty ranked vaults plus today, the week, the month, the season and the
streak. The other 35 are deliberately unspent.

The daily boards get their daily/weekly/all-time views free — the SDK creates
those for every board — which is why "one map per day" costs one slot rather
than one per day.

## The day boundary

The daily cube turns over at **midnight UTC-7**, which is the instant Play
Games resets a daily leaderboard, all year. That is not a coincidence and it
is not adjustable: a shared ranking of "today's cube" needs everybody to
agree which day it is, or the board ranks people who solved different
puzzles. Changing it in `index.html` breaks the daily board.

## Signing and release

Release builds are minified. `proguard-rules.pro` keeps both bridges,
because every `@JavascriptInterface` method is called from a string inside
`index.html` that R8 cannot see — without those rules the bridges are renamed
and the game silently loses leaderboards, mail and the clipboard.

Resource shrinking is deliberately **off**: leaderboard ids are string
resources reached only from Kotlin, and a shrunk-away id fails silently at
runtime.

## Reporting

The report button in the game composes a message and hands it to
`HostBridge.mail`, which fires `ACTION_SENDTO` — mail clients specifically,
not the whole share sheet. The address is in `index.html`
(`REPORT_TO`), not here.

Blocking a reported cube is a shipped list: add the identity with
`../turnkey/tools/sgblock.js add <id>`, rebuild, ship. The identity is taken
over all twenty-four orientations, so blocking one also blocks the same cube
reshared rotated.
