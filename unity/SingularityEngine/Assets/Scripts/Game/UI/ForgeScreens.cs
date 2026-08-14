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
    ///
    /// Both screens are `.layer` columns like every other, which is a change: this
    /// one used to place nine bands with a hand-run cursor down from the top of a
    /// 1280-unit canvas, and before that with offsets measured from two different
    /// origins that overlapped on any screen but the one they were tuned against.
    /// A column cannot overlap itself.
    /// </summary>
    public static class ForgeScreens
    {
        static GameDirector _dir;
        static Forge _ed;

        // shelf
        static RectTransform _shelfGrid, _madeRail;
        static Text _shelfCount, _shelfMsg;
        static InputField _importField;

        // editor
        static RectTransform _slice, _toolRow, _coach, _codeRow, _deleteRow;
        static Text _deckLabel, _edMsg, _coachN, _coachText;
        static InputField _nameField, _codeField;
        static Button _sizeBtn;

        public static void Build(GameDirector dir)
        {
            _dir = dir;
            BuildShelf();
            BuildEditor();
        }

        // ---- the shelf -------------------------------------------------------
        //
        // Reached from the vault shelf as well as the title, because the title is a
        // composition with a cube in the middle of it and a fifth button would be
        // the thing that finally covered it.

        static void BuildShelf()
        {
            RectTransform L = Screens.Layer("forge");
            Flow col = UiKit.Column(L, false);

            RectTransform bar = UiKit.Slot(col, "vaultBar", Css.HCtl, Css.GridMax, 16f);
            RectTransform id = UiKit.Fill(bar, "vaultId");
            _madeRail = UiKit.Rect(id, "rail", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   Vector2.zero, Vector2.zero);
            _madeRail.sizeDelta = new Vector2(Mathf.Min(180f, Css.Vw(52f)), 3f);
            _shelfCount = UiKit.Label(id, "count", "FORGE", Css.TTiny, Palette.Dim,
                                      TextAnchor.UpperCenter, 0.18f,
                                      new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -8f));

            _shelfGrid = UiKit.Slot(col, "madeGrid", 0f, Css.GridMax, 18f);

            // THE CODE IS THE CARGO. A generated cube has a seed because a seed is
            // what made it; an authored cube was not made by anything, so the code
            // has to carry the voxels themselves.
            RectTransform row = UiKit.Slot(col, "edRow", Css.HCtl, Css.ManualMax, 10f);
            RectTransform fieldSlot = UiKit.Rect(row, "impCode", Vector2.zero, new Vector2(1f, 1f),
                                                 Vector2.zero, new Vector2(-Css.HCtl * 1.9f - 8f, 0f));
            _importField = UiKit.Field(fieldSlot, "PASTE A CUBE CODE");
            RectTransform loadSlot = UiKit.Rect(row, "load", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                                new Vector2(-Css.HCtl * 1.9f, 0f), Vector2.zero);
            UiKit.PillBtn(loadSlot, "[ LOAD ]", () =>
            {
                bool ok = Forge.Import(_importField.text, out string msg);
                _shelfMsg.text = msg.ToUpperInvariant();
                _shelfMsg.color = ok ? Palette.Arc : Palette.Fault;
                if (ok) { _importField.text = ""; PaintShelf(); }
            });

            RectTransform msg = UiKit.Slot(col, "madeMsg", Css.TMicro * 2.6f, Css.ManualMax, 10f);
            _shelfMsg = UiKit.Type(msg, "m", "", Css.TMicro, Palette.Dim, TextAnchor.UpperCenter, 0.09f);

            // SIDE BY SIDE, NOT STACKED. These two are alternatives to each other
            // rather than steps in a sequence, which is what a row says and a stack
            // does not.
            RectTransform two = UiKit.Slot(col, "btn2", Css.HBtn + 6f, Css.BtnMax, 11f);
            RectTransform mk = UiKit.Rect(two, "new", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                                          Vector2.zero, new Vector2(-5f, 0f));
            UiKit.Btn(mk, "NEW CUBE", () => OpenEditor(Forge.New()), UiKit.Kind.Btn, Css.TSm, 0.12f);
            RectTransform bk = UiKit.Rect(two, "back", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                                          new Vector2(5f, 0f), Vector2.zero);
            UiKit.Btn(bk, "BACK", Screens.Back, UiKit.Kind.Ghost, Css.TSm, 0.12f);
        }

        public static void OpenShelf()
        {
            PaintShelf();
            Screens.Show("forge");
        }

        static void PaintShelf()
        {
            for (int i = _shelfGrid.childCount - 1; i >= 0; i--) Object.Destroy(_shelfGrid.GetChild(i).gameObject);

            var made = Store.Data.made;
            _shelfCount.text = made.Count == 0 ? "FORGE" : "FORGE · " + made.Count;
            UiKit.Rail(_madeRail, made.Count == 0 ? Palette.Dim2 : Palette.Arc, 1f);

            if (made.Count == 0)
            {
                _shelfGrid.sizeDelta = new Vector2(_shelfGrid.sizeDelta.x, 120f);
                Text t = UiKit.Para(_shelfGrid, "empty",
                    "NOTHING BUILT YET.\n\nA cube is edited one deck at a time. " +
                    "The solver proves it before you can save it.",
                    Css.TMicro, Palette.Dim, TextAnchor.MiddleCenter, 0.10f);
                return;
            }

            // the same square grid the vault shelf uses, because a cube is a cube
            const float Gap = 11f, Min = 96f;
            float avail = Mathf.Min(Css.GridMax, Css.VpW - Css.PadX * 2f);
            int cols = Mathf.Max(1, Mathf.FloorToInt((avail + Gap) / (Min + Gap)));
            float cell = (avail - Gap * (cols - 1)) / cols;
            int rows = Mathf.CeilToInt(made.Count / (float)cols);
            _shelfGrid.sizeDelta = new Vector2(_shelfGrid.sizeDelta.x, rows * cell + (rows - 1) * Gap);

            for (int i = 0; i < made.Count; i++)
            {
                MadeCube m = made[i];
                int cx = i % cols, cy = i / cols;
                RectTransform slot = UiKit.Rect(_shelfGrid, "m" + i,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                slot.pivot = new Vector2(0f, 1f);
                slot.sizeDelta = new Vector2(cell, cell);
                slot.anchoredPosition = new Vector2(cx * (cell + Gap), -cy * (cell + Gap));

                var plate = slot.gameObject.AddComponent<Image>();
                plate.sprite = UiKit.Frame(Css.RCard, 0f);
                plate.type = Image.Type.Sliced;
                plate.color = Palette.Panel;
                Image edge = UiKit.Edge(slot, m.best >= 0
                    ? new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, 0.55f)
                    : Css.Edge2, Css.RCard, Css.Hair);

                UiKit.Label(slot, "idx", m.n + "³", Css.T2Xl, m.best >= 0 ? Palette.Arc : Palette.Dim,
                            TextAnchor.LowerCenter, 0f,
                            new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -10f));
                UiKit.Label(slot, "nm", m.name, Css.TMicro, Palette.Dim2, TextAnchor.UpperCenter, 0.13f,
                            new Vector2(0f, 0.2f), new Vector2(1f, 0.45f), new Vector2(6f, 0f), new Vector2(-6f, 5f));
                UiKit.Label(slot, "par", "PAR " + m.par, Css.TMicro, Palette.Dim2,
                            TextAnchor.LowerCenter, 0.13f,
                            new Vector2(0f, 0f), new Vector2(1f, 0.2f), Vector2.zero, new Vector2(0f, 8f));

                MadeCube captured = m;
                var btn = slot.gameObject.AddComponent<Button>();
                btn.targetGraphic = plate;
                var colors = btn.colors;
                colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
                colors.fadeDuration = 0f;
                btn.colors = colors;
                btn.onClick.AddListener(() => ShelfMenu(captured));

                var press = slot.gameObject.AddComponent<UiKit.Press>();
                press.plate = plate;
                press.edge = edge;
                press.plateUp = plate.color;
                press.plateDown = Palette.Rust;
                press.edgeUp = edge.color;
                press.edgeDown = Palette.Rust;
            }
        }

        static void ShelfMenu(MadeCube m)
        {
            // Two things a built cube can do, and no submenu for two things.
            _shelfMsg.text = (m.name + " — " + m.id).ToUpperInvariant();
            _shelfMsg.color = Palette.Dim;
            OpenEditor(Forge.From(m));
        }

        // ---- the editor ------------------------------------------------------

        static void BuildEditor()
        {
            RectTransform L = Screens.Layer("forgeEdit");
            Flow col = UiKit.Column(L, false);

            // .edBar — the deck stepper
            RectTransform bar = UiKit.Slot(col, "edBar", Css.HCtl, Css.GridMax, 12f);
            RectTransform dn = UiKit.Rect(bar, "dn", new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          Vector2.zero, new Vector2(Css.HCtl, 0f));
            UiKit.PillBtn(dn, "[ < ]", () => { _ed.layer = Mathf.Max(0, _ed.layer - 1); PaintSlice(); });
            RectTransform up = UiKit.Rect(bar, "up", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                          new Vector2(-Css.HCtl, 0f), Vector2.zero);
            UiKit.PillBtn(up, "[ > ]", () => { _ed.layer = Mathf.Min(_ed.n - 1, _ed.layer + 1); PaintSlice(); });
            RectTransform deckId = UiKit.Rect(bar, "vaultId", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                              new Vector2(Css.HCtl + 10f, 0f), new Vector2(-Css.HCtl - 10f, 0f));
            _deckLabel = UiKit.Type(deckId, "deck", "DECK 1 / 5", Css.TTiny, Palette.Dim,
                                    TextAnchor.MiddleCenter, 0.18f);

            // .edCoach. An empty grid and ten unlabelled tools is not an editor, it
            // is a puzzle about an editor — so the one next thing standing between
            // this cube and a saveable cube is named, in a sentence, above the deck.
            // Always one thing and never a checklist: a checklist is read once, and
            // a next step is read every time it changes.
            _coach = UiKit.Slot(col, "edCoach", Css.TMicro * 1.45f * 2f + 20f, Css.ManualMax, 10f);
            var cp = _coach.gameObject.AddComponent<Image>();
            cp.sprite = UiKit.Frame(Css.RCard, 0f);
            cp.type = Image.Type.Sliced;
            cp.color = Palette.PanelHi;
            cp.raycastTarget = false;
            UiKit.Edge(_coach, Css.EdgeOn, Css.RCard, Css.Hair);

            RectTransform nDisc = UiKit.Rect(_coach, "edCoachN", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                             Vector2.zero, Vector2.zero);
            nDisc.sizeDelta = new Vector2(20f, 20f);
            nDisc.anchoredPosition = new Vector2(12f + 10f, 0f);
            var disc = nDisc.gameObject.AddComponent<Image>();
            disc.sprite = UiKit.Disc;
            disc.type = Image.Type.Sliced;
            disc.color = Palette.Rust;
            disc.raycastTarget = false;
            _coachN = UiKit.Type(nDisc, "n", "1", 10f, Palette.Void, TextAnchor.MiddleCenter, 0f);

            RectTransform coachTxt = UiKit.Rect(_coach, "edCoachT", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                                new Vector2(12f + 20f + 10f, 10f), new Vector2(-12f, -10f));
            _coachText = UiKit.Para(coachTxt, "t", "", Css.TMicro, Palette.Ink, TextAnchor.UpperLeft, 0.08f);

            // .edSlice — the deck, made square by the script once it knows how wide
            // a cell came out
            _slice = UiKit.Slot(col, "edSlice", 0f, Css.ManualMax, 12f);
            _toolRow = UiKit.Slot(col, "edTools", 0f, Css.ManualMax, 12f);

            // .edRow — the name, the size and the teacher
            RectTransform nameRow = UiKit.Slot(col, "edRow", Css.HCtl, Css.ManualMax, 10f);
            RectTransform nf = UiKit.Rect(nameRow, "edName", Vector2.zero, new Vector2(1f, 1f),
                                          Vector2.zero, new Vector2(-(Css.HCtl * 2f + 16f), 0f));
            _nameField = UiKit.Field(nf, "NAME THIS CUBE");
            _nameField.characterLimit = 18;
            RectTransform sizeSlot = UiKit.Rect(nameRow, "edSize", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                                new Vector2(-(Css.HCtl * 2f + 8f), 0f), new Vector2(-(Css.HCtl + 8f), 0f));
            _sizeBtn = UiKit.PillBtn(sizeSlot, "[ 5 ]", CycleSize);
            RectTransform teach = UiKit.Rect(nameRow, "edTeach", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                             new Vector2(-Css.HCtl, 0f), Vector2.zero);
            UiKit.PillBtn(teach, "[ ? ]", Screens.ShowManual);

            // The code used to be printed into the verdict line next to a clipboard
            // call that does not exist offline. It was on screen and there was no
            // way on earth to get it off — so it lives in a selectable field, and
            // the row is hidden until there is a code to put in it.
            _codeRow = UiKit.Slot(col, "edCodeRow", Css.HCtl, Css.ManualMax, 10f);
            RectTransform cf = UiKit.Rect(_codeRow, "edCode", Vector2.zero, new Vector2(1f, 1f),
                                          Vector2.zero, new Vector2(-(Css.HCtl * 2f + 8f), 0f));
            _codeField = UiKit.Field(cf, "");
            _codeField.readOnly = true;
            // .edIn.code{ font-size:var(--t-micro);letter-spacing:.04em;color:var(--arc) }
            _codeField.textComponent.color = Palette.Arc;
            _codeField.textComponent.fontSize = Css.Pt(Css.TMicro);
            RectTransform copy = UiKit.Rect(_codeRow, "edCopy", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                            new Vector2(-Css.HCtl * 2f, 0f), Vector2.zero);
            UiKit.PillBtn(copy, "[ COPY ]", () =>
            {
                GUIUtility.systemCopyBuffer = _codeField.text;
                Status("COPIED", false);
            });

            RectTransform msgRow = UiKit.Slot(col, "edMsg", Css.TMicro * 2.6f, Css.ManualMax, 10f);
            _edMsg = UiKit.Type(msgRow, "m", "", Css.TMicro, Palette.Dim, TextAnchor.UpperCenter, 0.09f);

            RectTransform two = UiKit.Slot(col, "btn2", Css.HBtn + 6f, Css.BtnMax, 11f);
            RectTransform vSlot = UiKit.Rect(two, "verify", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                                             Vector2.zero, new Vector2(-5f, 0f));
            UiKit.Btn(vSlot, "VERIFY", DoVerify, UiKit.Kind.Btn, Css.TSm, 0.12f);
            RectTransform sSlot = UiKit.Rect(two, "save", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                                             new Vector2(5f, 0f), Vector2.zero);
            UiKit.Btn(sSlot, "SAVE", DoSave, UiKit.Kind.Primary, Css.TSm, 0.12f);

            // BACK goes up one, to the shelf; MENU goes home. Both are safe here
            // because the draft is written on every edit, so leaving the editor by
            // either door loses nothing.
            RectTransform three = UiKit.Slot(col, "btn2three", Css.HBtn + 6f, Css.BtnMax, 11f);
            Third(three, 0, "PLAY", DoPlay, UiKit.Kind.Btn);
            Third(three, 1, "BACK", Screens.Back, UiKit.Kind.Ghost);
            Third(three, 2, "MENU", Screens.ShowTitle, UiKit.Kind.Ghost);

            // Only on a cube that has actually been saved: a new one has nothing to
            // delete, and a button that does nothing is worse than no button.
            _deleteRow = UiKit.Slot(col, "edDelete", Css.HCtl, Css.BtnMax, 0f);
            UiKit.Btn(_deleteRow, "DELETE THIS CUBE", () =>
            {
                _ed.Delete();
                Status("DELETED", false);
                OpenShelf();
            }, UiKit.Kind.Ghost);
        }

        static void Third(RectTransform parent, int i, string label, System.Action act, UiKit.Kind kind)
        {
            const float Gap = 10f;
            RectTransform slot = UiKit.Rect(parent, label,
                new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f),
                new Vector2(i == 0 ? 0f : Gap * i / 3f, 0f),
                new Vector2(i == 2 ? 0f : -Gap * (2 - i) / 3f, 0f));
            UiKit.Btn(slot, label, act, kind, Css.TFine, 0.08f);
        }

        public static void OpenEditor(Forge f)
        {
            _ed = f;
            _nameField.text = f.name ?? "";
            UiKit.SetLabel(_sizeBtn, f.n.ToString());
            _deleteRow.gameObject.SetActive(f.from != null);
            _codeField.text = "";
            _codeRow.gameObject.SetActive(false);
            Status("", false);
            PaintTools();
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
            _coachText.text = say.ToUpperInvariant();
        }

        /// <summary>
        /// .edTools — five to a row, and the one in hand is `.on`: ink on panel-hi
        /// inside a lit edge, rather than the same tile with different coloured
        /// type. A selected state that is only a colour is a state you have to
        /// look for.
        /// </summary>
        static void PaintTools()
        {
            for (int i = _toolRow.childCount - 1; i >= 0; i--) Object.Destroy(_toolRow.GetChild(i).gameObject);

            const int Cols = 5;
            const float Gap = 5f;
            float avail = Mathf.Min(Css.ManualMax, Css.VpW - Css.PadX * 2f);
            float w = (avail - Gap * (Cols - 1)) / Cols;
            float h = Css.TMicro * 1.1f * 2f + 18f;
            int rows = Mathf.CeilToInt(Forge.Tools.Length / (float)Cols);
            _toolRow.sizeDelta = new Vector2(_toolRow.sizeDelta.x, rows * h + (rows - 1) * Gap);

            for (int i = 0; i < Forge.Tools.Length; i++)
            {
                Forge.Tool t = Forge.Tools[i];
                int cx = i % Cols, cy = i / Cols;
                RectTransform slot = UiKit.Rect(_toolRow, "t" + t.k,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                slot.pivot = new Vector2(0f, 1f);
                slot.sizeDelta = new Vector2(w, h);
                slot.anchoredPosition = new Vector2(cx * (w + Gap), -cy * (h + Gap));

                bool on = _ed != null && _ed.tool == t.k;
                var plate = slot.gameObject.AddComponent<Image>();
                plate.sprite = UiKit.Frame(Css.RCard, 0f);
                plate.type = Image.Type.Sliced;
                plate.color = on ? Palette.PanelHi : Palette.Panel;
                UiKit.Edge(slot, on ? Css.EdgeOn : Css.Edge2, Css.RCard, Css.Hair);
                UiKit.Type(slot, "l", t.n, Css.TMicro, on ? Palette.Ink : Palette.Dim,
                           TextAnchor.MiddleCenter, 0.05f);

                char k = t.k;
                var btn = slot.gameObject.AddComponent<Button>();
                btn.targetGraphic = plate;
                var colors = btn.colors;
                colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
                colors.fadeDuration = 0f;
                btn.colors = colors;
                btn.onClick.AddListener(() =>
                {
                    if (k == 'C') { _ed.ClearDeck(); Status("", false); PaintSlice(); return; }
                    _ed.tool = k;
                    PaintTools();
                    PaintSlice();
                });
            }
        }

        /// <summary>
        /// .edSlice — an n-by-n deck of `.edCell`, coloured by what is in the voxel
        /// and marked with a `b` for whatever object sits on it. The classes and
        /// their colours are the sheet's:
        ///
        ///   .edCell     void-2 on a dim-2 hairline   nothing
        ///   .edCell.g   grid on grid-hi              lattice
        ///   .edCell.t   trace-lo on trace            a trace
        ///   .edCell.pa  trace-lo on a 2px arc        a plate, one side
        ///   .edCell.pb  trace-lo on a 2px rust       a plate, the other
        /// </summary>
        static void PaintSlice()
        {
            PaintCoach();
            for (int i = _slice.childCount - 1; i >= 0; i--) Object.Destroy(_slice.GetChild(i).gameObject);

            _deckLabel.text = "DECK " + (_ed.layer + 1) + " / " + _ed.n;

            int n = _ed.n;
            const float Gap = 4f;
            float avail = Mathf.Min(Css.ManualMax, Css.VpW - Css.PadX * 2f);
            float cell = (avail - Gap * (n - 1)) / n;
            _slice.sizeDelta = new Vector2(_slice.sizeDelta.x, n * cell + (n - 1) * Gap);

            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    // z runs up the screen, so deck row 0 is at the bottom — the same
                    // orientation the level literals are written in, which is the only
                    // way hand-authoring one is survivable.
                    RectTransform slot = UiKit.Rect(_slice, x + "," + z,
                        new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
                    slot.pivot = new Vector2(0f, 0f);
                    slot.sizeDelta = new Vector2(cell, cell);
                    slot.anchoredPosition = new Vector2(x * (cell + Gap), z * (cell + Gap));

                    char c = _ed.vox[Level.Vidx(n, x, _ed.layer, z)];
                    char mark = _ed.MarkAt(new Int3(x, _ed.layer, z));

                    bool trace = c == '+';
                    bool lattice = c == '#';
                    bool glyph = Level.IsGlyph(c);

                    Color fill = trace || glyph ? Palette.TraceLo
                               : lattice ? Palette.Grid
                               : Palette.Void2;
                    Color line = trace ? Palette.Trace
                               : lattice ? Palette.GridHi
                               : glyph ? Palette.Rust
                               : Palette.Dim2;

                    var img = slot.gameObject.AddComponent<Image>();
                    img.sprite = UiKit.Frame(2f, 0f);
                    img.type = Image.Type.Sliced;
                    img.color = fill;
                    UiKit.Edge(slot, line, 2f, glyph ? 2f : Css.Hair);

                    // .edCell b — a 54% disc with the object's letter in black on it,
                    // and the shape says which object: a circle is the start, a
                    // rounded square the core, a square a node or a lock.
                    if (mark != '\0')
                    {
                        RectTransform b = UiKit.Rect(slot, "b", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                     Vector2.zero, Vector2.zero);
                        b.sizeDelta = new Vector2(cell * 0.54f, cell * 0.54f);
                        var dot = b.gameObject.AddComponent<Image>();
                        dot.type = Image.Type.Sliced;
                        dot.raycastTarget = false;
                        dot.sprite = mark == 'S' ? UiKit.Disc
                                   : mark == 'E' ? UiKit.Frame(2f, 0f)
                                   : UiKit.Frame(0.5f, 0f);
                        dot.color = mark == 'S' ? Palette.Rust
                                  : mark == 'E' ? Palette.Core
                                  : mark == 'N' ? Palette.Node
                                  : Palette.Lock;
                        UiKit.Type(b, "l", mark.ToString(), 11f, Palette.Void, TextAnchor.MiddleCenter, 0f);
                    }

                    int cx2 = x, cz2 = z;
                    var btn = slot.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    var colors = btn.colors;
                    colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
                    colors.fadeDuration = 0f;
                    btn.colors = colors;
                    btn.onClick.AddListener(() =>
                    {
                        _ed.Apply(cx2, cz2);
                        _codeField.text = "";
                        _codeRow.gameObject.SetActive(false);
                        Status("", false);
                        PaintSlice();
                    });
                }
        }

        static void Status(string msg, bool bad)
        {
            _edMsg.text = (msg ?? "").ToUpperInvariant();
            // .edMsg.bad is fault, .edMsg.good is arc
            _edMsg.color = string.IsNullOrEmpty(msg) ? Palette.Dim : bad ? Palette.Fault : Palette.Arc;
        }

        static void ShowCode(string code)
        {
            bool has = !string.IsNullOrEmpty(code);
            _codeField.text = code ?? "";
            _codeRow.gameObject.SetActive(has);
        }

        static void DoVerify()
        {
            _ed.name = Forge.CleanName(_nameField.text);
            Forge.VerifyResult v = _ed.Verify();
            Status(v.message, !v.ok);
            ShowCode(v.ok ? ShareCode.Encode(v.level) : "");
        }

        static void DoSave()
        {
            _ed.name = Forge.CleanName(_nameField.text);
            bool ok = _ed.Save(out string msg);
            Status(msg, !ok);
            if (!ok) return;
            _nameField.text = _ed.name;
            _deleteRow.gameObject.SetActive(true);
            MadeCube rec = Store.Made(_ed.from);
            if (rec != null) ShowCode(rec.code);
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
