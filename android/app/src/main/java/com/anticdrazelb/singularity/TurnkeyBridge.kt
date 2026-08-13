package com.anticdrazelb.singularity

import android.webkit.WebView

/**
 * THE HALF OF THE CONTRACT THAT THE HOST CALLS.
 *
 * The page publishes exactly one object, `window.TURNKEY`, and its own comment
 * says why: "A packaged WebView has no address bar, so the app owns the back
 * gesture, the lifecycle, and the insets. All three are published on one object
 * rather than scattered across globals the host has to guess at."
 *
 * This class is the Kotlin end of that object and nothing else. It does not
 * know what a fold is, and the page does not know what an Activity is.
 *
 * EVERY CALL IS GUARDED ON THE PAGE STILL BEING THERE. `window.TURNKEY` is
 * defined at the very bottom of a 9,000-line inline script, so for the first
 * moments of a cold start — and again for the whole of a renderer rebuild — it
 * genuinely does not exist. A bare `window.TURNKEY.pause()` in that window
 * throws inside the WebView, which surfaces as a console error nobody reads and
 * a pause that never happened.
 */
class TurnkeyBridge(private val web: WebView) {

    /** Guards against re-sending identical insets on every layout pass. */
    private var lastInsets: String? = null

    /**
     * Ask the page to handle a back gesture.
     *
     * The page walks its own screen stack — win → cubes, manual → pause, pause →
     * board, and so on — and returns true for "handled, keep the Activity
     * alive". It returns false only at the point where there is nothing left to
     * close, which is the one place Android should be allowed to finish us.
     *
     * The answer arrives asynchronously because evaluateJavascript is
     * asynchronous; there is no synchronous way to ask a WebView a question.
     */
    fun onBack(answer: (handled: Boolean) -> Unit) {
        eval(
            "(function(){try{" +
                "return !!(window.TURNKEY&&window.TURNKEY.onBack&&window.TURNKEY.onBack());" +
                "}catch(e){return false}})()"
        ) { result -> answer(result == "true") }
    }

    /**
     * Stop the frame loop and, more importantly, the solve clock.
     *
     * The page: "the solve clock stops with the frame loop: a cube left running
     * while the Activity is backgrounded would otherwise bank the whole phone
     * call." Both sides of this are idempotent — the page's clockHold and
     * clockGo are each guarded by a flag — so it does not matter that the page's
     * own visibilitychange listener may fire for the same event.
     */
    fun pause() = eval("try{window.TURNKEY&&window.TURNKEY.pause()}catch(e){}")

    fun resume() = eval("try{window.TURNKEY&&window.TURNKEY.resume()}catch(e){}")

    /**
     * Hand the page the real display cutout.
     *
     * This is the seam the stylesheet was built around. Its comment: "AN ANDROID
     * WEBVIEW DOES NOT KNOW WHERE THE NOTCH IS. env(safe-area-inset-*) is
     * plumbed through Chromium's own display-cutout handling, and a WebView is a
     * View inside somebody else's Activity — not Chromium's window. So in a
     * packaged Android app every one of these reads ZERO however correctly the
     * host sets viewport-fit."
     *
     * Values are CSS pixels, already divided by density by the caller. The page
     * appends 'px' and calls its own fit().
     */
    fun setInsets(top: Float, right: Float, bottom: Float, left: Float) {
        val call = "${fmt(top)},${fmt(right)},${fmt(bottom)},${fmt(left)}"
        if (call == lastInsets) return
        lastInsets = call
        eval("try{window.TURNKEY&&window.TURNKEY.setInsets($call)}catch(e){}")
    }

    /**
     * Forget what we last sent, so the next setInsets is treated as new.
     *
     * Needed after a page load: the new document has default insets and has
     * never heard from us, but this object may still be holding the values it
     * sent to the *previous* document and would dedupe the resend away.
     */
    fun forgetInsets() {
        lastInsets = null
    }

    /** Two decimals is finer than a physical pixel at any density that ships. */
    private fun fmt(v: Float): String = String.format(java.util.Locale.US, "%.2f", v)

    private fun eval(js: String, then: ((String) -> Unit)? = null) {
        // evaluateJavascript must be called on the thread the WebView was made
        // on. Bridge callbacks arrive on a binder thread, so this is not
        // theoretical.
        web.post { web.evaluateJavascript(js) { value -> then?.invoke(value) } }
    }
}
