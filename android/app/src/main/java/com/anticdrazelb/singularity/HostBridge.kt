package com.anticdrazelb.singularity

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.util.Log
import android.webkit.JavascriptInterface

/**
 * window.AndroidHost -- mail and clipboard.
 *
 * EVERY METHOD HERE RUNS ON A BINDER THREAD, NOT THE UI THREAD. That is the
 * rule for @JavascriptInterface and it is easy to forget because most calls
 * appear to work anyway until one of them touches a View and throws from a
 * stack trace that mentions nothing recognisable. Everything that touches
 * Android UI is posted to the main thread here.
 *
 * The game degrades on its own if this object is absent -- it falls back to
 * a mailto: URL and to execCommand for copy -- so nothing here is load
 * bearing for the game running. It is load bearing for those two features
 * actually working.
 */
class HostBridge(private val activity: Activity) {

    /**
     * Hands a finished report to whatever mail app exists.
     *
     * ACTION_SENDTO with a mailto: URI, not ACTION_SEND: SENDTO addresses
     * mail clients specifically, where SEND would offer the whole share sheet
     * and let somebody "report" a cube into a photo editor.
     */
    @JavascriptInterface
    fun mail(to: String, subject: String, body: String) {
        activity.runOnUiThread {
            val intent = Intent(Intent.ACTION_SENDTO, Uri.parse("mailto:" + Uri.encode(to))).apply {
                putExtra(Intent.EXTRA_SUBJECT, subject)
                putExtra(Intent.EXTRA_TEXT, body)
            }
            try {
                activity.startActivity(Intent.createChooser(intent, activity.getString(R.string.send_report)))
            } catch (e: ActivityNotFoundException) {
                // No mail client. The web layer has already put the whole
                // message on the clipboard, so this is recoverable and the
                // game has already said so.
                Log.w(TAG, "no mail client for the report", e)
            }
        }
    }

    /** Puts text on the clipboard. */
    @JavascriptInterface
    fun copy(text: String) {
        activity.runOnUiThread {
            val cm = activity.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
            cm.setPrimaryClip(ClipData.newPlainText(activity.getString(R.string.clip_label), text))
        }
    }

    /** Share sheet for a cube code, for anything that is not mail. */
    @JavascriptInterface
    fun share(text: String) {
        activity.runOnUiThread {
            val intent = Intent(Intent.ACTION_SEND).apply {
                type = "text/plain"
                putExtra(Intent.EXTRA_TEXT, text)
            }
            try {
                activity.startActivity(Intent.createChooser(intent, activity.getString(R.string.share_cube)))
            } catch (e: ActivityNotFoundException) {
                Log.w(TAG, "nothing to share with", e)
            }
        }
    }

    private companion object { const val TAG = "SingularityHost" }
}
