using UnityEngine;
using UnityEngine.UI;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// THE FORGE, on screen: a shelf of what the player has built, and the editor
    /// that builds it.
    ///
    /// The editor works ONE DECK AT A TIME and that is the whole interaction
    /// design. Direct manipulation of a projected solid is a lovely demo and a
    /// miserable tool — the thing you want to click is behind the thing you can
    /// see, and on a phone your thumb covers both. A deck is an n-by-n grid you
    /// can hit accurately, and the deck stepper is the only spatial reasoning the
    /// editor asks for.
    /// </summary>
    public static class ForgeScreens
    {
        static GameDirector _dir;
        static Forge _ed;

        // shelf
        static RectTransform _shelfGrid;
        static Text _shelfCount, _shelfMsg;
        static InputField _importField;

        // editor
        static RectTransform _slice, _deck, _toolRow, _coach;
        static Text _deckLabel, _edMsg, _codeText, _coachN, _coachText;
        static InputField _nameField;
        static Button _sizeBtn, _deleteBtn, _verifyBtn, _saveBtn;

        /// <summary>
        /// One band of the editor column: what it is, how tall, and how much air
        /// after it. Kept in a list rather than baked into a cursor at build time
        /// because two of them come and go — see FlowEditor.
        /// </summary>
        class EdBand
        {
            public RectTransform rt;
            public float h, gap;
            public bool on = true;
        }

        static readonly System.Collections.Generic.List<EdBand> _bands = new System.Collections.Generic.List<EdBand>();
        static EdBand _sliceBand, _codeBand, _delBand;

        const float EdTop = 84f, EdFoot = 28f, EdSide = 40f;

        /// <summary>Which cube DELETE is currently armed for. Cleared by leaving the screen.</summary>
        static string _armedDelete;

        public static void Build(GameDirector dir)
        {
            _dir = dir;
            BuildShelf();
            BuildEditor();
        }

        // ---- the shelf -------------------------------------------------------

        static void BuildShelf()
        {
            RectTransform L = Screens.Layer("forge");
            Screens.SolidLayer(L);

            _shelfCount = UiKit.Label(L, "count", "FORGE", 34, Palette.Ink, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -90));

            _shelfGrid = UiKit.Rect(L, "grid", new Vector2(0, 0), new Vector2(1, 1),
                                    new Vector2(40, 330), new Vector2(-40, -170));

            // THE CODE IS THE CARGO. A generated cube has a seed because a seed is
            // what made it; an authored cube was not made by anything, so the code
            // has to carry the voxels themselves.
            RectTransform row = UiKit.Rect(L, "importRow", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(40, 250), new Vector2(-40, 316));
            _importField = UiKit.Field(row, "code", "PASTE A CUBE CODE",
                                       new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, new Vector2(-8, 0));
            RectTransform loadSlot = UiKit.Rect(row, "loadSlot", new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Bracketed(loadSlot, "load", "LOAD", () =>
            {
                bool ok = Forge.Import(_importField.text, out string msg);
                _shelfMsg.text = msg;
                _shelfMsg.color = ok ? Palette.Node : Palette.Fault;
                if (ok) { _importField.text = ""; PaintShelf(); }
            }, 24);

            _shelfMsg = UiKit.Label(L, "msg", "", 18, Palette.Dim, TextAnchor.UpperCenter,
                                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 210), new Vector2(-40, 246));

            // SIDE BY SIDE, NOT STACKED. Two full-width bars in a hundred pixels
            // gives two controls forty pixels tall and four hundred wide, which is a
            // shape no thumb wants and no eye reads as a button. Halved, they come
            // out close to square — and these two are alternatives to each other
            // rather than steps in a sequence, which is what a row says and a stack
            // does not.
            RectTransform bot = UiKit.Rect(L, "bot", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(50, 96), new Vector2(-50, 178));
            RectTransform mk = UiKit.Rect(bot, "mk", new Vector2(0, 0), new Vector2(0.5f, 1),
                                          Vector2.zero, new Vector2(-7, 0));
            // RESUME, NOT NEW. The editor mirrors itself into a draft after every
            // change, so the cube somebody was halfway through when Android
            // reclaimed the app is still there. A button that says NEW CUBE and
            // silently bins twenty minutes of work is the worst kind.
            UiKit.Bracketed(mk, "NEW CUBE", "NEW CUBE", () => OpenEditor(Forge.Resume()), 24, true);
            RectTransform bk = UiKit.Rect(bot, "bk", new Vector2(0.5f, 0), Vector2.one,
                                          new Vector2(7, 0), Vector2.zero);
            UiKit.Bracketed(bk, "BACK", "BACK", Screens.Back, 24);
        }

        public static void OpenShelf()
        {
            _armedDelete = null;
            PaintShelf();
            Screens.Show("forge");
        }

        static void PaintShelf()
        {
            for (int i = _shelfGrid.childCount - 1; i >= 0; i--) Object.Destroy(_shelfGrid.GetChild(i).gameObject);

            var made = Store.Data.made;
            _shelfCount.text = made.Count == 0 ? "FORGE" : "FORGE · " + made.Count;
            if (made.Count == 0)
            {
                UiKit.Label(_shelfGrid, "empty",
                            "NOTHING BUILT YET.\n\nA cube is edited one deck at a time.\nThe solver proves it before you can save it.",
                            20, Palette.Dim, TextAnchor.MiddleCenter,
                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, wrap: true);
                return;
            }

            const int cols = 2;
            int rows = Mathf.Max(4, Mathf.CeilToInt(made.Count / (float)cols));
            for (int i = 0; i < made.Count; i++)
            {
                MadeCube m = made[i];
                int cx = i % cols, cy = i / cols;
                RectTransform slot = UiKit.Rect(_shelfGrid, "m" + i,
                    new Vector2(cx / (float)cols, 1f - (cy + 1f) / rows),
                    new Vector2((cx + 1f) / cols, 1f - cy / (float)rows),
                    new Vector2(6, 6), new Vector2(-6, -6));

                string face = m.name + "\n" + m.n + "³ · PAR " + m.par + (m.best >= 0 ? " · BEST " + m.best : "");
                MadeCube captured = m;
                UiKit.Bracketed(slot, "b", face, () => ShelfMenu(captured), 18);
            }
        }

        static void ShelfMenu(MadeCube m)
        {
            // Two things a built cube can do, and no submenu for two things.
            _shelfMsg.text = m.name + " — " + m.id;
            _shelfMsg.color = Palette.Dim;
            OpenEditor(Forge.From(m));
        }

        // ---- the editor ------------------------------------------------------

        static void BuildEditor()
        {
            RectTransform L = Screens.Layer("forgeEdit");
            Screens.SolidLayer(L);

            // A COLUMN, NOT TWO STACKS MEETING IN THE MIDDLE.
            //
            // This screen was laid out as hand-picked pixel offsets, some measured
            // down from the top and some up from the bottom. Nine bands, two
            // origins, and no arithmetic anywhere saying they could not meet: the
            // tool palette sat at 400..565 and the name field at 400..466, so the
            // second row of tools was drawn straight through the name box. Numbers
            // that happen not to collide on the one screen they were tuned against
            // are not a layout.
            //
            // Everything is placed by a cursor running down from the top now. Bands
            // cannot overlap because each one starts where the last finished, and
            // adding or resizing one moves what follows instead of landing on it.
            //
            // AND THE CURSOR RUNS AT SHOW TIME, NOT AT BUILD TIME. Two of these
            // bands are not always there — DELETE has nothing to delete until the
            // cube has been saved once, and the share code is empty until VERIFY
            // has proved one — and a band that is switched off while its 68 or 70
            // units stay reserved is a hole. That hole was the empty strip under
            // the buttons, and it was coming out of the one band that wanted it:
            // the deck.
            _bands.Clear();

            EdBand Slot(string name, float h, float gap = 12f)
            {
                var b = new EdBand
                {
                    rt = UiKit.Rect(L, name, new Vector2(0, 1), new Vector2(1, 1),
                                    new Vector2(EdSide, -h), new Vector2(-EdSide, 0)),
                    h = h,
                    gap = gap,
                };
                _bands.Add(b);
                return b;
            }

            RectTransform Band(string name, float h, float gap = 12f) => Slot(name, h, gap).rt;

            RectTransform bar = Band("deckBar", 60);
            RectTransform dn = UiKit.Rect(bar, "dn", new Vector2(0, 0), new Vector2(0.22f, 1), Vector2.zero, Vector2.zero);
            UiKit.Bracketed(dn, "dn", "<", () => { _ed.layer = Mathf.Max(0, _ed.layer - 1); PaintSlice(); }, 24);
            _deckLabel = UiKit.Label(bar, "deck", "DECK 1 / 5", 24, Palette.Ink, TextAnchor.MiddleCenter,
                                     new Vector2(0.22f, 0), new Vector2(0.78f, 1), Vector2.zero, Vector2.zero);
            RectTransform up = UiKit.Rect(bar, "up", new Vector2(0.78f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Bracketed(up, "up", ">", () => { _ed.layer = Mathf.Min(_ed.n - 1, _ed.layer + 1); PaintSlice(); }, 24);

            // THE COACH. An empty grid and ten unlabelled tools is not an editor, it
            // is a puzzle about an editor — so the one next thing standing between
            // this cube and a saveable cube is named, in a sentence, above the deck.
            // Always one thing and never a checklist: a checklist is read once, and a
            // next step is read every time it changes.
            _coach = Band("coach", 54);
            UiKit.Framed(_coach, Palette.PanelHi, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.6f));
            _coachN = UiKit.Label(_coach, "n", "1", 17, Palette.Rust, TextAnchor.MiddleCenter,
                                  new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 0), new Vector2(42, 0));
            // A SENTENCE, SO IT WRAPS. "TRACE IS WHAT YOU STAND ON. TAP CELLS TO
            // LAY 8 MORE." is fifty-one characters and this band has never been
            // wide enough for it on a phone — it ran off the right of the plate
            // and the player was told to lay eight mor.
            _coachText = UiKit.Label(_coach, "t", "", 19, Palette.Ink, TextAnchor.MiddleLeft,
                                     new Vector2(0, 0), new Vector2(1, 1), new Vector2(46, 0), new Vector2(-12, 0),
                                     wrap: true);

            // The deck is the one band that should take what is spare, because it
            // is the thing being edited — so its height is not a number here at
            // all. FlowEditor adds up what everything else needs and hands it the
            // remainder, which is also how it gets bigger when DELETE or the share
            // code are not on screen.
            _sliceBand = Slot("slice", 0f, 16f);
            _slice = _sliceBand.rt;

            // A DECK IS SQUARE, BECAUSE A SLICE OF A CUBE IS SQUARE.
            //
            // The cells were anchored as fractions of the band, and the band is as
            // wide as the plate and as tall as whatever is left — 545 by 333. So a
            // five-cube drew cells 109 across and 67 high, and the editor showed
            // you a grid that was not the shape of the thing you were editing.
            // Every cell you place is a cube; a squashed one is a lie about where
            // the trace you just laid is going to be.
            //
            // So the grid lives in a square centred in the band, and the band's
            // spare width is the price of the cells being right.
            _deck = UiKit.Rect(_slice, "deck", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               Vector2.zero, Vector2.zero);

            _toolRow = Band("tools", 118);

            RectTransform nameRow = Band("nameRow", 62);
            _nameField = UiKit.Field(nameRow, "name", "NAME THIS CUBE",
                                     new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, new Vector2(-8, 0));
            _nameField.characterLimit = 18;
            _nameField.onEndEdit.AddListener(v =>
            {
                if (_ed == null) return;
                _ed.name = Forge.CleanName(v);
                _nameField.text = _ed.name;
                _ed.SaveDraft();
            });
            RectTransform sizeSlot = UiKit.Rect(nameRow, "sizeSlot", new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            _sizeBtn = UiKit.Bracketed(sizeSlot, "size", "5", CycleSize, 24);

            // The code used to be printed into the verdict line next to a clipboard
            // call that does not exist offline. It was on screen and there was no
            // way on earth to get it off — so it lives in a selectable field.
            _codeBand = Slot("codeRow", 58);
            RectTransform codeRow = _codeBand.rt;
            InputField codeField = UiKit.Field(codeRow, "code", "", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            codeField.readOnly = true;
            _codeText = codeField.textComponent;

            RectTransform msgRow = Band("msg", 30);
            _edMsg = UiKit.Label(msgRow, "msg", "", 18, Palette.Dim, TextAnchor.MiddleCenter,
                                 Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // BOTH BUILT PRIMARY, ONE LIT AT A TIME.
            //
            // SAVE was the lit plate from the moment the editor opened, while the
            // coach two rows above it was still saying "lay eight more traces" —
            // so the one thing the screen looked like it wanted was a button that
            // could not work yet. The plate follows the coach now: VERIFY until the
            // cube is proved, SAVE once it is. See UiKit.SetPrimary for why both
            // have to be built this way.
            RectTransform two = Band("two", 62);
            RectTransform vSlot = UiKit.Rect(two, "v", new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-6, 0));
            _verifyBtn = UiKit.Bracketed(vSlot, "verify", "VERIFY", DoVerify, 24, true);
            RectTransform sSlot = UiKit.Rect(two, "s", new Vector2(0.5f, 0), Vector2.one, new Vector2(6, 0), Vector2.zero);
            _saveBtn = UiKit.Bracketed(sSlot, "save", "SAVE", DoSave, 24, true);

            RectTransform three = Band("three", 58);
            Third(three, 0, "PLAY", DoPlay);
            Third(three, 1, "BACK", Screens.Back);
            Third(three, 2, "MENU", Screens.ShowTitle);

            _delBand = Slot("del", 56);
            RectTransform del = _delBand.rt;
            // DELETING IS THE ONE THING IN THE FORGE THAT CANNOT BE UNDONE.
            //
            // There is no bin and no undo: once the cube is gone the only copy
            // left is whatever share code somebody happened to keep. It went in
            // one tap, on a button a thumb passes on the way to BACK. So it takes
            // two, it names what it is about to destroy, and leaving the screen
            // disarms it.
            _deleteBtn = UiKit.Bracketed(del, "delete", "DELETE THIS CUBE", () =>
            {
                if (_ed == null || _ed.from == null) return;
                if (_armedDelete != _ed.from)
                {
                    _armedDelete = _ed.from;
                    MadeCube rec = Store.Made(_ed.from);
                    Status("TAP AGAIN TO DELETE " + (rec != null && !string.IsNullOrEmpty(rec.name)
                                                     ? rec.name : "UNTITLED") + ". THIS CANNOT BE UNDONE.", true);
                    return;
                }
                _armedDelete = null;
                _ed.Delete();
                _ed = null;
                OpenShelf();
                _shelfMsg.text = "DELETED";
                _shelfMsg.color = Palette.Dim;
            }, 22);

            // every band was built at the top of the layer with a placeholder
            // height; this is where they get their positions
            FlowEditor();
        }

        /// <summary>
        /// PLACE THE COLUMN, AND GIVE WHAT IS LEFT TO THE DECK.
        ///
        /// Called whenever a band appears or disappears, which is: opening a cube,
        /// saving one for the first time, and proving one. Two bands come and go.
        ///
        /// DELETE has nothing to delete until the cube has been saved once. The
        /// SHARE CODE is an empty read-only field until VERIFY has proved a cube,
        /// and an empty field is not a thing, it is a gap with a border on it.
        /// Both used to be hidden while keeping their space, which is where the
        /// strip of nothing under the buttons came from — and it was coming
        /// straight out of the deck, the one band on this screen that is the thing
        /// being worked on.
        ///
        /// The message row is NOT reclaimed, deliberately. Its text changes on
        /// every tap, and a grid that resized itself under the thumb every time a
        /// status appeared would be far worse than thirty units. The other two
        /// move only on a deliberate press.
        /// </summary>
        static void FlowEditor()
        {
            _codeBand.on = !string.IsNullOrEmpty(_codeText.text);
            _delBand.on = _ed != null && _ed.from != null;

            float taken = 0f;
            foreach (EdBand b in _bands)
                if (b.on && b != _sliceBand) taken += b.h + b.gap;

            _sliceBand.h = Mathf.Max(240f,
                Layout.PlateHeight - EdTop - EdFoot - taken - _sliceBand.gap);

            float y = EdTop;
            foreach (EdBand b in _bands)
            {
                if (b.rt.gameObject.activeSelf != b.on) b.rt.gameObject.SetActive(b.on);
                if (!b.on) continue;
                b.rt.offsetMax = new Vector2(-EdSide, -y);
                b.rt.offsetMin = new Vector2(EdSide, -(y + b.h));
                y += b.h + b.gap;
            }

            // and the deck is the largest square the band will hold
            float side = Mathf.Min(Layout.PlateWidth - EdSide * 2f, _sliceBand.h);
            _deck.sizeDelta = new Vector2(side, side);
            _deck.anchoredPosition = Vector2.zero;
        }

        static void Third(RectTransform parent, int i, string label, System.Action act)
        {
            RectTransform slot = UiKit.Rect(parent, label, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                                            new Vector2(5, 0), new Vector2(-5, 0));
            UiKit.Bracketed(slot, label, label, act, 22);
        }

        public static void OpenEditor(Forge f)
        {
            _armedDelete = null;
            _ed = f;
            _nameField.text = f.name ?? "";
            UiKit.SetLabel(_sizeBtn, f.n.ToString());
            _deleteBtn.gameObject.SetActive(f.from != null);
            _codeText.text = "";
            Status("", false);
            PaintTools();
            FlowEditor();
            PaintSlice();
            Screens.Show("forgeEdit");
        }

        static void CycleSize()
        {
            int at = System.Array.IndexOf(Forge.Sizes, _ed.n);
            int next = Forge.Sizes[(at + 1 + Forge.Sizes.Length) % Forge.Sizes.Length];
            _ed.Resize(next);
            UiKit.SetLabel(_sizeBtn, next.ToString());
            Status("", false);
            PaintSlice();
        }

        /// <summary>Re-read the cube and say the one next thing.</summary>
        static void PaintCoach()
        {
            var (step, say) = _ed.Advice();
            _coachN.text = step.ToString();
            _coachText.text = say;

            // The coach's steps: 4 is VERIFY, 5 is SAVE. Anything earlier is a
            // cube that cannot be verified yet, and neither plate is lit — the
            // work is on the deck, not on a button.
            UiKit.SetPrimary(_verifyBtn, step == 4);
            UiKit.SetPrimary(_saveBtn, step >= 5);
        }

        static void PaintTools()
        {
            for (int i = _toolRow.childCount - 1; i >= 0; i--) Object.Destroy(_toolRow.GetChild(i).gameObject);

            int cols = 5;
            int rows = Mathf.CeilToInt(Forge.Tools.Length / (float)cols);
            for (int i = 0; i < Forge.Tools.Length; i++)
            {
                Forge.Tool t = Forge.Tools[i];
                int cx = i % cols, cy = i / cols;
                RectTransform slot = UiKit.Rect(_toolRow, "t" + t.k,
                    new Vector2(cx / (float)cols, 1f - (cy + 1f) / rows),
                    new Vector2((cx + 1f) / cols, 1f - cy / (float)rows),
                    new Vector2(4, 4), new Vector2(-4, -4));

                char k = t.k;
                Button b = UiKit.Bracketed(slot, "b", t.n, () =>
                {
                    if (k == 'C') { _ed.ClearDeck(); Status("", false); PaintSlice(); return; }
                    _ed.tool = k;
                    PaintTools();
                    PaintSlice();
                }, 14);

                if (_ed != null && _ed.tool == k)
                    b.GetComponentInChildren<Text>().color = Palette.Rust;
            }
        }

        static void PaintSlice()
        {
            PaintCoach();
            for (int i = _deck.childCount - 1; i >= 0; i--) Object.Destroy(_deck.GetChild(i).gameObject);

            _deckLabel.text = "DECK " + (_ed.layer + 1) + " / " + _ed.n;

            int n = _ed.n;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    // z runs up the screen, so deck row 0 is at the bottom — the same
                    // orientation the level literals are written in, which is the only
                    // way hand-authoring one is survivable.
                    RectTransform slot = UiKit.Rect(_deck, x + "," + z,
                        new Vector2(x / (float)n, z / (float)n),
                        new Vector2((x + 1) / (float)n, (z + 1) / (float)n),
                        new Vector2(2, 2), new Vector2(-2, -2));

                    char c = _ed.vox[Level.Vidx(n, x, _ed.layer, z)];
                    char mark = _ed.MarkAt(new Int3(x, _ed.layer, z));

                    // A CELL CARRIES ITS MATERIAL AS A FILL AND ITS EDGE AS A RING,
                    // and the ring is not decoration — it is the whole reason you
                    // can see what you just did.
                    //
                    // These were flat fills with no edge at all, and the step from
                    // an empty cell to a laid trace was Void3 to TraceLo: a
                    // contrast ratio of 2.03, on a phone, under a bezel. Tapping a
                    // cell looked like it had done nothing, which is exactly what
                    // a player reports as "the editor is broken". With the ring the
                    // same tap is a 8.96 step, because the ring is the bright
                    // colour and the fill stays dark.
                    //
                    // The two plates were also the same colour as each other, so
                    // the one pair of tools whose whole point is that they are
                    // OPPOSITES drew identical cells.
                    Color fill = c == '#' ? Palette.Grid
                               : Level.IsWalkType(c) ? Palette.TraceLo
                               : Palette.Void2;
                    Color ring = c == '#' ? Palette.GridHi
                               : c == 'A' ? Palette.Arc
                               : c == 'B' ? Palette.Rust
                               : c == '+' ? Palette.Trace
                               : Palette.Inert;

                    // AND THE RING IS THE FACE.
                    //
                    // The cube is viewed along z, so the frontmost solid in each
                    // column is the only one the cube actually shows — and the
                    // cell that hides another is not on the deck above, it is the
                    // one further up THIS grid, in plain sight. Nothing said so,
                    // which is why START IS BURIED was the commonest refusal in
                    // the editor by a distance: more than half of four hundred
                    // plausible cubes, on a rule the player had no way to see.
                    //
                    // A cell behind another keeps its material and loses its ring.
                    // The lit rings are the faces, and a start goes on a face.
                    if (!_ed.OnFace(x, z)) ring = Palette.Dim2;

                    Image img = UiKit.Framed(slot, fill, ring);
                    var btn = slot.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;

                    // A MARK IS A CHIP WITH A LETTER IN IT, and it is a different
                    // SHAPE for each role. Colour still carries what it carries
                    // everywhere else in this game, but it is no longer the only
                    // thing carrying it — which is the same reason the board prints
                    // depth on request, and it is what makes the deck readable
                    // without colour vision.
                    if (mark != '\0')
                    {
                        RectTransform chip = UiKit.Rect(slot, "m", new Vector2(0.23f, 0.23f),
                                                        new Vector2(0.77f, 0.77f), Vector2.zero, Vector2.zero);
                        var ci = chip.gameObject.AddComponent<Image>();
                        ci.raycastTarget = false;
                        ci.color = mark == 'S' ? Palette.Rust : mark == 'E' ? Palette.Core
                                 : mark == 'N' ? Palette.Node : Palette.Lock;
                        // round for the start, softened for the exit, square for a
                        // node and a lock — the shapes the board already uses
                        if (mark == 'S') ci.sprite = UiKit.Disc;
                        else if (mark == 'E') { ci.sprite = UiKit.RoundFill; ci.type = Image.Type.Sliced; }

                        UiKit.Label(chip, "l", mark.ToString(), 18, Palette.Void, TextAnchor.MiddleCenter,
                                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    }

                    int cx2 = x, cz2 = z;
                    btn.onClick.AddListener(() =>
                    {
                        _ed.Apply(cx2, cz2);
                        // laying a cell invalidates the proof, so the code goes —
                        // and the deck grows back into the row it was in
                        bool hadCode = !string.IsNullOrEmpty(_codeText.text);
                        _codeText.text = "";
                        Status("", false);
                        if (hadCode) FlowEditor();
                        PaintSlice();
                    });
                }
        }

        static void Status(string msg, bool bad)
        {
            _edMsg.text = msg ?? "";
            _edMsg.color = string.IsNullOrEmpty(msg) ? Palette.Dim : bad ? Palette.Fault : Palette.Node;
        }

        static void DoVerify()
        {
            _ed.name = Forge.CleanName(_nameField.text);
            Forge.VerifyResult v = _ed.Verify();
            Status(v.message, !v.ok);
            _codeText.text = v.ok ? ShareCode.Encode(v.level) : "";
            FlowEditor();
            PaintSlice();
        }

        static void DoSave()
        {
            _ed.name = Forge.CleanName(_nameField.text);
            bool ok = _ed.Save(out string msg);
            Status(msg, !ok);
            if (!ok) return;
            _nameField.text = _ed.name;
            _deleteBtn.gameObject.SetActive(true);
            MadeCube rec = Store.Made(_ed.from);
            if (rec != null) _codeText.text = rec.code;
            FlowEditor();
            PaintSlice();
        }

        static void DoPlay()
        {
            _ed.name = Forge.CleanName(_nameField.text);
            if (!_ed.Save(out string msg)) { Status(msg, true); return; }
            Screens.Show(null);
            _dir.Play(Daily.SpecLevel, LoadKind.Made, _ed.from);
        }
    }
}
