class_name Daily
# THE DAILY VAULT — one engine a day, the same one for everybody.
#
# THE SEED IS THE DATE, and that is the whole design. There is no server, no
# fetch, no clock to trust and nothing to go down: every copy of this game
# computes the same integer from the same calendar day and mints the same cube
# from it, because the generator has been deterministic from a single seed since
# the first build.
#
# IT IS CUT AT VAULT V's DIFFICULTY, ALWAYS. A daily that scaled with progress
# would be a different puzzle for each player and the shared score would mean
# nothing; one that scaled with the date would be unplayable by March. Fixed
# spec, fixed shape, one seed a day.
#
# THE DAY TURNS AT MIDNIGHT UTC-7, GLOBALLY. Local rollover is the nicer answer
# while nobody is counting, and a ranked daily cannot have it: a shared ranking
# of "today's cube" needs everyone to agree on which day it is, so the boundary
# has to be the same instant everywhere. The cost is a rollover at 08:00 in
# London instead of midnight, and that is the price of the board.

const SPEC_LEVEL := 48                     # vault V demand, forever
const DAY_ANCHOR_MS := 7 * 3600000         # midnight UTC-7, no DST shift

const _CACHE := {}


static func now_ms() -> int:
	return OS.get_system_time_msecs()


static func day_index() -> int:
	return day_index_at(now_ms())


static func day_index_at(now: int) -> int:
	return int(floor((now - DAY_ANCHOR_MS) / 86400000.0))


# The calendar date the day belongs to, read on the same clock the index was cut
# on — a label from local time would disagree with it for seven hours out of
# every twenty-four.
static func day_date(day: int) -> Dictionary:
	return OS.get_datetime_from_unix_time((day * 86400000 + DAY_ANCHOR_MS) / 1000)


static func day_label(day: int = -0x7FFFFFFF) -> String:
	if day == -0x7FFFFFFF:
		day = day_index()
	var d := day_date(day)
	return "%d.%02d.%02d" % [d.year, d.month, d.day]


static func daily_seed(day: int = -0x7FFFFFFF) -> int:
	if day == -0x7FFFFFFF:
		day = day_index()
	var h := Bits.s32((day * 2654435761) & 0xFFFFFFFF) ^ 0x5eed1e
	h = Rng.imul(h ^ Rng.ushr(h, 15), Bits.s32(2246822507))
	h = Rng.imul(h ^ Rng.ushr(h, 13), Bits.s32(3266489909))
	return (h ^ Rng.ushr(h, 16)) & 0xFFFFFFFF


# One mint a day, kept, because minting is the most expensive thing this game
# does and the answer cannot change until the date does.
static func data() -> Level:
	var day := day_index()
	if _CACHE.has(day):
		return _CACHE[day]
	_CACHE.clear()
	var lv := Generator.mint(SPEC_LEVEL, daily_seed(day))
	lv.name = "DAILY " + day_label(day)
	_CACHE[day] = lv
	return lv


# ---- the periods a daily score is ranked in -------------------------------
#
# A missed day is not a zero, it is a MISS, and a miss has to cost more than the
# worst honest attempt or skipping a hard cube would be free.

const DAY_MISS := 120
const SEASON_DAYS := 91


static func week_of(day: int) -> int:
	return int(floor((day - 3) / 7.0))


static func week_span(w: int) -> Array:
	var a := 3 + 7 * w
	return [a, a + 6]


static func month_of(day: int) -> int:
	var d := day_date(day)
	return d.year * 12 + (d.month - 1)


# The day indices belonging to a month, and THE ANCHOR IS NOT SUBTRACTED HERE
# even though month_of applies it. That looks inconsistent and is the only
# arithmetic that makes the two inverses of each other.
#
# Day d covers the instant d*86400000 + anchor. For that to land on or after a
# month beginning at M you need d >= (M - anchor)/86400000, which is
# M/86400000 - 0.29 — and the smallest integer satisfying it is M/86400000
# exactly, because the day boundary the anchor introduces falls INSIDE the first
# day rather than before it. Subtracting the anchor and flooring gives
# M/86400000 - 1, which is a day early at both ends: every month's window would
# start on the last day of the previous one, so a player's best day would be
# counted twice and a missed day charged twice.
static func month_span(m: int) -> Array:
	var year := m / 12
	var mon := m % 12
	var fsec := OS.get_unix_time_from_datetime({
		"year": year, "month": mon + 1, "day": 1,
		"hour": 0, "minute": 0, "second": 0})
	var ny := year + 1 if mon == 11 else year
	var nm := 1 if mon == 11 else mon + 2
	var nsec := OS.get_unix_time_from_datetime({
		"year": ny, "month": nm, "day": 1,
		"hour": 0, "minute": 0, "second": 0})
	return [int(fsec / 86400), int(nsec / 86400) - 1]


static func season_of(day: int) -> int:
	return int(floor(day / float(SEASON_DAYS)))


static func season_span(s: int) -> Array:
	return [s * SEASON_DAYS, s * SEASON_DAYS + SEASON_DAYS - 1]


# Packing a period and a total into one sortable integer, best-first.
const PACK := 10000000


static func packed(period: int, total: int) -> int:
	return period * PACK + (PACK - 1 - total)
