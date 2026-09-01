extends Node
class_name ForgeScreens

# THE FORGE, on screen: a shelf of what the player has built, and the editor that
# builds it.
#
# The editor works ONE DECK AT A TIME and that is the whole interaction design.
# Direct manipulation of a projected solid is a lovely demo and a miserable tool —
# the thing you want to click is behind the thing you can see, and on a phone your
# thumb covers both. A deck is an n-by-n grid you can hit accurately, and the deck
# stepper is the only spatial reasoning the editor asks for.
#
# One instance behind a static front door, for the same reason Screens has one:
# everything on these two screens is a callback and a FuncRef needs an object.
const _S := []


static func i() -> ForgeScreens:
	return _S[0] if not _S.empty() else null


var _dir = null
var _ed: Forge = null

# shelf
var _shelf_grid: UiRect
var _shelf_count: Label
var _shelf_msg: Label
var _import_field: UiField

# editor
var _slice: UiRect
var _deck: UiRect
var _tool_row: UiRect
var _coach: UiRect
var _deck_label: Label
var _ed_msg: Label
var _code_field: UiField
var _coach_n: Label
var _coach_text: Label
var _name_field: UiField
var _size_btn: UiButton
var _delete_btn: UiButton
var _verify_btn: UiButton
var _save_btn: UiButton


# One band of the editor column: what it is, how tall, and how much air after it.
# Kept in a list rather than baked into a cursor at build time because two of them
# come and go — see _flow_editor.
class EdBand:
	var rt: UiRect
	var h := 0.0
	var gap := 0.0
	var on := true


var _bands := []
var _slice_band: EdBand
var _code_band: EdBand
var _del_band: EdBand

const ED_TOP := 84.0
const ED_FOOT := 28.0
const ED_SIDE := 40.0

# Which cube DELETE is currently armed for. Cleared by leaving the screen.
var _armed_delete = null


static func build(dir) -> void:
	var f: ForgeScreens = Make.of("res://ui/ForgeScreens.gd")
	f.name = "ForgeScreens"
	f._dir = dir
	_S.clear()
	_S.append(f)
	Screens.i().add_child(f)

	f._build_shelf()
	f._build_editor()


# ---- the shelf -------------------------------------------------------------

func _build_shelf() -> void:
	var l := Screens.layer("forge")
	Screens.solid_layer(l)

	_shelf_count = UiKit.label(l, "count", "FORGE", 34, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(0, -140), Vector2(0, -90))

	# the rack stops above the import row, which grew to a finger — see below
	_shelf_grid = UiKit.rect(l, "grid", Vector2(0, 0), Vector2(1, 1),
			Vector2(40, 354), Vector2(-40, -170))

	# THE CODE IS THE CARGO. A generated cube has a seed because a seed is what made
	# it; an authored cube was not made by anything, so the code has to carry the
	# voxels themselves.
	# EIGHTY-EIGHT. A text field and the button beside it are sixty-six units tall
	# in the C#, which is a field somebody has to hit while holding a phone and a
	# code they have to paste into it.
	var row := UiKit.rect(l, "importRow", Vector2(0, 0), Vector2(1, 0),
			Vector2(40, 250), Vector2(-40, 338))
	_import_field = UiKit.field(row, "code", "PASTE A CUBE CODE",
			Vector2(0, 0), Vector2(0.72, 1), Vector2.ZERO, Vector2(-8, 0))
	var load_slot := UiKit.rect(row, "loadSlot", Vector2(0.72, 0), Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	UiKit.bracketed(load_slot, "load", "LOAD", funcref(self, "_do_import"), 24)

	_shelf_msg = UiKit.label(l, "msg", "", 18, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 0), Vector2(1, 0), Vector2(40, 210), Vector2(-40, 246))

	# SIDE BY SIDE, NOT STACKED. Two full-width bars in a hundred pixels gives two
	# controls forty pixels tall and four hundred wide, which is a shape no thumb
	# wants and no eye reads as a button. Halved, they come out close to square — and
	# these two are alternatives to each other rather than steps in a sequence, which
	# is what a row says and a stack does not.
	# and the two below it, which were six units short of the same rule
	var bot := UiKit.rect(l, "bot", Vector2(0, 0), Vector2(1, 0), Vector2(50, 96), Vector2(-50, 184))
	var mk := UiKit.rect(bot, "mk", Vector2(0, 0), Vector2(0.5, 1), Vector2.ZERO, Vector2(-7, 0))
	# RESUME, NOT NEW. The editor mirrors itself into a draft after every change, so
	# the cube somebody was halfway through when Android reclaimed the app is still
	# there. A button that says NEW CUBE and silently bins twenty minutes of work is
	# the worst kind.
	UiKit.bracketed(mk, "NEW CUBE", "NEW CUBE", funcref(self, "_new_cube"), 24, true)
	var bk := UiKit.rect(bot, "bk", Vector2(0.5, 0), Vector2.ONE, Vector2(7, 0), Vector2.ZERO)
	UiKit.bracketed(bk, "BACK", "BACK", funcref(self, "_on_back"), 24)


func _on_back(_a = null) -> void:
	Screens.back()


func _on_menu(_a = null) -> void:
	Screens.show_title()


func _new_cube(_a = null) -> void:
	open_editor(Forge.resume())


func _do_import(_a = null) -> void:
	var r := Forge.import_code(_import_field.text())
	_shelf_msg.text = r[1]
	_shelf_msg.add_color_override("font_color", Palette.NODE if r[0] else Palette.fault())
	if r[0]:
		_import_field.set_text("")
		_paint_shelf()


static func open_shelf() -> void:
	var me := i()
	me._armed_delete = null
	me._paint_shelf()
	Screens.show("forge")


func _paint_shelf() -> void:
	for c in _shelf_grid.get_children():
		_shelf_grid.remove_child(c)
		c.queue_free()

	var made: Array = Store.data().made
	_shelf_count.text = "FORGE" if made.empty() else ("FORGE · " + str(made.size()))
	if made.empty():
		UiKit.label(_shelf_grid, "empty",
				"NOTHING BUILT YET.\n\nA cube is edited one deck at a time.\nThe solver proves it before you can save it.",
				20, Palette.dim(), UiKit.Anchor.MIDDLE_CENTER,
				Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO, true)
		return

	var cols := 2
	var rows: int = int(max(4, ceil(made.size() / float(cols))))
	for idx in range(made.size()):
		var m: MadeCube = made[idx]
		var cx := idx % cols
		var cy := idx / cols
		var slot := UiKit.rect(_shelf_grid, "m" + str(idx),
				Vector2(cx / float(cols), 1.0 - (cy + 1.0) / rows),
				Vector2((cx + 1.0) / cols, 1.0 - cy / float(rows)),
				Vector2(6, 6), Vector2(-6, -6))

		var face := m.name + "\n" + str(m.n) + "³ · PAR " + str(m.par) \
				+ (" · BEST " + str(m.best) if m.best >= 0 else "")
		var act := UiAct.on(slot, self, "_shelf_menu", m)
		UiKit.bracketed(slot, "b", face, funcref(act, "press"), 18)


func _shelf_menu(m: MadeCube) -> void:
	# Two things a built cube can do, and no submenu for two things.
	_shelf_msg.text = m.name + " — " + m.id
	_shelf_msg.add_color_override("font_color", Palette.dim())
	open_editor(Forge.from_record(m))


# ---- the editor ------------------------------------------------------------

func _build_editor() -> void:
	var l := Screens.layer("forgeEdit")
	Screens.solid_layer(l)

	# A COLUMN, NOT TWO STACKS MEETING IN THE MIDDLE.
	#
	# This screen was laid out as hand-picked pixel offsets, some measured down from
	# the top and some up from the bottom. Nine bands, two origins, and no arithmetic
	# anywhere saying they could not meet: the tool palette sat at 400..565 and the
	# name field at 400..466, so the second row of tools was drawn straight through
	# the name box. Numbers that happen not to collide on the one screen they were
	# tuned against are not a layout.
	#
	# Everything is placed by a cursor running down from the top now. Bands cannot
	# overlap because each one starts where the last finished, and adding or resizing
	# one moves what follows instead of landing on it.
	#
	# AND THE CURSOR RUNS AT SHOW TIME, NOT AT BUILD TIME. Two of these bands are not
	# always there — DELETE has nothing to delete until the cube has been saved once,
	# and the share code is empty until VERIFY has proved one — and a band that is
	# switched off while its 68 or 70 units stay reserved is a hole. That hole was the
	# empty strip under the buttons, and it was coming out of the one band that wanted
	# it: the deck.
	_bands.clear()

	var bar := _band(l, "deckBar", 60)
	var dn := UiKit.rect(bar, "dn", Vector2(0, 0), Vector2(0.22, 1), Vector2.ZERO, Vector2.ZERO)
	UiKit.bracketed(dn, "dn", "<", funcref(self, "_deck_down"), 24)
	_deck_label = UiKit.label(bar, "deck", "DECK 1 / 5", 24, Palette.INK, UiKit.Anchor.MIDDLE_CENTER,
			Vector2(0.22, 0), Vector2(0.78, 1), Vector2.ZERO, Vector2.ZERO)
	var up := UiKit.rect(bar, "up", Vector2(0.78, 0), Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	UiKit.bracketed(up, "up", ">", funcref(self, "_deck_up"), 24)

	# THE COACH. An empty grid and ten unlabelled tools is not an editor, it is a
	# puzzle about an editor — so the one next thing standing between this cube and a
	# saveable cube is named, in a sentence, above the deck. Always one thing and
	# never a checklist: a checklist is read once, and a next step is read every time
	# it changes.
	_coach = _band(l, "coach", 54)
	var r := Palette.rust()
	UiKit.framed(_coach, Palette.PANEL_HI, Color(r.r, r.g, r.b, 0.6))
	_coach_n = UiKit.label(_coach, "n", "1", 17, Palette.rust(), UiKit.Anchor.MIDDLE_CENTER,
			Vector2(0, 0), Vector2(0, 1), Vector2(8, 0), Vector2(42, 0))
	# A SENTENCE, SO IT WRAPS. "TRACE IS WHAT YOU STAND ON. TAP CELLS TO LAY 8 MORE."
	# is fifty-one characters and this band has never been wide enough for it on a
	# phone — it ran off the right of the plate and the player was told to lay eight
	# mor.
	_coach_text = UiKit.label(_coach, "t", "", 19, Palette.INK, UiKit.Anchor.MIDDLE_LEFT,
			Vector2(0, 0), Vector2(1, 1), Vector2(46, 0), Vector2(-12, 0), true)

	# The deck is the one band that should take what is spare, because it is the
	# thing being edited — so its height is not a number here at all. _flow_editor
	# adds up what everything else needs and hands it the remainder, which is also
	# how it gets bigger when DELETE or the share code are not on screen.
	_slice_band = _slot(l, "slice", 0.0, 16.0)
	_slice = _slice_band.rt

	# A DECK IS SQUARE, BECAUSE A SLICE OF A CUBE IS SQUARE.
	#
	# The cells were anchored as fractions of the band, and the band is as wide as the
	# plate and as tall as whatever is left — 545 by 333. So a five-cube drew cells
	# 109 across and 67 high, and the editor showed you a grid that was not the shape
	# of the thing you were editing. Every cell you place is a cube; a squashed one is
	# a lie about where the trace you just laid is going to be.
	#
	# So the grid lives in a square centred in the band, and the band's spare width is
	# the price of the cells being right.
	_deck = UiKit.rect(_slice, "deck", Vector2(0.5, 0.5), Vector2(0.5, 0.5), Vector2.ZERO, Vector2.ZERO)

	_tool_row = _band(l, "tools", 118)

	var name_row := _band(l, "nameRow", 62)
	_name_field = UiKit.field(name_row, "name", "NAME THIS CUBE",
			Vector2(0, 0), Vector2(0.72, 1), Vector2.ZERO, Vector2(-8, 0))
	_name_field.line.max_length = 18
	# Godot's LineEdit signals the two ends of an edit separately; both mean the
	# same thing here, which is "the player has stopped typing".
	_name_field.line.connect("text_entered", self, "_name_done")
	_name_field.line.connect("focus_exited", self, "_name_done_nofocus")
	var size_slot := UiKit.rect(name_row, "sizeSlot", Vector2(0.72, 0), Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	_size_btn = UiKit.bracketed(size_slot, "size", "5", funcref(self, "_cycle_size"), 24)

	# The code used to be printed into the verdict line next to a clipboard call that
	# does not exist offline. It was on screen and there was no way on earth to get it
	# off — so it lives in a selectable field.
	_code_band = _slot(l, "codeRow", 58)
	_code_field = UiKit.field(_code_band.rt, "code", "", Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	# `editable`, not `readonly` — Godot's LineEdit spells the negative form and
	# has no property by the other name at all.
	_code_field.line.editable = false

	var msg_row := _band(l, "msg", 30)
	_ed_msg = UiKit.label(msg_row, "msg", "", 18, Palette.dim(), UiKit.Anchor.MIDDLE_CENTER,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)

	# BOTH BUILT PRIMARY, ONE LIT AT A TIME.
	#
	# SAVE was the lit plate from the moment the editor opened, while the coach two
	# rows above it was still saying "lay eight more traces" — so the one thing the
	# screen looked like it wanted was a button that could not work yet. The plate
	# follows the coach now: VERIFY until the cube is proved, SAVE once it is. See
	# UiKit.set_primary for why both have to be built this way.
	var two := _band(l, "two", 62)
	var v_slot := UiKit.rect(two, "v", Vector2(0, 0), Vector2(0.5, 1), Vector2.ZERO, Vector2(-6, 0))
	_verify_btn = UiKit.bracketed(v_slot, "verify", "VERIFY", funcref(self, "_do_verify"), 24, true)
	var s_slot := UiKit.rect(two, "s", Vector2(0.5, 0), Vector2.ONE, Vector2(6, 0), Vector2.ZERO)
	_save_btn = UiKit.bracketed(s_slot, "save", "SAVE", funcref(self, "_do_save"), 24, true)

	var three := _band(l, "three", 58)
	_third(three, 0, "PLAY", "_do_play")
	_third(three, 1, "BACK", "_on_back")
	_third(three, 2, "MENU", "_on_menu")

	_del_band = _slot(l, "del", 56)
	# DELETING IS THE ONE THING IN THE FORGE THAT CANNOT BE UNDONE.
	#
	# There is no bin and no undo: once the cube is gone the only copy left is
	# whatever share code somebody happened to keep. It went in one tap, on a button a
	# thumb passes on the way to BACK. So it takes two, it names what it is about to
	# destroy, and leaving the screen disarms it.
	_delete_btn = UiKit.bracketed(_del_band.rt, "delete", "DELETE THIS CUBE",
			funcref(self, "_do_delete"), 22)

	# every band was built at the top of the layer with a placeholder height; this is
	# where they get their positions
	_flow_editor()


func _slot(l: Control, nm: String, h: float, gap: float = 12.0) -> EdBand:
	var b := EdBand.new()
	b.rt = UiKit.rect(l, nm, Vector2(0, 1), Vector2(1, 1), Vector2(ED_SIDE, -h), Vector2(-ED_SIDE, 0))
	b.h = h
	b.gap = gap
	_bands.append(b)
	return b


func _band(l: Control, nm: String, h: float, gap: float = 12.0) -> UiRect:
	return _slot(l, nm, h, gap).rt


func _deck_down(_a = null) -> void:
	_ed.layer = int(max(0, _ed.layer - 1))
	_paint_slice()


func _deck_up(_a = null) -> void:
	_ed.layer = int(min(_ed.n - 1, _ed.layer + 1))
	_paint_slice()


func _name_done(_t = null) -> void:
	if _ed == null:
		return
	_ed.name = Forge.clean_name(_name_field.text())
	_name_field.set_text(_ed.name)
	_ed.save_draft()


func _name_done_nofocus() -> void:
	_name_done()


# PLACE THE COLUMN, AND GIVE WHAT IS LEFT TO THE DECK.
#
# Called whenever a band appears or disappears, which is: opening a cube, saving
# one for the first time, and proving one. Two bands come and go.
#
# DELETE has nothing to delete until the cube has been saved once. The SHARE CODE
# is an empty read-only field until VERIFY has proved a cube, and an empty field is
# not a thing, it is a gap with a border on it. Both used to be hidden while
# keeping their space, which is where the strip of nothing under the buttons came
# from — and it was coming straight out of the deck, the one band on this screen
# that is the thing being worked on.
#
# The message row is NOT reclaimed, deliberately. Its text changes on every tap,
# and a grid that resized itself under the thumb every time a status appeared would
# be far worse than thirty units. The other two move only on a deliberate press.
func _flow_editor() -> void:
	_code_band.on = _code_field.text() != ""
	_del_band.on = _ed != null and _ed.from != null

	var taken := 0.0
	for b in _bands:
		if b.on and b != _slice_band:
			taken += b.h + b.gap

	_slice_band.h = max(240.0,
			Layout.plate_height() - ED_TOP - ED_FOOT - taken - _slice_band.gap)

	var y := ED_TOP
	for b in _bands:
		if b.rt.visible != b.on:
			b.rt.visible = b.on
		if not b.on:
			continue
		b.rt.anchor_to(Vector2(0, 1), Vector2(1, 1),
				Vector2(ED_SIDE, -(y + b.h)), Vector2(-ED_SIDE, -y))
		y += b.h + b.gap

	# and the deck is the largest square the band will hold
	var side: float = min(Layout.plate_width() - ED_SIDE * 2.0, _slice_band.h)
	_deck.set_size_delta(Vector2(side, side))
	_deck.set_anchored_position(Vector2.ZERO)


func _third(parent: Control, idx: int, label: String, method: String) -> void:
	var slot := UiKit.rect(parent, label, Vector2(idx / 3.0, 0), Vector2((idx + 1) / 3.0, 1),
			Vector2(5, 0), Vector2(-5, 0))
	UiKit.bracketed(slot, label, label, funcref(self, method), 22)


static func open_editor(f: Forge) -> void:
	var me := i()
	me._armed_delete = null
	me._ed = f
	me._name_field.set_text(f.name)
	UiKit.set_label(me._size_btn, str(f.n))
	me._delete_btn.visible = f.from != null
	me._code_field.set_text("")
	me._status("", false)
	me._paint_tools()
	me._flow_editor()
	me._paint_slice()
	Screens.show("forgeEdit")


func _cycle_size(_a = null) -> void:
	var at: int = Forge.SIZES.find(_ed.n)
	var next: int = Forge.SIZES[(at + 1 + Forge.SIZES.size()) % Forge.SIZES.size()]
	_ed.resize(next)
	UiKit.set_label(_size_btn, str(next))
	_status("", false)
	_paint_slice()


# Re-read the cube and say the one next thing.
func _paint_coach() -> void:
	var a: Array = _ed.advice()
	var step: int = a[0]
	_coach_n.text = str(step)
	_coach_text.text = a[1]

	# The coach's steps: 4 is VERIFY, 5 is SAVE. Anything earlier is a cube that
	# cannot be verified yet, and neither plate is lit — the work is on the deck, not
	# on a button.
	UiKit.set_primary(_verify_btn, step == 4)
	UiKit.set_primary(_save_btn, step >= 5)


func _paint_tools() -> void:
	for c in _tool_row.get_children():
		_tool_row.remove_child(c)
		c.queue_free()

	var cols := 5
	var rows: int = int(ceil(Forge.TOOLS.size() / float(cols)))
	for idx in range(Forge.TOOLS.size()):
		var t: Array = Forge.TOOLS[idx]
		var k: int = t[0]
		var cx := idx % cols
		var cy := idx / cols
		var slot := UiKit.rect(_tool_row, "t" + str(k),
				Vector2(cx / float(cols), 1.0 - (cy + 1.0) / rows),
				Vector2((cx + 1.0) / cols, 1.0 - cy / float(rows)),
				Vector2(4, 4), Vector2(-4, -4))

		var act := UiAct.on(slot, self, "_pick_tool", k)
		var b := UiKit.bracketed(slot, "b", t[1], funcref(act, "press"), 14)

		if _ed != null and _ed.tool == k:
			b.label_node().add_color_override("font_color", Palette.rust())


func _pick_tool(k: int) -> void:
	if k == Forge.T_WIPE:
		_ed.clear_deck()
		_status("", false)
		_paint_slice()
		return
	_ed.tool = k
	_paint_tools()
	_paint_slice()


func _paint_slice() -> void:
	_paint_coach()
	for c in _deck.get_children():
		_deck.remove_child(c)
		c.queue_free()

	_deck_label.text = "DECK " + str(_ed.layer + 1) + " / " + str(_ed.n)

	var n: int = _ed.n
	for z in range(n):
		for x in range(n):
			# z runs up the screen, so deck row 0 is at the bottom — the same
			# orientation the level literals are written in, which is the only way
			# hand-authoring one is survivable.
			var slot := UiKit.rect(_deck, str(x) + "," + str(z),
					Vector2(x / float(n), z / float(n)),
					Vector2((x + 1) / float(n), (z + 1) / float(n)),
					Vector2(2, 2), Vector2(-2, -2))

			var c: int = _ed.vox[Level.vidx_n(n, x, _ed.layer, z)]
			var mark: int = _ed.mark_at(Vector3(x, _ed.layer, z))

			# A CELL CARRIES ITS MATERIAL AS A FILL AND ITS EDGE AS A RING, and the
			# ring is not decoration — it is the whole reason you can see what you just
			# did.
			#
			# These were flat fills with no edge at all, and the step from an empty cell
			# to a laid trace was VOID3 to TRACE_LO: a contrast ratio of 2.03, on a
			# phone, under a bezel. Tapping a cell looked like it had done nothing, which
			# is exactly what a player reports as "the editor is broken". With the ring
			# the same tap is an 8.96 step, because the ring is the bright colour and the
			# fill stays dark.
			#
			# The two plates were also the same colour as each other, so the one pair of
			# tools whose whole point is that they are OPPOSITES drew identical cells.
			var fill: Color = Palette.GRID if c == Level.LATTICE \
					else (Palette.TRACE_LO if Level.is_walk_type(c) else Palette.VOID2)
			var ring: Color = Palette.INERT
			if c == Level.LATTICE:
				ring = Palette.GRID_HI
			elif c == Level.PLATE_A:
				ring = Palette.ARC
			elif c == Level.PLATE_B:
				ring = Palette.rust()
			elif c == Level.TRACE:
				ring = Palette.TRACE

			# AND THE RING IS THE FACE.
			#
			# The cube is viewed along z, so the frontmost solid in each column is the
			# only one the cube actually shows — and the cell that hides another is not
			# on the deck above, it is the one further up THIS grid, in plain sight.
			# Nothing said so, which is why START IS BURIED was the commonest refusal in
			# the editor by a distance: more than half of four hundred plausible cubes,
			# on a rule the player had no way to see.
			#
			# A cell behind another keeps its material and loses its ring. The lit rings
			# are the faces, and a start goes on a face.
			if not _ed.on_face(x, z):
				ring = Palette.dim2()

			var act := UiAct.on(slot, self, "_tap_cell", Vector2(x, z))
			var btn := UiKit.bracketed(slot, "b", "", funcref(act, "press"), 1)
			var lbl := btn.label_node()
			if lbl != null:
				lbl.visible = false
			btn.plate_node().modulate = fill
			btn.edge_node().modulate = ring

			# A MARK IS A CHIP WITH A LETTER IN IT, and it is a different SHAPE for each
			# role. Colour still carries what it carries everywhere else in this game,
			# but it is no longer the only thing carrying it — which is the same reason
			# the board prints depth on request, and it is what makes the deck readable
			# without colour vision.
			if mark != 0:
				var chip := UiKit.rect(btn, "m", Vector2(0.23, 0.23), Vector2(0.77, 0.77),
						Vector2.ZERO, Vector2.ZERO)
				var col: Color = Palette.rust()
				if mark == Forge.M_EXIT:
					col = Palette.core()
				elif mark == Forge.M_NODE:
					col = Palette.NODE
				elif mark == Forge.M_LOCK:
					col = Palette.LOCK

				# round for the start, softened for the exit, square for a node and a lock
				# — the shapes the board already uses
				if mark == Forge.M_START:
					var disc := TextureRect.new()
					disc.name = "chip"
					disc.texture = Sprites.disc()
					disc.expand = true
					disc.stretch_mode = TextureRect.STRETCH_SCALE
					disc.modulate = col
					disc.mouse_filter = Control.MOUSE_FILTER_IGNORE
					disc.anchor_right = 1.0
					disc.anchor_bottom = 1.0
					chip.add_child(disc)
				elif mark == Forge.M_EXIT:
					var soft := NinePatchRect.new()
					soft.name = "chip"
					soft.texture = Sprites.frame(false)
					soft.patch_margin_left = Sprites.FRAME_SLICE
					soft.patch_margin_right = Sprites.FRAME_SLICE
					soft.patch_margin_top = Sprites.FRAME_SLICE
					soft.patch_margin_bottom = Sprites.FRAME_SLICE
					soft.modulate = col
					soft.mouse_filter = Control.MOUSE_FILTER_IGNORE
					soft.anchor_right = 1.0
					soft.anchor_bottom = 1.0
					chip.add_child(soft)
				else:
					var square := ColorRect.new()
					square.name = "chip"
					square.color = col
					square.mouse_filter = Control.MOUSE_FILTER_IGNORE
					square.anchor_right = 1.0
					square.anchor_bottom = 1.0
					chip.add_child(square)

				UiKit.label(chip, "l", char(mark), 18, Palette.VOID, UiKit.Anchor.MIDDLE_CENTER,
						Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)


func _tap_cell(at: Vector2) -> void:
	_ed.apply(int(at.x), int(at.y))
	# laying a cell invalidates the proof, so the code goes — and the deck grows back
	# into the row it was in
	var had_code: bool = _code_field.text() != ""
	_code_field.set_text("")
	_status("", false)
	if had_code:
		_flow_editor()
	_paint_slice()


func _status(msg: String, bad: bool) -> void:
	_ed_msg.text = msg
	_ed_msg.add_color_override("font_color",
			Palette.dim() if msg == "" else (Palette.fault() if bad else Palette.NODE))


func _do_verify(_a = null) -> void:
	_ed.name = Forge.clean_name(_name_field.text())
	var v := _ed.verify()
	_status(v.message, not v.ok)
	_code_field.set_text(ShareCode.encode(v.level) if v.ok else "")
	_flow_editor()
	_paint_slice()


func _do_save(_a = null) -> void:
	_ed.name = Forge.clean_name(_name_field.text())
	var r := _ed.save()
	_status(r[1], not r[0])
	if not r[0]:
		return
	_name_field.set_text(_ed.name)
	_delete_btn.visible = true
	var rec = Store.made(_ed.from)
	if rec != null:
		_code_field.set_text(rec.code)
	_flow_editor()
	_paint_slice()


func _do_play(_a = null) -> void:
	_ed.name = Forge.clean_name(_name_field.text())
	var r := _ed.save()
	if not r[0]:
		_status(r[1], true)
		return
	Screens.show(null)
	_dir.play(Daily.SPEC_LEVEL, Session.LoadKind.MADE, _ed.from)


func _do_delete(_a = null) -> void:
	if _ed == null or _ed.from == null:
		return
	if _armed_delete != _ed.from:
		_armed_delete = _ed.from
		var rec = Store.made(_ed.from)
		var nm: String = rec.name if (rec != null and rec.name != "") else "UNTITLED"
		_status("TAP AGAIN TO DELETE " + nm + ". THIS CANNOT BE UNDONE.", true)
		return
	_armed_delete = null
	_ed.delete()
	_ed = null
	open_shelf()
	_shelf_msg.text = "DELETED"
	_shelf_msg.add_color_override("font_color", Palette.dim())
