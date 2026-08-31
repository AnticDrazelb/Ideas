class_name SolveResult
var ok := false
var turns := 0
var steps := 0
var first = null          # Act, or null
var has_first := false

# THE WHOLE LINE, not just its head.
#
# The report already walks the parent chain end to end — it has to, to count
# the steps — and then kept one act of it. A hint wants only the first, which is
# why it was written that way, but ANYTHING THAT REPLAYS A SOLUTION NEEDS ALL
# OF IT: asking again after each move does not work, because a step costs
# nothing, so two states can each be one fold from the core and each have the
# other as the head of an optimal line. A driver following `first` walks between
# them forever. That is not a theory — it is what the finish checks did on 24
# cubes before this existed.
var line := []

# The search was cut short by the budget rather than exhausted — so the cube may
# well be solvable, just not within the folds we were willing to look for. The
# HUD reads this as "40+" rather than "no route", which is the difference
# between an honest unknown and a false accusation.
var over := false
