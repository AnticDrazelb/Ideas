package com.anticdrazelb.singularity

import android.app.Activity
import android.util.Log
import android.webkit.JavascriptInterface
import com.google.android.gms.games.PlayGames
import java.util.concurrent.atomic.AtomicBoolean

/**
 * window.AndroidGPG -- submit and show, and nothing else.
 *
 * PLAY GAMES v2 SIGNS THE PLAYER IN BY ITSELF. There is no sign-in button in
 * this app, no GoogleSignInClient and no account picker: v2 authenticates at
 * launch and the only question left is whether it succeeded. `available()`
 * answers that, and the game uses it to decide whether the BOARDS screen
 * offers rankings or says it is showing local records only.
 *
 * SCORES ARRIVE AS STRINGS. A vault total in milliseconds fits a Double
 * today, but a packed season score does not have room to spare, and the
 * bridge is the exact place a silently-rounded long would be impossible to
 * notice. They are parsed here, once, and a value that will not parse is
 * dropped with a log rather than submitted as zero.
 */
class GpgBridge(private val activity: Activity) {

    private val authed = AtomicBoolean(false)

    /** Called from MainActivity once the SDK has answered. */
    fun setAuthenticated(value: Boolean) = authed.set(value)

    @JavascriptInterface
    fun available(): Boolean = authed.get()

    @JavascriptInterface
    fun submit(board: String, score: String) {
        val id = Boards.idFor(activity, board)
        if (id == null) {
            Log.w(TAG, "no leaderboard id configured for '$board' -- see res/values/leaderboards.xml")
            return
        }
        val value = score.trim().toLongOrNull()
        if (value == null) {
            Log.w(TAG, "refusing to submit unparseable score '$score' to $board")
            return
        }
        if (!authed.get()) {
            // Not signed in. The SDK would queue this, but a score posted for
            // nobody is worse than one not posted: the game keeps its own
            // record and will post again next time it improves.
            Log.i(TAG, "not authenticated; dropping $board=$value")
            return
        }
        PlayGames.getLeaderboardsClient(activity).submitScore(id, value)
        Log.d(TAG, "submitted $board ($id) = $value")
    }

    @JavascriptInterface
    fun show(board: String) {
        val id = Boards.idFor(activity, board)
        if (id == null) {
            Log.w(TAG, "no leaderboard id configured for '$board'")
            return
        }
        if (!authed.get()) return
        PlayGames.getLeaderboardsClient(activity)
            .getLeaderboardIntent(id)
            .addOnSuccessListener { intent ->
                activity.runOnUiThread {
                    // The result is never read -- the player is looking at a
                    // board, not choosing something -- but the API is an
                    // Intent and it has to be started for a result to render
                    // the Play Games UI correctly.
                    activity.startActivityForResult(intent, RC_LEADERBOARD)
                }
            }
            .addOnFailureListener { e -> Log.w(TAG, "could not open board $board", e) }
    }

    companion object {
        const val RC_LEADERBOARD = 9004
        private const val TAG = "SingularityGPG"
    }
}
