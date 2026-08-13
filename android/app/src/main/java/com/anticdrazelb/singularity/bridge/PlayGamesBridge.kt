package com.anticdrazelb.singularity.bridge

import android.app.Activity
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.webkit.JavascriptInterface
import com.anticdrazelb.singularity.R
import com.google.android.gms.games.PlayGames
import com.google.android.gms.games.PlayGamesSdk

/**
 * `window.AndroidGPG` — leaderboards, and the discipline of not pretending.
 *
 * ────────────────────────────────────────────────────────────────────────────
 * THE PROBLEM THIS CLASS EXISTS TO SOLVE
 *
 * The page decides whether to show online boards with `!!window.AndroidGPG` —
 * presence of the object *is* the claim that leaderboards work. So the object
 * must not exist until they actually do. A bridge installed unconditionally at
 * startup makes the page draw a BOARDS screen full of entries that submit into
 * nothing, on a phone with no Play Games account.
 *
 * addJavascriptInterface cannot express that, because it only takes effect on
 * the next page load, and sign-in resolves long after the page is up. The way
 * through is a detail the page volunteers about itself:
 *
 *   "THE HOST IS RESOLVED PER CALL, NOT CAPTURED ONCE. A WebView can install
 *    its bridge object after the document has run, and an adapter that made up
 *    its mind at load time would decide nothing was listening and stay wrong
 *    for the rest of the session."
 *
 * So this object is registered before load under a private name, and promoted to
 * `window.AndroidGPG` by one line of JavaScript only once sign-in has actually
 * succeeded. Until then the page's BOARDS screen reads "BOARDS — LOCAL ONLY",
 * which is a designed state in the page, not a broken one.
 *
 * ────────────────────────────────────────────────────────────────────────────
 * AND IT IS OFF BY DEFAULT
 *
 * A checkout with no Play Console behind it must still build and run. [enabled]
 * is false until res/values/strings.xml carries a real application id, and while
 * it is false PlayGamesSdk.initialize is never called — which is what keeps the
 * placeholder in the manifest inert.
 */
class PlayGamesBridge private constructor(
    private val activity: Activity,
    private val boards: Map<String, String>,
) {
    private val main = Handler(Looper.getMainLooper())

    /**
     * Submit a score.
     *
     * The page sends scores as strings on purpose: "A vault total in
     * milliseconds is small enough for a double today, but it lands in a Java
     * long, and a string is the one representation that cannot lose a digit on
     * the way." So it is parsed here, once, and a value that is not a whole
     * number is dropped rather than rounded into a fake record.
     */
    @JavascriptInterface
    fun submit(board: String?, score: String?) {
        val id = boards[board] ?: return logUnmapped(board)
        val value = score?.trim()?.toLongOrNull() ?: return
        main.post {
            runCatching { PlayGames.getLeaderboardsClient(activity).submitScore(id, value) }
                .onFailure { Log.w(TAG, "submit failed for $board", it) }
        }
    }

    /** Open Play Games' own leaderboard UI. */
    @JavascriptInterface
    fun show(board: String?) {
        val id = boards[board] ?: return logUnmapped(board)
        main.post {
            PlayGames.getLeaderboardsClient(activity)
                .getLeaderboardIntent(id)
                .addOnSuccessListener { intent ->
                    runCatching { activity.startActivity(intent) }
                        .onFailure { Log.w(TAG, "could not open board $board", it) }
                }
                .addOnFailureListener { Log.w(TAG, "no intent for board $board", it) }
        }
    }

    private fun logUnmapped(board: String?) {
        // Not an error. A half-filled board map is the normal state during
        // setup, and dropping the unmapped ones is better than submitting a
        // placeholder string to Google.
        Log.i(TAG, "no leaderboard id mapped for '$board' — skipping")
    }

    companion object {
        private const val TAG = "PlayGames"

        /** Registered under this; promoted to AndroidGPG only after sign-in. */
        const val PRIVATE_NAME = "AndroidGPGImpl"

        /** The name the page looks the bridge up under. Fixed by the contract. */
        const val PUBLIC_NAME = "AndroidGPG"

        /** The one line that turns the page's leaderboards on. */
        const val PROMOTE_JS =
            "try{window.$PUBLIC_NAME=window.$PRIVATE_NAME}catch(e){}"

        private const val PLACEHOLDER_APP_ID = "0000000000"
        private const val PLACEHOLDER_BOARD = "CHANGE_ME"

        /**
         * @return a bridge, or null when this build has no Play Games project
         *   configured — in which case nothing is registered and nothing is
         *   initialised.
         */
        fun createIfConfigured(activity: Activity): PlayGamesBridge? {
            val appId = activity.getString(R.string.play_games_app_id).trim()
            if (appId.isEmpty() || appId == PLACEHOLDER_APP_ID) return null

            val boards = readBoardMap(activity)
            if (boards.isEmpty()) {
                Log.i(TAG, "app id is set but no leaderboard ids are mapped — staying local")
                return null
            }
            return PlayGamesBridge(activity, boards)
        }

        /**
         * Initialise, then ask whether there is a signed-in player.
         *
         * [onAuthenticated] runs only on a real yes. Play Games attempts an
         * automatic sign-in on initialize; a player who has declined it, or who
         * has no Play Games account, resolves to false and the page is never
         * told leaderboards exist.
         */
        fun signIn(activity: Activity, onAuthenticated: () -> Unit) {
            try {
                PlayGamesSdk.initialize(activity)
            } catch (t: Throwable) {
                Log.w(TAG, "Play Games failed to initialise — staying local", t)
                return
            }

            PlayGames.getGamesSignInClient(activity).isAuthenticated
                .addOnCompleteListener { task ->
                    val authenticated = task.isSuccessful && task.result.isAuthenticated
                    if (authenticated) onAuthenticated() else Log.i(TAG, "not signed in — staying local")
                }
        }

        /** Parses the `logical=ID` lines from res/values/play_games_boards.xml. */
        private fun readBoardMap(activity: Activity): Map<String, String> =
            activity.resources.getStringArray(R.array.leaderboard_map)
                .mapNotNull { entry ->
                    val separator = entry.indexOf('=')
                    if (separator <= 0) return@mapNotNull null
                    val logical = entry.substring(0, separator).trim()
                    val id = entry.substring(separator + 1).trim()
                    if (logical.isEmpty() || id.isEmpty() || id == PLACEHOLDER_BOARD) null
                    else logical to id
                }
                .toMap()
    }
}
