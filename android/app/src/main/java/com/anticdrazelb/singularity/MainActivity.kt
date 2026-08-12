package com.anticdrazelb.singularity

import android.annotation.SuppressLint
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.util.Log
import android.view.View
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.webkit.WebViewAssetLoader
import com.google.android.gms.games.PlayGames
import com.google.android.gms.games.PlayGamesSdk

class MainActivity : AppCompatActivity() {

    private lateinit var web: WebView
    private lateinit var gpg: GpgBridge

    /**
     * THE GAME IS SERVED OVER https, NOT file://, AND THAT IS DELIBERATE.
     *
     * A page loaded from file:// is not a SECURE CONTEXT, which means
     * navigator.clipboard does not exist, and localStorage under file:// is
     * treated inconsistently across WebView versions. WebViewAssetLoader
     * serves the same asset from an https origin that never leaves the
     * device, so the clipboard API works, storage behaves like a normal
     * origin, and no network is involved at any point.
     */
    private val assetLoader: WebViewAssetLoader by lazy {
        WebViewAssetLoader.Builder()
            .setDomain(ASSET_DOMAIN)
            .addPathHandler("/assets/", WebViewAssetLoader.AssetsPathHandler(this))
            .build()
    }

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)

        // v2 authenticates on its own. Initialising before anything asks a
        // question means available() is usually already true by the time the
        // player reaches a screen that cares.
        PlayGamesSdk.initialize(this)

        web = WebView(this)
        setContentView(web)

        WindowCompat.setDecorFitsSystemWindows(window, false)
        goImmersive()
        configureWebView()
        wireInsets()
        wireBack()

        if (savedInstanceState == null) {
            web.loadUrl("https://$ASSET_DOMAIN/assets/index.html")
        } else {
            web.restoreState(savedInstanceState)
        }

        checkSignIn()
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun configureWebView() = with(web) {
        setBackgroundColor(0xFF000000.toInt())   // no white flash before the first frame
        overScrollMode = View.OVER_SCROLL_NEVER  // no blue glow when a menu bottoms out
        isVerticalScrollBarEnabled = false
        isHorizontalScrollBarEnabled = false

        settings.javaScriptEnabled = true
        // localStorage. The game keeps every record in one key and silently
        // forgets everything without this -- no crash, just a save that never
        // survives a restart.
        settings.domStorageEnabled = true
        settings.mediaPlaybackRequiresUserGesture = false
        settings.allowFileAccess = false
        settings.allowContentAccess = false
        settings.setSupportZoom(false)
        settings.builtInZoomControls = false
        settings.displayZoomControls = false
        // The HUD measures itself and shrinks to fit; system font scaling
        // would fight that fitter rather than help it, and at 200% there is
        // no size the chips could shrink to that would still be legible.
        settings.textZoom = 100

        if (BuildConfig.DEBUG) WebView.setWebContentsDebuggingEnabled(true)

        addJavascriptInterface(HostBridge(this@MainActivity), "AndroidHost")
        gpg = GpgBridge(this@MainActivity)
        addJavascriptInterface(gpg, "AndroidGPG")

        webViewClient = object : WebViewClient() {
            override fun shouldInterceptRequest(
                view: WebView, request: WebResourceRequest
            ): WebResourceResponse? = assetLoader.shouldInterceptRequest(request.url)

            override fun shouldOverrideUrlLoading(
                view: WebView, request: WebResourceRequest
            ): Boolean {
                val url = request.url
                // The game only ever loads itself. Anything with another
                // scheme is a mailto: fallback or an external link, and a
                // WebView handles neither -- left alone it shows
                // ERR_UNKNOWN_URL_SCHEME inside the game.
                if (url.host == ASSET_DOMAIN) return false
                return try {
                    startActivity(Intent(Intent.ACTION_VIEW, url))
                    true
                } catch (e: Exception) {
                    Log.w(TAG, "nothing can open $url", e)
                    true
                }
            }

            override fun onPageFinished(view: WebView, url: String) {
                pushInsets(ViewCompat.getRootWindowInsets(view))
                pushSignIn()
            }
        }
    }

    /**
     * THE WEBVIEW IS NOT CHROMIUM'S WINDOW, so env(safe-area-inset-*) reads
     * ZERO inside it however correctly viewport-fit is set. The game knows
     * this and takes the real numbers through TURNKEY.setInsets instead.
     * They are converted to CSS pixels here, because the page is measured in
     * those and Android hands out physical ones.
     */
    private fun wireInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(web) { _, insets ->
            pushInsets(insets)
            insets
        }
    }

    private fun pushInsets(insets: WindowInsetsCompat?) {
        if (insets == null) return
        val bars = insets.getInsets(
            WindowInsetsCompat.Type.systemBars() or WindowInsetsCompat.Type.displayCutout()
        )
        val d = resources.displayMetrics.density
        val t = (bars.top / d).toInt()
        val r = (bars.right / d).toInt()
        val b = (bars.bottom / d).toInt()
        val l = (bars.left / d).toInt()
        call("window.TURNKEY && window.TURNKEY.setInsets($t,$r,$b,$l)")
    }

    /**
     * The game owns its own navigation stack -- cards, vaults, the forge --
     * and answers whether it consumed the press. Only when it says no does
     * this leave the game.
     *
     * evaluateJavascript is ASYNCHRONOUS, so the decision cannot be returned
     * from the callback. The callback disables itself and re-dispatches
     * instead, which is the only way to hand a press back to the system after
     * having already accepted it.
     */
    private fun wireBack() {
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                web.evaluateJavascript("window.TURNKEY ? String(window.TURNKEY.onBack()) : \"false\"") { result ->
                    if (result?.contains("true") != true) {
                        isEnabled = false
                        onBackPressedDispatcher.onBackPressed()
                    }
                }
            }
        })
    }

    private fun goImmersive() {
        WindowInsetsControllerCompat(window, web).apply {
            hide(WindowInsetsCompat.Type.systemBars())
            systemBarsBehavior =
                WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }
    }

    private fun checkSignIn() {
        PlayGames.getGamesSignInClient(this).isAuthenticated
            .addOnCompleteListener { task ->
                val ok = task.isSuccessful && task.result.isAuthenticated
                gpg.setAuthenticated(ok)
                Log.i(TAG, if (ok) "Play Games authenticated" else "Play Games not authenticated")
                pushSignIn()
            }
    }

    /** Rebuilds the boards screen if the player happens to be looking at it. */
    private fun pushSignIn() {
        call("window.buildBoards && !document.getElementById('scBoards').classList.contains('hide') && buildBoards()")
    }

    private fun call(js: String) {
        web.post { web.evaluateJavascript(js, null) }
    }

    override fun onPause() {
        super.onPause()
        call("window.TURNKEY && window.TURNKEY.pause()")
        web.onPause()
    }

    override fun onResume() {
        super.onResume()
        web.onResume()
        goImmersive()
        call("window.TURNKEY && window.TURNKEY.resume()")
        // Coming back from the Play Games UI is a chance for sign-in to have
        // changed underneath the game.
        checkSignIn()
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        web.saveState(outState)
    }

    override fun onDestroy() {
        web.destroy()
        super.onDestroy()
    }

    private companion object {
        const val ASSET_DOMAIN = "appassets.androidplatform.net"
        const val TAG = "Singularity"
    }
}
