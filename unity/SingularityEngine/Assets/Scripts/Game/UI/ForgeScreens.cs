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
        static RectTransform _slice, _toolRow;
        static Text _deckLabel, _edMsg, _codeText;
        static InputField _nameField;
        static Button _sizeBtn, _deleteBtn;

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

            RectTransform bot = UiKit.Rect(L, "bot", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(60, 90), new Vector2(-60, 200));
            Screens.Row(bot, 0, 2, "NEW CUBE", () => OpenEditor(Forge.New()), true);
            Screens.Row(bot, 1, 2, "BACK", Screens.Back);
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
            if (made.Count == 0)
            {
                UiKit.Label(_shelfGrid, "empty",
                            "NOTHING BUILT YET.\n\nA cube is edited one deck at a time.\nThe solver proves it before you can save it.",
                            20, Palette.Dim, TextAnchor.MiddleCenter,
                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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

            RectTransform bar = UiKit.Rect(L, "deckBar", new Vector2(0, 1), new Vector2(1, 1),
                                           new Vector2(40, -150), new Vector2(-40, -90));
            RectTransform dn = UiKit.Rect(bar, "dn", new Vector2(0, 0), new Vector2(0.22f, 1), Vector2.zero, Vector2.zero);
            UiKit.Bracketed(dn, "dn", "<", () => { _ed.layer = Mathf.Max(0, _ed.layer - 1); PaintSlice(); }, 24);
            _deckLabel = UiKit.Label(bar, "deck", "DECK 1 / 5", 24, Palette.Ink, TextAnchor.MiddleCenter,
                                     new Vector2(0.22f, 0), new Vector2(0.78f, 1), Vector2.zero, Vector2.zero);
            RectTransform up = UiKit.Rect(bar, "up", new Vector2(0.78f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Bracketed(up, "up", ">", () => { _ed.layer = Mathf.Min(_ed.n - 1, _ed.layer + 1); PaintSlice(); }, 24);

            _slice = UiKit.Rect(L, "slice", new Vector2(0, 1), new Vector2(1, 1),
                                new Vector2(60, -700), new Vector2(-60, -170));

            _toolRow = UiKit.Rect(L, "tools", new Vector2(0, 1), new Vector2(1, 1),
                                  new Vector2(30, -880), new Vector2(-30, -715));

            RectTransform nameRow = UiKit.Rect(L, "nameRow", new Vector2(0, 0), new Vector2(1, 0),
                                               new Vector2(40, 400), new Vector2(-40, 466));
            _nameField = UiKit.Field(nameRow, "name", "NAME THIS CUBE",
                                     new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, new Vector2(-8, 0));
            _nameField.characterLimit = 18;
            RectTransform sizeSlot = UiKit.Rect(nameRow, "sizeSlot", new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            _sizeBtn = UiKit.Bracketed(sizeSlot, "size", "5", CycleSize, 24);

            // The code used to be printed into the verdict line next to a clipboard
            // call that does not exist offline. It was on screen and there was no
            // way on earth to get it off — so it lives in a selectable field.
            RectTransform codeRow = UiKit.Rect(L, "codeRow", new Vector2(0, 0), new Vector2(1, 0),
                                               new Vector2(40, 330), new Vector2(-40, 392));
            InputField codeField = UiKit.Field(codeRow, "code", "", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            codeField.readOnly = true;
            _codeText = codeField.textComponent;

            _edMsg = UiKit.Label(L, "msg", "", 18, Palette.Dim, TextAnchor.UpperCenter,
                                 new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 288), new Vector2(-40, 326));

            RectTransform two = UiKit.Rect(L, "two", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(40, 214), new Vector2(-40, 280));
            RectTransform vSlot = UiKit.Rect(two, "v", new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-6, 0));
            UiKit.Bracketed(vSlot, "verify", "VERIFY", DoVerify, 24);
            RectTransform sSlot = UiKit.Rect(two, "s", new Vector2(0.5f, 0), Vector2.one, new Vector2(6, 0), Vector2.zero);
            UiKit.Bracketed(sSlot, "save", "SAVE", DoSave, 24, true);

            RectTransform three = UiKit.Rect(L, "three", new Vector2(0, 0), new Vector2(1, 0),
                                             new Vector2(40, 140), new Vector2(-40, 206));
            Third(three, 0, "PLAY", DoPlay);
            Third(three, 1, "BACK", Screens.Back);
            Third(three, 2, "MENU", Screens.ShowTitle);

            RectTransform del = UiKit.Rect(L, "del", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(40, 70), new Vector2(-40, 132));
            _deleteBtn = UiKit.Bracketed(del, "delete", "DELETE THIS CUBE", () =>
            {
                _ed.Delete();
                Status("DELETED", false);
                OpenShelf();
            }, 22);
        }

        static void Third(RectTransform parent, int i, string label, System.Action act)
        {
            RectTransform slot = UiKit.Rect(parent, label, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                                            new Vector2(5, 0), new Vector2(-5, 0));
            UiKit.Bracketed(slot, label, label, act, 22);
        }

        public static void OpenEditor(Forge f)
        {
            _ed = f;
            _nameField.text = f.name ?? "";
            UiKit.SetLabel(_sizeBtn, f.n.ToString());
            _deleteBtn.gameObject.SetActive(f.from != null);
            _codeText.text = "";
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
            for (int i = _slice.childCount - 1; i >= 0; i--) Object.Destroy(_slice.GetChild(i).gameObject);

            _deckLabel.text = "DECK " + (_ed.layer + 1) + " / " + _ed.n;

            int n = _ed.n;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    // z runs up the screen, so deck row 0 is at the bottom — the same
                    // orientation the level literals are written in, which is the only
                    // way hand-authoring one is survivable.
                    RectTransform slot = UiKit.Rect(_slice, x + "," + z,
                        new Vector2(x / (float)n, z / (float)n),
                        new Vector2((x + 1) / (float)n, (z + 1) / (float)n),
                        new Vector2(2, 2), new Vector2(-2, -2));

                    char c = _ed.vox[Level.Vidx(n, x, _ed.layer, z)];
                    char mark = _ed.MarkAt(new Int3(x, _ed.layer, z));

                    Color col = c == '+' ? Palette.TraceLo
                              : c == '#' ? Palette.Grid
                              : Level.IsGlyph(c) ? Palette.Rust * 0.5f
                              : Palette.Void3;

                    var img = slot.gameObject.AddComponent<Image>();
                    img.color = col;
                    var btn = slot.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;

                    if (mark != '\0')
                        UiKit.Label(slot, "m", mark.ToString(), 22,
                                    mark == 'S' ? Palette.Arc : mark == 'E' ? Palette.Core
                                    : mark == 'N' ? Palette.Node : Palette.Lock,
                                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    else if (Level.IsGlyph(c))
                        UiKit.Label(slot, "g", c.ToString(), 20, Palette.RustHi, TextAnchor.MiddleCenter,
                                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                    int cx2 = x, cz2 = z;
                    btn.onClick.AddListener(() =>
                    {
                        _ed.Apply(cx2, cz2);
                        _codeText.text = "";
                        Status("", false);
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
