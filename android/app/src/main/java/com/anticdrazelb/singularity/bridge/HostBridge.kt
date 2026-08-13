package com.anticdrazelb.singularity.bridge

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Handler
import android.os.Looper
import android.webkit.JavascriptInterface

/**
 * `window.AndroidHost` — the two things the page cannot do for itself.
 *
 * EVERY METHOD HERE RUNS ON A BINDER THREAD, NOT THE MAIN THREAD.
 *
 * This is the defect that WebView bridges ship with more often than any other,
 * because it does not look like one. Chromium calls @JavascriptInterface methods
 * on a private "JavaBridge" thread, so a startActivity or a WebView touch made
 * directly in one of these methods is a cross-thread call into single-threaded
 * UI code. It usually appears to work, and then throws, or corrupts, on some
 * other phone. Everything that touches the UI toolkit below is posted to the
 * main looper. Only the PackageManager query — which is thread-safe and whose
 * answer is the return value — stays where it is.
 */
class HostBridge(
    private val activity: Activity,
    private val onNoMailApp: () -> Unit,
) {
    private val main = Handler(Looper.getMainLooper())

    /**
     * Copy to the clipboard.
     *
     * The page tries this first for a reason of its own: "navigator.clipboard
     * requires a SECURE CONTEXT … on the actual device that API is simply
     * absent". This app does serve the game from a secure https origin, so the
     * web API would in fact work now — but the page asks the host first, and the
     * host answering is still better: it is one call, it cannot be blocked by a
     * permissions policy, and it does not depend on the document being focused.
     *
     * No toast. Android 13+ shows its own copy confirmation, and an app that
     * adds a second one is an app that was written for Android 12.
     */
    @JavascriptInterface
    fun copy(text: String?) {
        val value = text.orEmpty()
        main.post {
            val clipboard = activity.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
            clipboard?.setPrimaryClip(ClipData.newPlainText("SINGULARITY", value))
        }
    }

    /**
     * Hand a composed report to whatever mail app the phone has.
     *
     * The page is candid that this may go nowhere — "THIS CANNOT SEND MAIL AND
     * DOES NOT PRETEND TO" — and copies the whole report to the clipboard on the
     * way past, so a phone with no mail client still leaves the player holding
     * the text. That safety net is why this method does not throw when it fails.
     *
     * ACTION_SENDTO with a mailto: URI is the correct intent: it reaches mail
     * apps only, and never offers to "send" the report to a photo editor. The
     * chooser is the fallback for a phone whose mail app registered for
     * ACTION_SEND alone.
     *
     * @return whether anything on this device claimed the intent. The page uses
     *   a false to fall back to its own mailto: navigation.
     */
    @JavascriptInterface
    fun mail(to: String?, subject: String?, body: String?): Boolean {
        val intent = mailIntent(to.orEmpty(), subject.orEmpty(), body.orEmpty())

        // Resolved here, synchronously, because it is the return value and the
        // page branches on it. Requires the <queries> block in the manifest —
        // without it this returns null on every phone running Android 11+ even
        // when three mail apps are installed.
        val resolvable = intent.resolveActivity(activity.packageManager) != null

        main.post {
            try {
                if (resolvable) {
                    activity.startActivity(intent)
                } else {
                    val fallback = shareIntent(to.orEmpty(), subject.orEmpty(), body.orEmpty())
                    activity.startActivity(Intent.createChooser(fallback, null))
                }
            } catch (_: ActivityNotFoundException) {
                onNoMailApp()
            }
        }
        return resolvable
    }

    /** The page's own fallback navigation lands here rather than in the WebView. */
    fun openMailto(url: String) {
        val intent = Intent(Intent.ACTION_SENDTO, Uri.parse(url))
        try {
            activity.startActivity(intent)
        } catch (_: ActivityNotFoundException) {
            onNoMailApp()
        }
    }

    private fun mailIntent(to: String, subject: String, body: String): Intent =
        Intent(Intent.ACTION_SENDTO).apply {
            // The recipient goes in the URI so that ACTION_SENDTO filters on it;
            // subject and body go in extras because a mailto: query string is
            // honoured by some mail apps and dropped by others.
            data = Uri.parse("mailto:${Uri.encode(to)}")
            putExtra(Intent.EXTRA_EMAIL, arrayOf(to))
            putExtra(Intent.EXTRA_SUBJECT, subject)
            putExtra(Intent.EXTRA_TEXT, body)
        }

    private fun shareIntent(to: String, subject: String, body: String): Intent =
        Intent(Intent.ACTION_SEND).apply {
            type = "message/rfc822"
            putExtra(Intent.EXTRA_EMAIL, arrayOf(to))
            putExtra(Intent.EXTRA_SUBJECT, subject)
            putExtra(Intent.EXTRA_TEXT, body)
        }

    companion object {
        /** The name the page looks this object up under. Fixed by the contract. */
        const val NAME = "AndroidHost"
    }
}
