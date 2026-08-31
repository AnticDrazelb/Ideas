class_name DailyBoards
# THE DAILY, SCORED OVER WINDOWS — and the half of it that needs no server.
#
# A MISS COSTS MORE THAN THE WORST HONEST ATTEMPT. A missed day is not a zero, it
# is a MISS, and if it were free then skipping a hard cube would be the optimal
# play. So an absent day is worth DAY_MISS, and a real attempt is clamped to one
# below it — which means turning up and playing badly always beats not playing,
# and that is the ordering a habit board has to have.
#
# A FRESH INSTALL MUST NOT OPEN BY POSTING A WALL OF PENALTIES for a month it was
# not installed for, so a window with nothing at all in it scores -1 and is not
# posted.
#
# THE PERIOD RIDES ABOVE THE SCORE. Packing the period number above the inverted
# total means the newest completed window outranks every older one by
# construction — so a service that keeps only a player's best entry turns into
# current-period standings on its own. No reset, no server-side job, one slot,
# and it never stops working.
#
# Everything here is computed locally and is worth reading offline. The only part
# that needs a host is the last step: handing the packed number to a ranking
# service. `submit` is where that goes, and it is deliberately a hook rather than
# a stub of somebody's SDK.

const DAY_MISS := 120
const SEASON_DAYS := Daily.SEASON_DAYS

# [key, board, label, why, which]  — `which` selects the window functions below,
# because GDScript 3.5 has no function values to put in a table.
const WEEK := 0
const MONTH := 1
const SEASON := 2

const PERIODS := [
	["w", "daily_week", "THIS WEEK", "SEVEN DAYS, TOTALLED", WEEK],
	["m", "daily_month", "THIS MONTH", "A CALENDAR MONTH, TOTALLED", MONTH],
	["s", "daily_season", "SEASON", "NINETY-ONE DAYS, TOTALLED", SEASON],
]

# Called with a board id and a packed score when a window closes. Wire a ranking
# service here; leave it alone and the game is exactly as complete as it is now,
# minus the ranking. A one-element array because there is nowhere else in
# GDScript 3.5 for a static to keep a value.
const _SUBMIT := []


static func on_submit(target: Object, method: String) -> void:
	_SUBMIT.clear()
	_SUBMIT.append(funcref(target, method))


static func submit(board: String, score: int) -> void:
	if not _SUBMIT.empty():
		_SUBMIT[0].call_func(board, score)


static func period_of(which: int, day: int) -> int:
	match which:
		WEEK:
			return Daily.week_of(day)
		MONTH:
			return Daily.month_of(day)
		_:
			return Daily.season_of(day)


static func period_span(which: int, p: int) -> Array:
	match which:
		WEEK:
			return Daily.week_span(p)
		MONTH:
			return Daily.month_span(p)
		_:
			return Daily.season_span(p)


# The total for a window, or -1 when the player has nothing at all in it.
static func span_total(span: Array) -> int:
	var t := 0
	var any := false
	var hist := Store.daily_history()
	for i in range(span[0], span[1] + 1):
		if hist.has(i):
			t += hist[i]
			any = true
		else:
			t += DAY_MISS
	return t if any else -1


# The window in progress, for display only. A running total is all penalty for
# the days that have not happened yet, so the days still to come are left out of
# it — this is "how am I doing", not a score.
# Returns [total, played, of].
static func running(which: int, today: int) -> Array:
	var span := period_span(which, period_of(which, today))
	var last: int = min(span[1], today)
	var t := 0
	var played := 0
	var days := 0
	var hist := Store.daily_history()
	for i in range(span[0], last + 1):
		days += 1
		if hist.has(i):
			t += hist[i]
			played += 1
		else:
			t += DAY_MISS
	return [t, played, days]


# ---- recording -------------------------------------------------------------

# One clear of today's cube. Keeps the best, advances the streak, closes any
# window that has finished, and prunes the history back to the longest span that
# still reads it.
static func record(folds: int) -> void:
	var today := Daily.day_index()

	# clamped, so turning up and playing badly always beats not playing
	var f: int = min(folds, DAY_MISS - 1)

	var hist := Store.daily_history()
	if not hist.has(today) or f < hist[today]:
		Store.set_daily(today, f)
		submit("daily", f)                         # a service buckets this one by arrival

	var st := Store.data()
	if st.streak_last != today:
		st.streak_cur = st.streak_cur + 1 if st.streak_last == today - 1 else 1
		st.streak_last = today
		if st.streak_cur > st.streak_best:
			st.streak_best = st.streak_cur
			submit("daily_streak", st.streak_best)

	_post_periods(today)
	# the history only has to reach back as far as the longest window that reads
	# it; the streak is carried forward instead of derived, so nothing here needs
	# to remember a year
	Store.prune_daily(today, SEASON_DAYS + 31)
	Store.save()


# Posted for the window that has FINISHED, never the one in progress. Marked even
# when there is nothing to send, so an empty period is not recomputed on every
# clear for the rest of the next one.
static func _post_periods(today: int) -> void:
	for p in PERIODS:
		var done: int = period_of(p[4], today) - 1
		if _posted(p[0]) == done:
			continue
		var total := span_total(period_span(p[4], done))
		_mark(p[0], done)
		if total >= 0:
			submit(p[1], packed(done, total))


# The period above the inverted total. The field is ten million wide against a
# worst case of ninety-one days at a hundred and twenty apiece — 10,920, three
# orders of magnitude of headroom.
static func packed(period: int, total: int) -> int:
	return Daily.packed(period, total)


static func _posted(key: String) -> int:
	if key == "w":
		return Store.data().post_w
	if key == "m":
		return Store.data().post_m
	return Store.data().post_s


static func _mark(key: String, v: int) -> void:
	if key == "w":
		Store.data().post_w = v
	elif key == "m":
		Store.data().post_m = v
	else:
		Store.data().post_s = v
