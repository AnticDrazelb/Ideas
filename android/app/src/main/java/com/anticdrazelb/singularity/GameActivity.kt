package com.anticdrazelb.singularity

import android.content.Intent
import android.content.res.Configuration
import android.graphics.Color
import android.graphics.Rect
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.View
import android.view.ViewGroup.LayoutParams.MATCH_PARENT
import android.view.WindowManager
import android.webkit.WebView
import android.widget.FrameLayout
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.OnBackPressedCallback
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import com.anticdrazelb.singularity.bridge.HostBridge
import com.anticdrazelb.singularity.bridge.PlayGamesBridge

/**
 * The whole app.
 *
 * There is one Activity, one View and no navigation, because the page already
 * has all three and a second copy of them in Kotlin would be a second thing to
 * keep in sync. What this class does is the four jobs the page cannot do for
 * itself, named in the page's own words: it "owns the back gesture, the
 * lifecycle, and the insets", and it puts the game on screen without ever
 * showing the player a white rectangle.
 */
class GameActivity : ComponentActivity() {

    private lateinit var container: FrameLayout
    private val main = Handler(Looper.getMainLooper())

    private var web: WebView? = null
    private var turnkey: TurnkeyBridge? = null
    private var host: HostBridge? = null
    private var playGames: PlayGamesBridge? = null

    private var firstPaint = false
    private var pageReady = false
    private var playGamesSignedIn = false

    // ─────────────────────────────────────────────────────────────────────
    // BACK
    //
    // The page keeps a stack of screens — win, manual, pause, settings, boards,
    // the editor, the cube grid — and its onBack walks exactly one level of it
    // per press, returning true for "handled". Android's job is to ask, and to
    // finish the Activity only on the false that comes back from the title
    // screen, where there is nothing left to close.
    //
    // The callback stays enabled for the life of the Activity, which is what
    // tells the platform this app handles back itself. That matters for
    // predictive back: a disabled callback makes the system preview the home
    // screen behind a gesture that is actually going to close a menu.
    // ─────────────────────────────────────────────────────────────────────
    private val backCallback = object : OnBackPressedCallback(true) {
        private var awaitingAnswer = false

        override fun handleOnBackPressed() {
            val bridge = turnkey
            if (bridge == null || !pageReady) { leaveApp(); return }

            // The answer is asynchronous, so a fast double-press can arrive
            // before the first one has been answered. Without this the page
            // would be asked twice and would close two screens for one press.
            if (awaitingAnswer) return
            awaitingAnswer = true

            bridge.onBack { handled ->
                awaitingAnswer = false
                if (!handled) leaveApp()
            }
        }
    }

    private fun leaveApp() {
        backCallback.isEnabled = false
        onBackPressedDispatcher.onBackPressed()
    }

    // ─────────────────────────────────────────────────────────────────────

    override fun onCreate(savedInstanceState: Bundle?) {
        // BEFORE super.onCreate, AND BEFORE setContentView. The splash screen
        // installs a pre-draw listener on the content view; installing it late
        // means the first frames are already gone.
        val splash = installSplashScreen()

        // Hold the splash until the game is genuinely on screen. This is the
        // whole anti-flash mechanism: without it the splash lifts the moment the
        // Activity is drawable, which is a black window, then a white WebView,
        // then the game — and the player sees all three.
        splash.setKeepOnScreenCondition { !firstPaint }
        splash.setOnExitAnimationListener { provider ->
            provider.view.animate()
                .alpha(0f)
                .setDuration(SPLASH_FADE_MS)
                .withEndAction { provider.remove() }
                .start()
        }

        super.onCreate(savedInstanceState)

        // A page that never paints — a corrupt asset, a WebView that failed to
        // start — must not leave the player looking at a splash for ever.
        main.postDelayed({ firstPaint = true }, SPLASH_LIMIT_MS)

        onBackPressedDispatcher.addCallback(this, backCallback)

        container = FrameLayout(this).apply {
            setBackgroundColor(Color.BLACK)
            // Every surface in the stack is the same black: window, container,
            // WebView, page. Nothing in the launch sequence can flash.
            fitsSystemWindows = false
        }
        setContentView(container)

        // AFTER setContentView. An insets controller obtained before the window
        // has content can have its hide() dropped on some versions, and the
        // symptom is a status bar that is present on first launch and gone on
        // every launch after — the kind of bug that gets called "flaky".
        goEdgeToEdge()

        // KEEP THE SCREEN ON. This is a puzzle game whose whole loop is looking
        // at a board and thinking. The default screen timeout treats thirty
        // seconds without a touch as an idle phone; here it is a player halfway
        // through a fold.
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        listenForInsets()

        playGames = PlayGamesBridge.createIfConfigured(this)
        buildWebView()

        // Sign-in is slow, optional and allowed to fail. Nothing waits on it;
        // when it succeeds it promotes the bridge and the page's BOARDS screen
        // finds leaderboards the next time it is opened.
        playGames?.let {
            PlayGamesBridge.signIn(this) {
                playGamesSignedIn = true
                promotePlayGamesIfReady()
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // THE VIEW
    // ─────────────────────────────────────────────────────────────────────

    private fun buildWebView() {
        pageReady = false

        val view = createGameWebView(
            context = this,
            onPageReady = {
                pageReady = true
                // A fresh document has never heard the insets, and the bridge
                // dedupes repeats — so it has to be told to forget first.
                turnkey?.forgetInsets()
                ViewCompat.requestApplyInsets(container)
                promotePlayGamesIfReady()
            },
            onMailto = { url -> host?.openMailto(url) },
            onExternalLink = ::openExternally,
            onRendererGone = ::rebuildAfterRendererDeath,
        )

        host = HostBridge(this) {
            Toast.makeText(this, R.string.no_mail_app, Toast.LENGTH_LONG).show()
        }
        view.addJavascriptInterface(host!!, HostBridge.NAME)

        // Registered under the private name. It becomes window.AndroidGPG only
        // if a player actually signs in — see PlayGamesBridge.
        playGames?.let { view.addJavascriptInterface(it, PlayGamesBridge.PRIVATE_NAME) }

        container.addView(view, MATCH_PARENT, MATCH_PARENT)
        web = view
        turnkey = TurnkeyBridge(view)

        view.addOnLayoutChangeListener { v, _, _, _, _, _, _, _, _ ->
            claimEdgesFromTheBackGesture(v)
        }

        view.loadGame { firstPaint = true }
    }

    /**
     * The renderer process died and took the page with it.
     *
     * See the note in GameWebView: the alternative to handling this is the app
     * disappearing off the screen with no message. The old WebView is a corpse —
     * it must be detached and destroyed without being touched for anything else
     * — and a new one takes its place. localStorage survived in the browser
     * process, so the player comes back to their vaults, their streak and their
     * built cubes, having lost only the cube in front of them.
     */
    private fun rebuildAfterRendererDeath(dead: WebView) {
        container.removeView(dead)
        dead.destroy()
        web = null
        turnkey = null
        host = null
        buildWebView()
    }

    private fun promotePlayGamesIfReady() {
        if (!pageReady || !playGamesSignedIn) return
        web?.evaluateJavascript(PlayGamesBridge.PROMOTE_JS, null)
    }

    private fun openExternally(url: String) {
        runCatching { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) }
    }

    // ─────────────────────────────────────────────────────────────────────
    // EDGE TO EDGE, AND THE INSETS THE PAGE ASKED FOR
    // ─────────────────────────────────────────────────────────────────────

    private fun goEdgeToEdge() {
        WindowCompat.setDecorFitsSystemWindows(window, false)
        val controller = WindowCompat.getInsetsController(window, window.decorView)
        controller.hide(WindowInsetsCompat.Type.systemBars())

        // Sticky immersive. A swipe brings the bars back as a transient overlay
        // that fades on its own, rather than resizing the game and pushing the
        // board around mid-fold.
        controller.systemBarsBehavior =
            WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
    }

    private fun listenForInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(container) { _, insets ->
            val density = resources.displayMetrics.density

            val cutout = insets.getInsets(WindowInsetsCompat.Type.displayCutout())
            val ime = insets.getInsets(WindowInsetsCompat.Type.ime())

            // The bars are hidden, so their inset is normally zero — but a
            // transient reveal makes them real for a few seconds, and a HUD that
            // ducks under a status bar the player just summoned is the correct
            // behaviour.
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())

            // THE KEYBOARD RIDES IN ON THE BOTTOM INSET.
            //
            // FORGE has a text field for share codes. With decorFitsSystemWindows
            // false the window does not resize for the IME — it arrives as an
            // inset instead — so nothing moves out of the keyboard's way unless
            // somebody moves it. The page has no notion of a keyboard, but it
            // does have a bottom safe area that every panel already pads
            // against, and folding the IME into it makes the field lift itself
            // using layout the page already had.
            val top = maxOf(cutout.top, bars.top)
            val bottom = maxOf(cutout.bottom, bars.bottom, ime.bottom)
            val left = maxOf(cutout.left, bars.left)
            val right = maxOf(cutout.right, bars.right)

            turnkey?.setInsets(
                top = top / density,
                right = right / density,
                bottom = bottom / density,
                left = left / density,
            )

            // Consumed: nothing below this needs insets, and letting them
            // propagate invites the WebView to pad itself as well.
            WindowInsetsCompat.CONSUMED
        }
    }

    /**
     * TAKE THE SIDE EDGES BACK FROM THE BACK GESTURE.
     *
     * On gesture navigation the left and right edges belong to the system, and a
     * drag that starts there goes back instead of turning the cube. This game is
     * played by dragging across the board, so on a tall phone the most natural
     * possible input — a big horizontal sweep — is also the one most likely to
     * throw the player out to the title screen.
     *
     * A gesture exclusion rect asks the system to leave a strip alone. Android
     * caps the total to 200dp per edge and silently trims anything more, so this
     * claims exactly that much, centred on the middle of the screen where the
     * board is. The top and bottom of each edge stay system gesture area, so
     * back-by-swipe is still there for anyone who reaches for it.
     */
    private fun claimEdgesFromTheBackGesture(view: View) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return
        if (view.width == 0 || view.height == 0) return

        val density = resources.displayMetrics.density
        val strip = (EDGE_STRIP_DP * density).toInt()
        val band = (MAX_EXCLUSION_DP * density).toInt().coerceAtMost(view.height)
        val top = ((view.height - band) / 2).coerceAtLeast(0)
        val bottom = (top + band).coerceAtMost(view.height)

        view.systemGestureExclusionRects = listOf(
            Rect(0, top, strip, bottom),
            Rect(view.width - strip, top, view.width, bottom),
        )
    }

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)

        // The Activity is not recreated on rotation, unfold, theme change or
        // font-scale change — see the configChanges list in the manifest — so
        // this is where all of those land. Density can change with a display-size
        // change, which moves every inset in CSS pixels even when the physical
        // pixels are identical, so the last-sent values are dropped rather than
        // deduped against.
        turnkey?.forgetInsets()
        goEdgeToEdge()
        ViewCompat.requestApplyInsets(container)
        web?.let { claimEdgesFromTheBackGesture(it) }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        // Coming back from the notification shade, a permission dialog or the
        // Play Games overlay leaves the system bars showing. Immersive is a
        // request, not a state, and it has to be made again.
        if (hasFocus) goEdgeToEdge()
    }

    override fun onPause() {
        // ORDER IS LOAD-BEARING. Tell the page first, while its JavaScript can
        // still run: pauseTimers stops the frame loop and the timer queue, and a
        // pause() issued after it may not execute until the app is resumed —
        // by which time the solve clock has banked the entire interruption.
        turnkey?.pause()
        web?.onPause()
        web?.pauseTimers()
        super.onPause()
    }

    override fun onResume() {
        super.onResume()
        web?.resumeTimers()
        web?.onResume()
        turnkey?.resume()

        // Re-armed in case a back press disabled it and the Activity survived.
        backCallback.isEnabled = true
    }

    override fun onDestroy() {
        main.removeCallbacksAndMessages(null)
        web?.let { view ->
            container.removeView(view)
            view.destroy()
        }
        web = null
        turnkey = null
        super.onDestroy()
    }

    private companion object {
        /** Ceiling on the splash hold, for a page that never paints at all. */
        const val SPLASH_LIMIT_MS = 6_000L
        const val SPLASH_FADE_MS = 200L

        /** Width of the strip reclaimed from the system's back gesture. */
        const val EDGE_STRIP_DP = 28f

        /** Android's own cap on gesture exclusion, per edge. */
        const val MAX_EXCLUSION_DP = 200f
    }
}
