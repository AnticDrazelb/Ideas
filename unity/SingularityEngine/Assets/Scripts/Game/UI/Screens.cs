using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// Every screen that is not the board.
    ///
    /// One canvas, one card at a time, and a stack so BACK always goes up one
    /// rather than home. The board keeps running underneath — the attract cube on
    /// the title screen is the same renderer and the same rules, just with nobody
    /// playing it.
    /// </summary>
    public static class Screens
    {
        static Canvas _canvas;
        static GameDirector _dir;
        static readonly Dictionary<string, GameObject> Layers = new Dictionary<string, GameObject>();
        static readonly List<string> Stack = new List<string>();
        static int _viewBand;

        public static void Build(GameDirector dir)
        {
            _dir = dir;
            _canvas = UiKit.Canvas("Screens", 20);
            BuildTitle();
            BuildVaults();
            BuildManual();
            BuildPlateTeach();
            BuildPause();
            BuildCalibrate();
            BuildBoards();
            BuildWin();
            ForgeScreens.Build(dir);
            HideAll();
        }

        // ---- plumbing -------------------------------------------------------

        internal static RectTransform Layer(string id)
        {
            RectTransform rt = UiKit.Rect(_canvas.transform, id, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = Palette.Scrim;
            Layers[id] = rt.gameObject;
            return rt;
        }

        /// <summary>
        /// A SCREEN MADE OF WORDS IS NOT A WINDOW.
        ///
        /// The pause card sits over a puzzle somebody is in the middle of and should
        /// let it through. The manual, the plate lesson, calibrate, the vault list
        /// and the Forge are text from top to bottom — and brickwork behind a
        /// paragraph is just contrast the reader has to fight. Those get a solid
        /// plate, which is also the difference between an interface and a stack of
        /// labels floating over a rotating object.
        /// </summary>
        static void Solid(RectTransform L) => L.GetComponent<Image>().color = Palette.Void;

        internal static void SolidLayer(RectTransform L) => Solid(L);

        static void HideAll() { foreach (var kv in Layers) kv.Value.SetActive(false); }

        public static void Show(string id)
        {
            HideAll();
            if (id != null)
            {
                Layers[id].SetActive(true);
                if (Stack.Count == 0 || Stack[Stack.Count - 1] != id) Stack.Add(id);
            }
            else Stack.Clear();
            _dir.SetScreenUp(id != null);
        }

        public static void Back()
        {
            if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
            Show(Stack.Count > 0 ? Stack[Stack.Count - 1] : null);
        }

        internal static RectTransform Column(RectTransform parent, float top, float bottom)
            => UiKit.Rect(parent, "col", new Vector2(0, 0), new Vector2(1, 1),
                          new Vector2(40, bottom), new Vector2(-40, -top));

        /// <summary>One of the four quiet destinations under the primary: 0,1 top row; 2,3 bottom.</summary>
        static void Quad(RectTransform col, int i, string label, System.Action act)
        {
            float x = i % 2, y = i / 2;
            RectTransform r = UiKit.Rect(col, label,
                new Vector2(x * 0.5f, (1 - y) * 0.28f), new Vector2((x + 1) * 0.5f, (2 - y) * 0.28f),
                new Vector2(x == 0 ? 0 : 6, 6), new Vector2(x == 0 ? -6 : 0, -6));
            UiKit.Bracketed(r, label, label, act, 24);
        }

        internal static void Row(RectTransform col, int slot, int of, string label, System.Action act, bool primary = false)
        {
            float h = 1f / of;
            RectTransform r = UiKit.Rect(col, label, new Vector2(0, 1 - (slot + 1) * h), new Vector2(1, 1 - slot * h),
                                         new Vector2(0, 8), new Vector2(0, -8));
            UiKit.Bracketed(r, label, label, act, 28, primary);
        }

        // ---- title ----------------------------------------------------------

        static Text _playLabel, _resumeName, _resumeVault;

        static void BuildTitle()
        {
            RectTransform L = Layer("title");
            UiKit.Stage(L);

            UiKit.Label(L, "wordmark1", "SINGULARITY", 66, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, -90));
            UiKit.Label(L, "wordmark2", "ENGINE", 66, Palette.Rust, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -252), new Vector2(0, -172));

            // WHERE YOU LEFT OFF, ON THE SCREEN THAT OFFERS TO TAKE YOU BACK.
            // The play button used to be a promise with no subject: it did not say
            // which cube, which vault, or what it would cost. Naming the thing a
            // button is about is the cheapest confidence a menu can buy.
            _resumeVault = UiKit.Label(L, "resumeVault", "", 19, Palette.Dim, TextAnchor.UpperCenter,
                                       new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -290), new Vector2(0, -262));
            _resumeName = UiKit.Label(L, "resume", "", 24, Palette.Ink, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -322), new Vector2(0, -292));

            UiKit.Label(L, "sub",
                "You are a black hole inside a broken machine.\nFold the engine until its circuits align,\nthen collapse into the core.",
                22, Palette.Dim, TextAnchor.UpperCenter,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -420), new Vector2(0, -330));

            // ONE PRIMARY, THEN A GRID OF FOUR.
            //
            // Five equal rows is a list, and a list has no answer to "what do I
            // press". The thing you came here to do is a full-width solid; the four
            // places you might go instead are a quiet two-by-two under it. That
            // shape also buys back most of the height five stacked rows were
            // spending, which is what leaves room for the cube to be the biggest
            // object on its own title screen.
            RectTransform col = UiKit.Rect(L, "buttons", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(48, 96), new Vector2(-48, 400));

            RectTransform go = UiKit.Rect(col, "CONTINUE", new Vector2(0, 0.56f), new Vector2(1, 1),
                                          new Vector2(0, 8), new Vector2(0, 0));
            UiKit.Bracketed(go, "CONTINUE", "CONTINUE",
                            () => { Show(null); _dir.Play(Mathf.Max(1, Store.Data.reached)); }, 30, true);

            Quad(col, 0, "DAILY", () => { Show(null); _dir.Play(Daily.SpecLevel, LoadKind.Daily); });
            Quad(col, 1, "VAULTS", OpenVaults);
            Quad(col, 2, "FORGE", ForgeScreens.OpenShelf);
            Quad(col, 3, "CALIBRATE", ShowCalibrate);

            _playLabel = go.GetComponentInChildren<Text>();

            UiKit.Label(L, "foot", "DIAGNOSTIC BUILD · NO DEAD PIXELS", 15, Palette.Dim2, TextAnchor.LowerCenter,
                        new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40), new Vector2(0, 76));
        }

        public static void ShowTitle()
        {
            int r = Mathf.Max(1, Store.Data.reached);
            // A machine that has never been started is INITIALISED, not continued.
            if (_playLabel != null) _playLabel.text = r <= 1 ? "[ INITIALISE ]" : "[ CONTINUE ]";

            int band = Vaults.VaultOf(r);
            if (_resumeVault != null)
                _resumeVault.text = "VAULT " + Vaults.RomanOf(band) + ": " + Vaults.VaultName(band);
            if (_resumeName != null)
            {
                // the cube's own name and what it costs, so the button has a subject
                int par = r <= Baked.Levels.Length ? Baked.Levels[r - 1].par : -1;
                _resumeName.text = Vaults.LevelName(r) + (par >= 0 ? "  /  PAR " + par : "");
            }
            Stack.Clear();
            Show("title");
            _dir.ShowAttract();
        }

        // ---- vault select ---------------------------------------------------

        static Text _vaultName;
        static RectTransform _grid;

        static void BuildVaults()
        {
            RectTransform L = Layer("vaults");
            Solid(L);

            _vaultName = UiKit.Label(L, "vaultName", "VAULT I", 32, Palette.Ink, TextAnchor.MiddleCenter,
                                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(120, -140), new Vector2(-120, -80));

            RectTransform prev = UiKit.Rect(L, "prev", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -140), new Vector2(140, -80));
            UiKit.Bracketed(prev, "prev", "<", () => { _viewBand = Mathf.Max(0, _viewBand - 1); PaintVaults(); }, 26);
            RectTransform next = UiKit.Rect(L, "next", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -140), new Vector2(-40, -80));
            UiKit.Bracketed(next, "next", ">", () => { _viewBand++; PaintVaults(); }, 26);

            _grid = UiKit.Rect(L, "grid", new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 300), new Vector2(-40, -170));

            // THE SEED BOX. Every generated cube in this game is a pure function of
            // its number, so the NUMBER IS THE SEED — a few characters that cut the
            // same puzzle on every device, forever. There has to be a way to type one.
            //
            // And typing one is a seed viewer, not a shortcut: a jump past `reached`
            // records nothing, unlocks nothing and posts nothing, or the whole
            // progression would be one text field wide.
            // THESE FOLLOW THE GRID RATHER THAN THE FLOOR.
            //
            // They were pinned to the bottom of the screen while the grid was pinned
            // to the top, so a ten-cube vault left seven hundred pixels of nothing
            // between the rack and the seed box — the two halves of one screen with
            // a hole punched between them. PaintVaults now puts them directly under
            // however many rows it actually drew.
            _jumpRow = UiKit.Rect(L, "jumpRow", new Vector2(0, 1), new Vector2(1, 1),
                                  new Vector2(40, -286), new Vector2(-40, -220));
            _jumpField = UiKit.Field(_jumpRow, "jump", "CUBE NUMBER",
                                     new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, new Vector2(-8, 0));
            _jumpField.contentType = InputField.ContentType.IntegerNumber;
            RectTransform goSlot = UiKit.Rect(_jumpRow, "goSlot", new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Bracketed(goSlot, "go", "GO", Jump, 24);

            _jumpMsg = UiKit.Label(L, "jumpMsg", "", 18, Palette.Dim, TextAnchor.UpperCenter,
                                   new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -324), new Vector2(-40, -290));

            RectTransform back = UiKit.Rect(L, "back", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(back, "back", "BACK", Back, 28);
        }

        static InputField _jumpField;
        static Text _jumpMsg;
        static RectTransform _jumpRow;

        static void Jump()
        {
            if (!int.TryParse(_jumpField.text, out int level) || level < 1)
            {
                _jumpMsg.text = "TYPE A CUBE NUMBER";
                _jumpMsg.color = Palette.Fault;
                return;
            }
            // The generator has no last cube, but a text field needs a ceiling: a
            // fat-fingered 99999999 is otherwise a minute of minting nobody asked for.
            if (level > Vaults.MaxCube)
            {
                _jumpMsg.text = "THE CATALOGUE STOPS AT " + Vaults.MaxCube;
                _jumpMsg.color = Palette.Fault;
                return;
            }

            bool earned = level <= Store.Data.reached;
            _jumpMsg.text = "";
            Show(null);
            _dir.Play(level, earned ? LoadKind.Vault : LoadKind.Practice);
        }

        static void OpenVaults()
        {
            _viewBand = Vaults.VaultOf(Mathf.Max(1, Store.Data.reached));
            PaintVaults();
            Show("vaults");
        }

        static void PaintVaults()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Object.Destroy(_grid.GetChild(i).gameObject);

            _vaultName.text = "VAULT " + Vaults.RomanOf(_viewBand) + " · " + Vaults.VaultName(_viewBand);

            int start = Vaults.VaultStart(_viewBand);
            int size = Vaults.VaultSize(_viewBand);
            int cols = 5;
            int rows = Mathf.CeilToInt(size / (float)cols);

            // A CUBE IS A SQUARE, NOT A SHARE OF THE SCREEN.
            //
            // The rows used to divide the grid's whole height between them, so a
            // ten-cube vault got two four-hundred-pixel-tall buttons and the screen
            // read as a broken table rather than a rack of cubes. The cell is sized
            // off the WIDTH — which is the axis that is actually constrained — and
            // the block is pinned to the top of the space it was given.
            float cellW = (_grid.rect.width - 12f) / cols;
            float cellH = Mathf.Min(cellW, 96f);

            // and the seed box follows the rack rather than the floor
            float below = 170f + rows * cellH + 26f;
            _jumpRow.offsetMax = new Vector2(-40, -below);
            _jumpRow.offsetMin = new Vector2(40, -(below + 62f));
            _jumpMsg.rectTransform.offsetMax = new Vector2(-40, -(below + 68f));
            _jumpMsg.rectTransform.offsetMin = new Vector2(40, -(below + 102f));

            for (int i = 0; i < size; i++)
            {
                int level = start + i;
                int cx = i % cols, cy = i / cols;
                RectTransform slot = UiKit.Rect(_grid, "c" + level,
                    new Vector2(cx / (float)cols, 1f), new Vector2((cx + 1f) / cols, 1f),
                    new Vector2(6, -(cy + 1) * cellH + 6), new Vector2(-6, -cy * cellH - 6));

                bool reached = level <= Store.Data.reached;
                bool cleared = Store.TryBest(level, out int best);
                // A cube visited by number is a cube seen, not a cube cleared — so a
                // jump past `reached` is offered, and it records nothing.
                string face = cleared ? best.ToString() : reached ? level.ToString() : "·";

                int lv = level;
                var btn = UiKit.Bracketed(slot, "b", face,
                    () => { Show(null); _dir.Play(lv, reached ? LoadKind.Vault : LoadKind.Practice); }, 24);
                if (cleared) btn.GetComponentInChildren<Text>().color = Palette.Lock;
                else if (!reached) btn.GetComponentInChildren<Text>().color = Palette.Dim2;
            }
        }

        // ---- the manual -----------------------------------------------------

        static void BuildManual()
        {
            RectTransform L = Layer("manual");
            Solid(L);
            UiKit.Label(L, "h", "CALIBRATION", 40, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -80));

            // NINE LINES, NOT NINE PARAGRAPHS — AND A PICTURE FOR EACH.
            //
            // Nobody reads a manual in a puzzle game. They glance at it, twice, and
            // go back to the cube. So every rule is a label you can take in with one
            // saccade, a line under it, and a DIAGRAM beside it — because the four
            // things this page has to explain are all spatial, and a sentence about
            // a fold is a worse description of a fold than two squares and an arrow.
            float y = 170f;

            void Kicker(string s)
            {
                UiKit.Label(L, s, s, 16, Palette.Rust, TextAnchor.LowerLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -(y + 24)), new Vector2(-44, -y));
                y += 28;
            }

            RectTransform Entry(string head, string body, float h = 88f)
            {
                UiKit.Label(L, head, head, 23, Palette.Ink, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -(y + 30)), new Vector2(-190, -y));
                UiKit.Label(L, head + "_d", body, 17, Palette.Dim, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -(y + h)), new Vector2(-190, -(y + 32)));

                // the diagram lives in a fixed gutter on the right, so every picture
                // on the page shares one optical column
                RectTransform art = UiKit.Rect(L, head + "_art", new Vector2(1, 1), new Vector2(1, 1),
                                               new Vector2(-176, -(y + 96)), new Vector2(-44, -(y + 4)));
                y += h + 16f;
                return art;
            }

            Image Mark(RectTransform art, string role, Color c, float size, float x, float v)
                => UiKit.Icon(art, role, c, size, new Vector2(0.5f, 0.5f), new Vector2(x, v));

            Kicker("THE RULE");
            RectTransform a1 = Entry("ONE FACE AT A TIME",
                "Two traces that line up when you fold are connected,\nhowever far apart they look.");
            // two decks, and the fold that brings them together
            Mark(a1, "sq", Palette.Trace, 34, -22, 28);
            Mark(a1, "sq", Palette.Trace, 34, -22, -28);
            Mark(a1, "arrow", Palette.Rust, 26, 26, 0);

            Kicker("THE VERBS");
            RectTransform a2 = Entry("SWIPE TO FOLD",
                "Past halfway it commits. The ticks say which folds have footing.");
            Mark(a2, "sq", Palette.Trace, 30, 0, 0);
            Mark(a2, "arrow", Palette.Rust, 20, 0, 34);
            Mark(a2, "arrow", Palette.Rust, 20, 0, -34).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            Mark(a2, "arrow", Palette.Rust, 20, -34, 0).rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            Mark(a2, "arrow", Palette.Rust, 20, 34, 0).rectTransform.localRotation = Quaternion.Euler(0, 0, -90);

            RectTransform a3 = Entry("TAP TO MOVE",
                "The singularity walks to any trace connected to the one under it.");
            // a column of three, and you on the surface of it
            Mark(a3, "sq", Palette.Trace, 30, 0, 32);
            Mark(a3, "sq", Palette.Trace, 30, 0, 0);
            Mark(a3, "sq", Palette.Trace, 30, 0, -32);
            Mark(a3, "hole", Palette.Void, 15, 0, 32);
            Mark(a3, "ring", Palette.Arc, 17, 0, 32);

            RectTransform a4 = Entry("COLLAPSE INTO THE CORE", "Reach it and the vault is solved.", 62f);
            Mark(a4, "core", Palette.Core, 52, 0, 0);

            Kicker("THE OBJECTS");
            RectTransform cards = UiKit.Rect(L, "cards", new Vector2(0, 1), new Vector2(1, 1),
                                             new Vector2(44, -(y + 92)), new Vector2(-44, -y));
            Card(cards, 0, "sqfill", Palette.Trace, "TRACE", "CIRCUIT");
            Card(cards, 1, "node", Palette.Node, "NODE", "COLLECT");
            Card(cards, 2, "lock", Palette.Lock, "LOCK", "BARRIER");
            Card(cards, 3, "plate", Palette.Rust, "PLATE", "INVERTS");
            y += 106f;

            Kicker("WORTH KNOWING");
            RectTransform a5 = Entry("HOLD FOR MATRIX",
                "The lattice goes to glass. Keep holding and drag to turn it.");
            // the solid, seen through — three faces of one box
            Mark(a5, "sq", Palette.Arc, 44, -8, -6);
            Mark(a5, "sq", new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, 0.45f), 44, 12, 12);
            Mark(a5, "sqfill", Palette.Trace, 14, -14, -12);

            RectTransform a6 = Entry("PLATES INVERT",
                "Every trace goes dead and every dead cell lights up, for five\nseconds. A plate is always footing, so every fold is legal on one.");
            Mark(a6, "sqfill", Palette.Trace, 26, -16, 14);
            Mark(a6, "sq", Palette.Dim2, 26, 14, 14);
            Mark(a6, "sq", Palette.Dim2, 26, -16, -16);
            Mark(a6, "sqfill", Palette.Trace, 26, 14, -16);

            RectTransform a7 = Entry("TO GO", "The fewest folds still possible from where you stand.", 62f);
            Mark(a7, "i.togo", Palette.Arc, 44, 0, 0);

            RectTransform got = UiKit.Rect(L, "got", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(got, "got", "GOT IT", Back, 28, true);
        }

        /// <summary>One of the four object cards: the mark, its name, and its job.</summary>
        static void Card(RectTransform row, int i, string glyph, Color col, string name, string job)
        {
            RectTransform c = UiKit.Rect(row, name, new Vector2(i / 4f, 0), new Vector2((i + 1) / 4f, 1),
                                         new Vector2(5, 0), new Vector2(-5, 0));
            UiKit.Framed(c, Palette.PanelHi, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.5f));
            UiKit.Icon(c, glyph, col, 34, new Vector2(0.5f, 1f), new Vector2(0, -32));
            UiKit.Label(c, "n", name, 16, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 26), new Vector2(0, 48));
            UiKit.Label(c, "j", job, 12, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 26));
        }

        public static void ShowManual() => Show("manual");

        // ---- pause ----------------------------------------------------------

        static void BuildPause()
        {
            RectTransform L = Layer("pause");
            UiKit.Label(L, "h", "PAUSED", 44, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -180), new Vector2(0, -120));

            RectTransform col = Column(L, 300, 200);
            Row(col, 0, 6, "RESUME", () => Show(null), true);
            Row(col, 1, 6, "RESET", () => { Show(null); _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey); });
            Row(col, 2, 6, "VAULTS", OpenVaults);
            Row(col, 3, 6, "BOARDS", ShowBoards);
            Row(col, 4, 6, "CALIBRATE", ShowCalibrate);
            Row(col, 5, 6, "MENU", ShowTitle);
        }

        public static void ShowPause(GameDirector dir) => Show("pause");

        // ---- the plate, taught once -----------------------------------------
        //
        // Shown the first time a cube carries one, and never again. A mechanic this
        // large should not arrive as a toast, and it should not arrive as a manual
        // page either — it arrives when the thing is on screen, with the thing
        // itself waiting behind the card.

        static void BuildPlateTeach()
        {
            RectTransform L = Layer("plate");
            Solid(L);
            UiKit.Label(L, "eyebrow", "A NEW COMPONENT", 20, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -100));
            UiKit.Label(L, "h", "PLATES", 44, Palette.Rust, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -140));
            UiKit.Label(L, "sub", "Everything you know how to do is about to be worth less.",
                        20, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -230), new Vector2(0, -200));

            string[] rows =
            {
                "THE LATTICE INVERTS|Every trace goes dead. Every dead cell lights up.\nNothing moves — only what carries current.",
                "IT LASTS FIVE SECONDS|Then the lattice springs back and puts you on the nearest\nplate. Running out costs you the walk, never the vault.",
                "TAP IT AGAIN TO PRESS IT AGAIN|A plate is a door between two versions of the same engine,\nand the core is usually only reachable in one of them.",
                "IT ALWAYS GIVES FOOTING|Cut clean through the lattice, visible from every face.\nWhile you stand on a plate every fold is legal.",
            };

            float y = -290;
            foreach (string r in rows)
            {
                string[] p = r.Split('|');
                UiKit.Label(L, p[0], p[0], 23, Palette.Ink, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 30), new Vector2(-48, y));
                UiKit.Label(L, p[0] + "_d", p[1], 19, Palette.Dim, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 92), new Vector2(-48, y - 32));
                y -= 118;
            }

            RectTransform go = UiKit.Rect(L, "go", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(go, "go", "STEP ON IT", () => Show(null), 28, true);
        }

        public static void ShowPlateTeach() => Show("plate");

        // ---- calibrate -------------------------------------------------------
        //
        // YOU CALIBRATE AN INSTRUMENT; YOU READ A MANUAL. These were the wrong way
        // round in an early build: the settings lived unlabelled inside the pause
        // card while the button called CALIBRATE opened the how-to, so a player
        // looking for brightness pressed CALIBRATE and got a rulebook.

        static void BuildCalibrate()
        {
            RectTransform L = Layer("calibrate");
            Solid(L);
            UiKit.Label(L, "h", "CALIBRATE", 40, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -90));
            UiKit.Label(L, "sub", "TUNE THE INSTRUMENT", 20, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -176), new Vector2(0, -146));

            // ONE PANEL, EIGHT ROWS, EACH WITH ITS OWN MARK.
            //
            // These were eight labels floating on black with [ON] buttons and plus
            // and minus steppers beside them. Two things were wrong with that. A
            // stepper is a fine way to change a number by one and a poor way to
            // answer "how bright, out of how bright it goes" — brightness is only
            // ever judged against the ends of its range, so it wants a POSITION,
            // not a reading. And a row of eight items that differ only in their
            // wording has to be read; a row where each carries a mark is
            // recognised, which is what you want on the screen somebody opens to
            // change one thing they already had in mind.
            RectTransform panel = UiKit.Rect(L, "panel", new Vector2(0, 0), new Vector2(1, 1),
                                             new Vector2(28, 190), new Vector2(-28, -200));
            UiKit.Framed(panel, Palette.Panel, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.55f));

            _calRow = 0;
            Toggle(panel, "i.sound", "SOUND", "", () => Store.Data.sound,
                   v => { Store.Data.sound = v; if (v == 0) Sfx.I.StopAmbience(); else Sfx.I.Ambience(Vaults.VaultOf(_dir.S.levelNo)); });
            Toggle(panel, "i.haptic", "HAPTICS", "", () => Store.Data.haptic, v => Store.Data.haptic = v);
            Toggle(panel, "i.fx", "EFFECTS", "SHAKE · SPARKS · BLOOM", () => Store.Data.fx,
                   v => { Store.Data.fx = v; _dir.Glow.Refresh(); });
            Toggle(panel, "i.togo", "FOLDS TO GO", "CYAN: PERFECT LINE · RUST: YOU SLIPPED",
                   () => Store.Data.togo, v => Store.Data.togo = v);
            Toggle(panel, "i.depth", "DEPTH READOUT", "DISTANCE PRINTED ON EVERY CELL",
                   () => Store.Data.depth, v => Store.Data.depth = v);
            Bar(panel, "i.buzz", "HAPTIC FEEDBACK", "INTENSITY", 0, 150, () => Store.Data.buzz, v => Store.Data.buzz = v);
            // Both default to 100, and at 100 no filter is applied at all — the
            // cost of these two is zero for anyone who leaves them alone.
            Bar(panel, "i.bright", "BRIGHTNESS", "", 60, 160, () => Store.Data.bright, v => Store.Data.bright = v);
            Bar(panel, "i.contrast", "CONTRAST", "", 70, 150, () => Store.Data.contrast, v => Store.Data.contrast = v);

            RectTransform feet = UiKit.Rect(L, "feet", new Vector2(0, 0), new Vector2(1, 0),
                                            new Vector2(40, 90), new Vector2(-40, 152));
            Third(feet, 0, "MANUAL", ShowManual);
            Third(feet, 1, "BACK", Back);
            Third(feet, 2, "MENU", ShowTitle);
        }

        static int _calRow;
        const int CalRows = 8;

        static void Third(RectTransform parent, int i, string label, System.Action act)
        {
            RectTransform slot = UiKit.Rect(parent, label, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                                            new Vector2(5, 0), new Vector2(-5, 0));
            UiKit.Bracketed(slot, label, label, act, 22);
        }

        /// <summary>A row of the calibrate panel: mark, name, what it does, and the control.</summary>
        static RectTransform CalRow(RectTransform panel, string icon, string label, string hint, bool wideHint)
        {
            float h = 1f / CalRows;
            int slot = _calRow++;
            RectTransform r = UiKit.Rect(panel, label, new Vector2(0, 1 - (slot + 1) * h), new Vector2(1, 1 - slot * h),
                                         new Vector2(0, 2), new Vector2(0, -2));

            UiKit.Icon(r, icon, Palette.Rust, 26, new Vector2(0, 0.5f), new Vector2(34, 0));
            UiKit.Label(r, "l", label, 21, Palette.Ink, TextAnchor.MiddleLeft,
                        new Vector2(0, string.IsNullOrEmpty(hint) ? 0 : 0.40f), new Vector2(0.62f, 1),
                        new Vector2(58, 0), Vector2.zero);
            if (!string.IsNullOrEmpty(hint))
                UiKit.Label(r, "h", hint, 14, Palette.Dim, TextAnchor.MiddleLeft,
                            new Vector2(0, 0), new Vector2(wideHint ? 0.72f : 0.30f, 0.40f),
                            new Vector2(58, 0), Vector2.zero);

            // a hairline under every row but the last, so eight rows read as one
            // instrument rather than eight unrelated controls
            if (slot < CalRows - 1)
                UiKit.Panel(r, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.20f),
                            new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 0), new Vector2(-20, 1));
            return r;
        }

        static void Toggle(RectTransform panel, string icon, string label, string hint,
                           System.Func<int> get, System.Action<int> set)
        {
            RectTransform r = CalRow(panel, icon, label, hint, true);
            UiKit.Switch(r, "sw", get() != 0, _ =>
            {
                int v = get() != 0 ? 0 : 1;
                set(v);
                Store.Save();
                return v != 0;
            }, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-108, -21), new Vector2(-18, 21));
        }

        static void Bar(RectTransform panel, string icon, string label, string hint,
                        int lo, int hi, System.Func<int> get, System.Action<int> set)
        {
            // The reading sits AFTER the hint rather than on top of it. Both were
            // pinned to the same left edge of the same band, so "120%" was printed
            // straight through the word INTENSITY.
            RectTransform r = CalRow(panel, icon, label, hint, false);
            Text val = UiKit.Label(r, "v", get() + "%", 16, Palette.Rust, TextAnchor.MiddleLeft,
                                   new Vector2(0.30f, 0), new Vector2(0.52f, 0.40f), Vector2.zero, Vector2.zero);
            UiKit.Bar(r, "bar", lo, hi, get(), v =>
            {
                set(v);
                Store.Save();
                val.text = v + "%";
            }, new Vector2(0.52f, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -18), new Vector2(-18, 18));
        }

        public static void ShowCalibrate() => Show("calibrate");

        // ---- boards ----------------------------------------------------------
        //
        // Every row reads its own local record, so the screen is worth opening on
        // a plane — the RANKING is the part that needs a host, not the number.

        static RectTransform _boardList;

        static void BuildBoards()
        {
            RectTransform L = Layer("boards");
            Solid(L);
            UiKit.Label(L, "h", "BOARDS", 40, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -90));
            _boardList = UiKit.Rect(L, "list", new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 200), new Vector2(-40, -180));

            RectTransform back = UiKit.Rect(L, "back", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(back, "back", "BACK", Back, 28);
        }

        public static void ShowBoards()
        {
            for (int i = _boardList.childCount - 1; i >= 0; i--) Object.Destroy(_boardList.GetChild(i).gameObject);

            var rows = new List<(string k, string v)>();

            // The daily first, because it is the one number shared with anybody else.
            int today = Daily.DayIndex();
            rows.Add(("DAILY " + Daily.DayLabel(),
                      Store.Data.dailySolved != 0 && Store.Data.dailyDay == today
                          ? Store.Data.dailyBest + " FOLDS" : "UNSOLVED"));
            rows.Add(("STREAK", Store.Data.streakCur + " · BEST " + Store.Data.streakBest));

            // THE WINDOWS. A missed day is not a zero, it is a MISS, and it costs
            // more than the worst honest attempt — otherwise skipping a hard cube
            // would be the optimal play. So these read as "lower is better", and
            // turning up and playing badly always beats not turning up.
            //
            // Only the days that have HAPPENED are counted: a running total that
            // charged for tomorrow would be all penalty and would tell you nothing.
            foreach (DailyBoards.Period p in DailyBoards.Periods)
            {
                var (total, played, of) = DailyBoards.Running(p, today);
                rows.Add((p.label, played == 0 ? "—" : total + "  (" + played + "/" + of + " DAYS)"));
            }

            // Then one row per vault: a vault is the unit a board ranks, scored on
            // the time to clear every cube in it, so a vault with a cube missing has
            // no total at all rather than a flattering partial one.
            for (int band = 0; band < Vaults.RankedVaults; band++)
            {
                int start = Vaults.VaultStart(band), size = Vaults.VaultSize(band);
                if (start > Store.Data.reached) break;

                long total = 0;
                int have = 0;
                for (int i = 0; i < size; i++)
                    if (Store.TryTimeBest(start + i, out long ms)) { total += ms; have++; }

                string v = have == size
                    ? (total / 1000f).ToString("0.0") + "s"
                    : have + "/" + size;
                rows.Add(("VAULT " + Vaults.RomanOf(band) + " · " + Vaults.VaultName(band), v));
            }

            float h = 1f / Mathf.Max(9, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                RectTransform r = UiKit.Rect(_boardList, "r" + i,
                    new Vector2(0, 1 - (i + 1) * h), new Vector2(1, 1 - i * h), new Vector2(0, 3), new Vector2(0, -3));
                UiKit.Label(r, "k", rows[i].k, 20, Palette.Dim, TextAnchor.MiddleLeft,
                            Vector2.zero, new Vector2(0.72f, 1), new Vector2(8, 0), Vector2.zero);
                UiKit.Label(r, "v", rows[i].v, 22, Palette.Ink, TextAnchor.MiddleRight,
                            new Vector2(0.72f, 0), Vector2.one, Vector2.zero, new Vector2(-8, 0));
            }

            Show("boards");
        }

        // ---- the win card ---------------------------------------------------

        static Text _winWhat, _winVerdict, _winFolds, _winPar, _winBest, _winVault;
        static Button _winNext, _winRetry;

        static void BuildWin()
        {
            RectTransform L = Layer("win");
            L.GetComponent<Image>().color = new Color(0, 0, 0, 0.9f);

            _winWhat = UiKit.Label(L, "what", "VAULT SOLVED", 22, Palette.Dim, TextAnchor.UpperCenter,
                                   new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -160), new Vector2(0, -120));
            _winVault = UiKit.Label(L, "vault", "", 20, Palette.Arc, TextAnchor.UpperCenter,
                                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -196), new Vector2(0, -164));
            _winVerdict = UiKit.Label(L, "verdict", "AT PAR", 48, Palette.Arc, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -280), new Vector2(0, -210));

            _winFolds = Stat(L, "FOLDS USED", -360);
            _winPar = Stat(L, "PAR", -420);
            _winBest = Stat(L, "YOUR BEST", -480);

            // A HIDDEN CONTROL MUST NOT LEAVE ITS HOLE BEHIND.
            //
            // These were fixed thirds of a column, and two of them come and go: NEXT
            // is meaningless after the daily, and RETRY FOR PAR only exists if you
            // went over. Switching one off left a third of the card empty — so a
            // PERFECT COLLAPSE, the best outcome in the game, produced the most
            // broken-looking card in it. They are now flowed over however many are
            // actually on, which is the only arrangement that cannot leave a gap.
            _winCol = Column(L, 560, 200);
            _winNext = Stacked("next", "NEXT", 30, true, () =>
            {
                Show(null);
                _dir.Play(_dir.S.levelNo + 1);
            });
            _winRetry = Stacked("retry", "RETRY FOR PAR", 26, false, () =>
            {
                Show(null);
                _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey);
            });
            _winVaults = Stacked("vaults", "VAULTS", 26, false, OpenVaults);
        }

        static RectTransform _winCol;
        static Button _winVaults;

        static Button Stacked(string name, string label, int size, bool primary, System.Action act)
        {
            RectTransform r = UiKit.Rect(_winCol, name, Vector2.zero, Vector2.one, new Vector2(0, 8), new Vector2(0, -8));
            return UiKit.Bracketed(r, name, label, act, size, primary);
        }

        static void FlowWin()
        {
            Button[] all = { _winNext, _winRetry, _winVaults };
            int on = 0;
            foreach (Button b in all) if (b.transform.parent.gameObject.activeSelf) on++;

            float h = 1f / Mathf.Max(1, on);
            int slot = 0;
            foreach (Button b in all)
            {
                var rt = (RectTransform)b.transform.parent;
                if (!rt.gameObject.activeSelf) continue;
                rt.anchorMin = new Vector2(0, 1f - (slot + 1) * h);
                rt.anchorMax = new Vector2(1, 1f - slot * h);
                slot++;
            }
        }

        static Text Stat(RectTransform L, string key, float y)
        {
            UiKit.Label(L, key, key, 20, Palette.Dim, TextAnchor.MiddleLeft,
                        new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(70, y - 26), new Vector2(0, y));
            return UiKit.Label(L, key + "_v", "0", 26, Palette.Ink, TextAnchor.MiddleRight,
                               new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, y - 26), new Vector2(-70, y));
        }

        public static void ShowWin(Session s, GameDirector dir)
        {
            _winWhat.text = s.IsMade ? "CUBE SOLVED" : s.IsDaily ? "DAILY SOLVED" : "VAULT SOLVED";

            _winFolds.text = s.turns.ToString();
            _winPar.text = s.lv.par.ToString();
            _winBest.text = s.IsDaily ? Store.Data.dailyBest.ToString()
                          : Store.TryBest(s.levelNo, out int b) ? b.ToString() : s.turns.ToString();

            _winVerdict.text = s.turns < s.lv.par ? "BELOW PAR"
                             : s.turns == s.lv.par ? "PERFECT COLLAPSE"
                             : (s.turns - s.lv.par) + " OVER PAR";
            _winVerdict.color = s.turns <= s.lv.par ? Palette.Arc : Palette.Lock;

            // A vault boundary is worth marking — it is the only structure an
            // endless game has, and it is where the demand steps up.
            bool crossing = !s.IsDaily && !s.IsMade && Vaults.VaultOf(s.levelNo + 1) != Vaults.VaultOf(s.levelNo);
            _winVault.text = s.IsDaily ? "DAILY " + Daily.DayLabel()
                           : crossing ? "VAULT " + Vaults.RomanOf(Vaults.VaultOf(s.levelNo + 1)) + " / "
                                        + Vaults.VaultName(Vaults.VaultOf(s.levelNo + 1))
                           : "";

            // there is no next cube after the daily — there is tomorrow
            _winNext.transform.parent.gameObject.SetActive(!s.IsDaily && !s.IsMade);
            _winRetry.transform.parent.gameObject.SetActive(s.turns > s.lv.par);
            FlowWin();

            Show("win");
        }
    }
}
