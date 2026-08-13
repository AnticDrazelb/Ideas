package com.anticdrazelb.singularity

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Color
import android.view.View
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.webkit.WebSettingsCompat
import androidx.webkit.WebViewAssetLoader
import androidx.webkit.WebViewFeature

/**
 * WHY THE GAME IS SERVED OVER https AND NOT LOADED FROM file://
 *
 * The obvious way to ship a bundled HTML game is `loadUrl("file:///android_asset/…")`.
 * It is also the way that quietly breaks this particular game in three places,
 * because a file:// document has an opaque origin and is not a secure context:
 *
 *   1. THE LEVEL GENERATOR STOPS BEING A WORKER. The page builds cubes on a
 *      Worker created from a blob: URL. Chromium refuses to create a worker from
 *      a blob on an opaque origin, so the page's own fallback kicks in — the
 *      comment for it reads "every failure path falls back to the synchronous
 *      timer this replaced" — and every new cube is generated on the main thread
 *      behind a 900ms setTimeout. The game still works. It just hitches, on the
 *      main thread, at the exact moment the player is waiting to play.
 *
 *   2. THE CLIPBOARD DISAPPEARS. navigator.clipboard is gated on a secure
 *      context, so on file:// it is not merely blocked, it is absent. The page
 *      knows: "on the actual device that API is simply absent — every clipboard
 *      call in here was failing silently."
 *
 *   3. LOCALSTORAGE GETS VAGUE. Storage for opaque origins is at the browser's
 *      discretion, and the save — every vault best, the daily streak, every
 *      cube built in FORGE — is one localStorage key.
 *
 * WebViewAssetLoader fixes all three by serving the same asset from a virtual
 * https origin that resolves inside the APK. `appassets.androidplatform.net` is
 * reserved by Google for exactly this and never resolves in DNS, so nothing
 * leaves the device — this app has no INTERNET permission to leave with.
 */
private const val ASSET_DOMAIN = "appassets.androidplatform.net"
const val GAME_URL = "https://$ASSET_DOMAIN/assets/game/index.html"

/**
 * Builds the one View this app has.
 *
 * @param onPageReady fired when the document has finished parsing and
 *   `window.TURNKEY` can be expected to exist.
 * @param onMailto the page falls back to `window.location.href = 'mailto:…'`
 *   when the host bridge is missing; that navigation lands here.
 * @param onExternalLink anything that is not this game.
 * @param onRendererGone the renderer process died and the WebView is a corpse.
 */
@SuppressLint("SetJavaScriptEnabled")
fun createGameWebView(
    context: Context,
    onPageReady: () -> Unit,
    onMailto: (String) -> Unit,
    onExternalLink: (String) -> Unit,
    onRendererGone: (WebView) -> Unit,
): WebView {
    val assetLoader = WebViewAssetLoader.Builder()
        .setDomain(ASSET_DOMAIN)
        .addPathHandler("/assets/", WebViewAssetLoader.AssetsPathHandler(context))
        .build()

    return WebView(context).apply {
        // BLACK BEFORE ANYTHING ELSE. A WebView is created white and stays white
        // until the page's first paint. On a black game that is a full-screen
        // white flash on every cold start and every renderer rebuild, and it is
        // the single most recognisable tell that an app is a web page.
        setBackgroundColor(Color.BLACK)

        // Chrome's rubber-band and the blue edge glow are browser furniture. The
        // page also sets overscroll-behavior:none, but that governs scroll
        // chaining inside the document; this governs the View itself, and only
        // one of the two can stop the whole page bouncing under a drag.
        overScrollMode = View.OVER_SCROLL_NEVER
        isVerticalScrollBarEnabled = false
        isHorizontalScrollBarEnabled = false
        isScrollbarFadingEnabled = false

        // The page owns haptics through navigator.vibrate and tunes patterns per
        // event. Android's own click feedback on top of that is a second,
        // uninvited buzz.
        isHapticFeedbackEnabled = false

        // No autofill on the FORGE share-code field. Android will otherwise
        // offer to fill a username into it.
        importantForAutofill = View.IMPORTANT_FOR_AUTOFILL_NO

        // NOTE ON LONG-PRESS, AND ON A HARDENING STEP DELIBERATELY NOT TAKEN.
        // The reflex here is setOnLongClickListener { true } to kill text
        // selection. That would also kill PASTE, and FORGE exists to have share
        // codes pasted into it. The page already declares user-select:none on
        // everything and re-enables it on .edIn alone, which is the correct,
        // surgical version of this — so the native side keeps its hands off.

        settings.apply {
            javaScriptEnabled = true

            // The save lives here.
            domStorageEnabled = true

            // The page's viewport meta is
            //   width=device-width, initial-scale=1, user-scalable=no
            // and useWideViewPort is what makes a WebView honour it at all.
            // Without it the page is laid out at 980px and scaled down, which is
            // "mobile website in a frame" rendered exactly.
            useWideViewPort = true
            loadWithOverviewMode = false

            // Pinch-zoom is a document gesture. This is a board you drag.
            setSupportZoom(false)
            builtInZoomControls = false
            displayZoomControls = false

            // SYSTEM FONT SCALE DOES NOT APPLY TO A GAME BOARD.
            // A WebView multiplies text by the user's font-size setting, so a
            // phone set to "Largest" renders this HUD at 130% and every chip
            // overflows its border. The page has its own fitHud() that shrinks
            // the bar to fit, and the two would fight each other every frame.
            // A native game renders its HUD at the size it designed; this is the
            // setting that makes a WebView do the same.
            textZoom = 100

            // WebAudio needs to be allowed to start. The page still builds its
            // AudioContext on a real interaction — this only removes Chromium's
            // extra gate on top.
            mediaPlaybackRequiresUserGesture = false

            // Nothing here needs the filesystem or content providers: the game
            // is served from the asset loader. Turning these off removes the
            // classic WebView file-exfiltration surface outright.
            allowFileAccess = false
            allowContentAccess = false
            javaScriptCanOpenWindowsAutomatically = false
            setGeolocationEnabled(false)

            // Do not steal focus and pop the keyboard on load.
            setNeedInitialFocus(false)

            cacheMode = WebSettings.LOAD_DEFAULT
        }

        // FORCE-DARK WOULD REPAINT A GAME THAT IS ALREADY DARK.
        // Android's algorithmic darkening inverts and re-tints a page it thinks
        // is light. This page's whole design is a colour code — "COLOUR IS DATA,
        // NOT MATERIAL … a colour on screen is a claim about what a thing IS" —
        // and darkening would rewrite that claim. Apps targeting API 33+ are
        // already opted out by default; this says so explicitly so the answer
        // does not depend on a targetSdk bump years from now.
        if (WebViewFeature.isFeatureSupported(WebViewFeature.ALGORITHMIC_DARKENING)) {
            WebSettingsCompat.setAlgorithmicDarkeningAllowed(settings, false)
        }

        webViewClient = object : WebViewClient() {

            override fun shouldInterceptRequest(
                view: WebView,
                request: WebResourceRequest,
            ): WebResourceResponse? = assetLoader.shouldInterceptRequest(request.url)

            override fun shouldOverrideUrlLoading(
                view: WebView,
                request: WebResourceRequest,
            ): Boolean {
                val url = request.url
                return when {
                    // The game navigating within itself.
                    url.host == ASSET_DOMAIN -> false

                    // The page's own fallback when the host bridge is missing.
                    url.scheme == "mailto" -> { onMailto(url.toString()); true }

                    url.scheme == "http" || url.scheme == "https" -> {
                        onExternalLink(url.toString()); true
                    }

                    // A game with no network has no business following intent:,
                    // file: or anything else. Refusing to navigate is the whole
                    // behaviour.
                    else -> true
                }
            }

            override fun onPageFinished(view: WebView, url: String) {
                onPageReady()
            }

            /**
             * The asset is inside the APK, so this should never fire. If it
             * does, it is a build problem — a renamed asset, a path handler that
             * did not match — and the symptom without this is a black screen
             * with nothing in Logcat to say why. Naming the error is the whole
             * value here.
             */
            override fun onReceivedError(
                view: WebView,
                request: WebResourceRequest,
                error: android.webkit.WebResourceError,
            ) {
                if (!request.isForMainFrame) return
                android.util.Log.e(
                    CONSOLE_TAG,
                    "failed to load ${request.url}: ${error.errorCode} ${error.description}",
                )
            }

            /**
             * THE RENDERER CAN DIE, AND THE DEFAULT IS THAT THE APP DIES WITH IT.
             *
             * Chromium runs the page in a separate process. If it is killed —
             * out of memory behind a camera app, or a genuine crash in a canvas
             * this size — then returning false here kills the whole app: no
             * dialog, no report, the app is simply gone from the recents list.
             * Returning true keeps the process alive so the Activity can build a
             * new WebView and reload. The save is in localStorage, so the player
             * loses the current cube and nothing else.
             */
            override fun onRenderProcessGone(
                view: WebView,
                detail: android.webkit.RenderProcessGoneDetail,
            ): Boolean {
                onRendererGone(view)
                return true
            }
        }

        // WITHOUT A WebChromeClient, THE PAGE'S CONSOLE GOES NOWHERE.
        //
        // A WebView with no chrome client silently discards every console
        // message, including uncaught exceptions. The game is 9,000 lines of
        // JavaScript with no other way to say anything went wrong, so the first
        // real bug found on a device would present as "the board stopped
        // responding" with an empty Logcat. This forwards the console at
        // matching severity and does nothing else.
        webChromeClient = object : android.webkit.WebChromeClient() {
            override fun onConsoleMessage(message: android.webkit.ConsoleMessage): Boolean {
                val where = "${message.sourceId()}:${message.lineNumber()}"
                val text = "${message.message()} ($where)"
                when (message.messageLevel()) {
                    android.webkit.ConsoleMessage.MessageLevel.ERROR ->
                        android.util.Log.e(CONSOLE_TAG, text)
                    android.webkit.ConsoleMessage.MessageLevel.WARNING ->
                        android.util.Log.w(CONSOLE_TAG, text)
                    else -> android.util.Log.d(CONSOLE_TAG, text)
                }
                return true
            }
        }
    }
}

private const val CONSOLE_TAG = "SingularityJS"

/**
 * Load the game and report the frame it becomes visible on.
 *
 * ORDER MATTERS AND IS EASY TO GET WRONG. postVisualStateCallback reports on the
 * state of the page *as of the call*, so posting it before loadUrl asks "is the
 * blank page ready to draw" — which it is, immediately. The splash would then
 * lift on the first frame of nothing at all, which is the white flash this whole
 * mechanism exists to prevent, arrived at through the API meant to fix it.
 */
fun WebView.loadGame(onFirstPaint: () -> Unit) {
    loadUrl(GAME_URL)
    postVisualStateCallback(FIRST_PAINT_REQUEST, object : WebView.VisualStateCallback() {
        override fun onComplete(requestId: Long) = onFirstPaint()
    })
}

private const val FIRST_PAINT_REQUEST = 1L
