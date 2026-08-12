package com.anticdrazelb.singularity

/**
 * THE GAME NEVER LEARNS A GOOGLE BOARD ID.
 *
 * index.html sends a LOGICAL name -- "daily", "vault_07" -- and this is the
 * only place that knows what those mean to Play Games. Board ids can be
 * reissued, or a whole game re-created in the console, without the web layer
 * being rebuilt or even opened.
 *
 * The ids themselves live in res/values/leaderboards.xml, one string each,
 * so they are pasted from the Play Console into a file that contains nothing
 * else. An empty entry is treated as "not configured yet" and the bridge
 * declines rather than calling the SDK with a blank id.
 */
object Boards {

    private val ids: Map<String, Int> = buildMap {
        put("daily",        R.string.lb_daily)
        put("daily_week",   R.string.lb_daily_week)
        put("daily_month",  R.string.lb_daily_month)
        put("daily_season", R.string.lb_daily_season)
        put("daily_streak", R.string.lb_daily_streak)
        // Thirty ranked vaults. vault_01 is CALIBRATION, vault_30 is the last
        // one that carries a board; the game does not send anything past it.
        put("vault_01", R.string.lb_vault_01); put("vault_02", R.string.lb_vault_02)
        put("vault_03", R.string.lb_vault_03); put("vault_04", R.string.lb_vault_04)
        put("vault_05", R.string.lb_vault_05); put("vault_06", R.string.lb_vault_06)
        put("vault_07", R.string.lb_vault_07); put("vault_08", R.string.lb_vault_08)
        put("vault_09", R.string.lb_vault_09); put("vault_10", R.string.lb_vault_10)
        put("vault_11", R.string.lb_vault_11); put("vault_12", R.string.lb_vault_12)
        put("vault_13", R.string.lb_vault_13); put("vault_14", R.string.lb_vault_14)
        put("vault_15", R.string.lb_vault_15); put("vault_16", R.string.lb_vault_16)
        put("vault_17", R.string.lb_vault_17); put("vault_18", R.string.lb_vault_18)
        put("vault_19", R.string.lb_vault_19); put("vault_20", R.string.lb_vault_20)
        put("vault_21", R.string.lb_vault_21); put("vault_22", R.string.lb_vault_22)
        put("vault_23", R.string.lb_vault_23); put("vault_24", R.string.lb_vault_24)
        put("vault_25", R.string.lb_vault_25); put("vault_26", R.string.lb_vault_26)
        put("vault_27", R.string.lb_vault_27); put("vault_28", R.string.lb_vault_28)
        put("vault_29", R.string.lb_vault_29); put("vault_30", R.string.lb_vault_30)
    }

    /** Real Play Games id for a logical name, or null if unknown or unset. */
    fun idFor(ctx: android.content.Context, logical: String): String? {
        val res = ids[logical] ?: return null
        val value = ctx.getString(res).trim()
        return value.ifEmpty { null }
    }

    /** Every logical name this build knows about. Used only for logging. */
    fun known(): Set<String> = ids.keys
}
