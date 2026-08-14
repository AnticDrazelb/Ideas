using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// Every screen that is not the board, composed in the shipped build's own
    /// order.
    ///
    /// Each of these is one `.layer` in index.html and each is built here as the
    /// same column of the same children with the same margins between them, taken
    /// from the markup rather than from a screenshot. Where a screen reads
    /// differently from the original, the difference should be findable as a
    /// different number in this file and nowhere else.
    ///
    /// One canvas, one layer at a time, and a stack so BACK always goes up one
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

        /// <summary>
        /// `.layer` — fixed to the whole screen, black, and fading rather than
        /// snapping. Every screen in the shipped build is opaque `var(--void)`:
        /// there is no translucent card anywhere in it, including the pause screen,
        /// because brickwork behind a paragraph is contrast the reader has to
        /// fight. The one exception is the title, which is a stage.
        /// </summary>
        internal static RectTransform Layer(string id)
        {
            RectTransform rt = UiKit.Fill(_canvas.transform, id);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = Palette.Void;
            // Every layer fades in, so the group it fades with is part of what a
            // layer is rather than something the transition goes looking for.
            rt.gameObject.AddComponent<CanvasGroup>();
            Layers[id] = rt.gameObject;
            return rt;
        }

        static void HideAll() { foreach (var kv in Layers) kv.Value.SetActive(false); }

        public static void Show(string id)
        {
            HideAll();
            if (id != null)
            {
                GameObject go = Layers[id];
                go.SetActive(true);
                if (Stack.Count == 0 || Stack[Stack.Count - 1] != id) Stack.Add(id);
                _dir.StartCoroutine(Arrive(go));
            }
            else Stack.Clear();
            _dir.SetScreenUp(id != null);
        }

        /// <summary>
        /// `.layer{ transition:opacity .12s linear }`, and the boot animation the
        /// children run under it: each child fades in on a four-step ease with a
        /// stagger, so the screen assembles rather than appears. Short enough that
        /// nobody waiting to press a button ever waits.
        ///
        /// On the UNSCALED clock, because menus are opened during hitstop and a
        /// card that eases in at a third of speed reads as the game hanging.
        /// </summary>
        static System.Collections.IEnumerator Arrive(GameObject go)
        {
            CanvasGroup cg = UiKit.Ensure<CanvasGroup>(go);
            const float Dur = 0.12f;
            float t = 0f;
            while (t < Dur)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / Dur);
                yield return null;
            }
            cg.alpha = 1f;
        }

        public static void Back()
        {
            if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
            Show(Stack.Count > 0 ? Stack[Stack.Count - 1] : null);
        }

        // ---- the sheet's own type rules -------------------------------------

        /// <summary>h1 — t-3xl, .26em, margin 0 0 6.</summary>
        static Text H1(Flow col, string name, string s, Color c)
        {
            RectTransform r = UiKit.Slot(col, name, Css.T3Xl * 1.15f, 0f, 6f);
            return UiKit.Type(r, name, s, Css.T3Xl, c, TextAnchor.MiddleCenter, 0.26f);
        }

        /// <summary>h2 — t-xl, .18em, margin 0 0 4.</summary>
        static Text H2(Flow col, string name, string s)
        {
            RectTransform r = UiKit.Slot(col, name, Css.TXl * 1.4f, 0f, 4f);
            return UiKit.Type(r, name, s, Css.TXl, Palette.Ink, TextAnchor.MiddleCenter, 0.18f);
        }

        /// <summary>
        /// .sub — t-micro, dim, line-height 1.6, .10em, max-width 40ch, margin 0 0 12.
        /// `ch` is the width of a zero, which in a fixed-pitch face is 0.6em and is
        /// the same for every glyph — the one place a monospace interface makes a
        /// CSS unit trivial to port.
        /// </summary>
        static Text Sub(Flow col, string name, string s, bool tight = false)
        {
            RectTransform r = UiKit.Rect(((MonoBehaviour)col).transform, name,
                                         Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Text t = UiKit.Para(r, name, s, Css.TMicro, Palette.Dim, TextAnchor.UpperCenter, 0.10f);
            col.AddText(r, t.text, Css.TMicro, 0.10f, Css.TMicro * 0.6f * 40f, tight ? 16f : 12f, 1.6f);
            return t;
        }

        /// <summary>.eyebrow — t-tiny, .24em, rust.</summary>
        static Text Eyebrow(Flow col, string name, string s, Color? c = null, float mb = 0f)
        {
            RectTransform r = UiKit.Slot(col, name, Css.TTiny * 1.5f, 0f, mb);
            return UiKit.Type(r, name, s, Css.TTiny, c ?? Palette.Rust, TextAnchor.MiddleCenter, 0.24f);
        }

        /// <summary>.foot — t-micro, dim-2, .16em, margin-top 16.</summary>
        static Text Foot(Flow col, string s)
        {
            RectTransform r = UiKit.Slot(col, "foot", Css.TMicro * 1.6f, 0f, 0f);
            return UiKit.Type(r, "foot", s, Css.TMicro, Palette.Dim2, TextAnchor.MiddleCenter, 0.16f);
        }

        /// <summary>A full-width `.btn` on its own row.</summary>
        static Button Row(Flow col, string label, System.Action act, UiKit.Kind kind = UiKit.Kind.Btn)
        {
            RectTransform r = UiKit.Slot(col, label, UiKit.BtnHeight(kind), Css.BtnMax, UiKit.BtnMargin(kind));
            return UiKit.Btn(r, label, act, kind);
        }

        /// <summary>
        /// `.btn2` — two or three buttons sharing a row, gap 10, margin-bottom 11.
        /// The buttons inside lose their own margin and their max-width and take a
        /// smaller step of type, which is the whole of the rule.
        /// </summary>
        static void Btn2(Flow col, string name, bool slim, bool three,
                         (string label, System.Action act, UiKit.Kind kind)[] items)
        {
            float h = slim ? Css.HCtl : Css.HBtn + 6f;
            float size = slim ? Css.TFine : three ? Css.TFine : Css.TSm;
            float track = slim ? 0.16f : three ? 0.08f : 0.12f;

            RectTransform row = UiKit.Slot(col, name, h, Css.BtnMax, slim ? 0f : 11f);

            int n = items.Length;
            const float Gap = 10f;
            for (int i = 0; i < n; i++)
            {
                // flex:1 with a gap between, expressed as a share of the row less
                // the gaps that come before and after this child
                float x0 = i / (float)n, x1 = (i + 1) / (float)n;
                RectTransform slot = UiKit.Rect(row, items[i].label,
                    new Vector2(x0, 0f), new Vector2(x1, 1f),
                    new Vector2(i == 0 ? 0f : Gap * i / n, 0f),
                    new Vector2(i == n - 1 ? 0f : -Gap * (n - 1 - i) / n, 0f));
                UiKit.Btn(slot, items[i].label, items[i].act, items[i].kind, size, track);
            }
        }

        /// <summary>.edMsg — the line a screen answers on. t-micro, centred, 2.6em tall.</summary>
        static Text EdMsg(Flow col, string name)
        {
            RectTransform r = UiKit.Slot(col, name, Css.TMicro * 2.6f, Css.ManualMax, 10f);
            return UiKit.Type(r, name, "", Css.TMicro, Palette.Dim, TextAnchor.UpperCenter, 0.09f);
        }

        // ---- title ----------------------------------------------------------

        static Text _playLabel, _resumeName, _resumePar, _resumeVault;

        static void BuildTitle()
        {
            RectTransform L = Layer("title");
            // #scTitle is the one layer that is not flat black: a gradient that
            // darkens only where words have to sit on it, so the cube turning
            // behind it is never tinted.
            UiKit.Stage(L);

            Flow col = UiKit.Column(L);

            // .titleTop -------------------------------------------------------
            //
            // #wordmark is one h1 of two lines, the second in rust. It is a column
            // rather than two headings so the 1.1 line-height closes them up.
            float wm = Css.VpTiny ? Css.T2Xl : Mathf.Clamp(Css.Vw(6.2f), 20f, 29f);
            RectTransform mark = UiKit.Slot(col, "wordmark", wm * 1.1f * 2f, Mathf.Min(420f, Css.Vw(88f)), 6f);
            UiKit.Label(mark, "w1", "SINGULARITY", wm, Palette.Ink, TextAnchor.UpperCenter, 0.26f,
                        new Vector2(0f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UiKit.Label(mark, "w2", "ENGINE", wm, Palette.Rust, TextAnchor.UpperCenter, 0.26f,
                        new Vector2(0f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

            // WHERE YOU LEFT OFF, ON THE SCREEN THAT OFFERS TO TAKE YOU BACK.
            // [ CONTINUE ] used to be a promise with no subject: it did not say
            // which cube, which vault, or what it would cost. Naming the thing a
            // button is about is the cheapest confidence a menu can buy.
            RectTransform resume = UiKit.Slot(col, "resume", Css.TMicro * 1.4f + Css.TMd * 1.5f, 0f, 0f);
            _resumeVault = UiKit.Label(resume, "resumeVault", "", Css.TMicro, Palette.Dim,
                                       TextAnchor.UpperCenter, 0.24f,
                                       new Vector2(0f, 1f), new Vector2(1f, 1f),
                                       new Vector2(0f, -Css.TMicro * 1.4f), Vector2.zero);
            RectTransform nameRow = UiKit.Rect(resume, "resumeName", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                               Vector2.zero, new Vector2(0f, -Css.TMicro * 1.4f - 2f));
            _resumeName = UiKit.Type(nameRow, "n", "", Css.TMd, Palette.Ink, TextAnchor.MiddleCenter, 0.16f);
            _resumePar = _resumeName;

            // .mastRule — one pixel of rust, min(88vw,420px) wide, 11 above and 12
            // below. It is the only rule on the screen and it closes the masthead,
            // so the pitch under it reads as a caption on the game rather than a
            // fourth line of the title.
            RectTransform rule = UiKit.Slot(col, "mastRule", 1f, Mathf.Min(420f, Css.Vw(88f)), 12f);
            rule.anchoredPosition += new Vector2(0f, -11f);
            UiKit.Panel(rule, "r", Palette.Rust, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // html.vpShort drops the pitch entirely rather than shrinking it
            if (!Css.VpShort)
                Sub(col, "sub", "You are a black hole inside a broken machine. " +
                                "Fold the engine until its circuits align, then collapse into the core.");

            // .heroSlot — a DECLARED height rather than whatever is left over. The
            // cube used to be given the whole gap between the two bands, which on a
            // tall phone is five hundred pixels for an object that can only be three
            // hundred wide, so the screen read as three things that had drifted
            // apart. It gets a slot, the column packs to the centre, and the
            // composition closes up.
            col.Space(Css.VpShort ? 132f : 104f);

            // .titleBottom ----------------------------------------------------
            RectTransform play = UiKit.Slot(col, "play", UiKit.BtnHeight(UiKit.Kind.Primary), Css.BtnMax, 16f);
            Button playBtn = UiKit.Btn(play, "CONTINUE",
                () => { Show(null); _dir.Play(Mathf.Max(1, Store.Data.reached)); }, UiKit.Kind.Primary);
            _playLabel = playBtn.GetComponentInChildren<Text>();

            Btn2(col, "row1", false, false, new[]
            {
                ("DAILY", (System.Action)(() => { Show(null); _dir.Play(Daily.SpecLevel, LoadKind.Daily); }), UiKit.Kind.Btn),
                ("VAULTS", (System.Action)OpenVaults, UiKit.Kind.Btn),
            });

            // FORGE SITS ON THE TITLE AND COSTS NOTHING TO PUT THERE. Pairing it
            // with CALIBRATE turns one full-width row into one two-up row, so the
            // column is exactly as tall as it was and the hero cube does not move.
            Btn2(col, "row2", true, false, new[]
            {
                ("FORGE", (System.Action)ForgeScreens.OpenShelf, UiKit.Kind.Btn),
                ("CALIBRATE", (System.Action)ShowCalibrate, UiKit.Kind.Btn),
            });

            RectTransform footSlot = UiKit.Slot(col, "footpad", 16f, 0f, 0f);
            Foot(col, "DIAGNOSTIC BUILD · NO DEAD PIXELS");
        }

        public static void ShowTitle()
        {
            int r = Mathf.Max(1, Store.Data.reached);
            // A machine that has never been started is INITIALISED, not continued.
            if (_playLabel != null) _playLabel.text = r <= 1 ? "[ INITIALISE ]" : "[ CONTINUE ]";

            int band = Vaults.VaultOf(r);
            if (_resumeVault != null)
                _resumeVault.text = ("VAULT " + Vaults.RomanOf(band) + ": " + Vaults.VaultName(band)).ToUpperInvariant();
            if (_resumeName != null)
            {
                // the cube's own name and what it costs, so the button has a subject
                int par = r <= Baked.Levels.Length ? Baked.Levels[r - 1].par : -1;
                _resumeName.text = (Vaults.LevelName(r) + (par >= 0 ? "  /  PAR " + par : "")).ToUpperInvariant();
            }
            Stack.Clear();
            Show("title");
            _dir.ShowAttract();
        }

        // ---- the vault shelf ------------------------------------------------

        static Text _vaultName, _jumpMsg;
        static RectTransform _grid, _vaultSwatch;
        static InputField _jumpField;
        static Button _vaultPrev, _vaultNext;

        static void BuildVaults()
        {
            RectTransform L = Layer("vaults");
            Flow col = UiKit.Column(L);

            // .vaultBar — a stepper either side of the vault's name and its
            // progress rail. The swatch used to be a two-tone chip of the vault's
            // palette; there is one palette now, so it says how far through the
            // vault you are instead.
            RectTransform bar = UiKit.Slot(col, "vaultBar", Css.HCtl, Css.GridMax, 16f);
            RectTransform prev = UiKit.Rect(bar, "prev", new Vector2(0f, 0f), new Vector2(0f, 1f),
                                            Vector2.zero, new Vector2(Css.HCtl, 0f));
            _vaultPrev = UiKit.PillBtn(prev, "[ < ]", () => { _viewBand = Mathf.Max(0, _viewBand - 1); PaintVaults(); });
            RectTransform next = UiKit.Rect(bar, "next", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                            new Vector2(-Css.HCtl, 0f), Vector2.zero);
            _vaultNext = UiKit.PillBtn(next, "[ > ]", () => { _viewBand++; PaintVaults(); });

            RectTransform id = UiKit.Rect(bar, "vaultId", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                          new Vector2(Css.HCtl + 10f, 0f), new Vector2(-Css.HCtl - 10f, 0f));
            _vaultSwatch = UiKit.Rect(id, "swatch", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                      Vector2.zero, Vector2.zero);
            _vaultSwatch.sizeDelta = new Vector2(Mathf.Min(180f, Css.Vw(52f)), 3f);
            _vaultName = UiKit.Label(id, "vaultName", "VAULT I", Css.TTiny, Palette.Dim,
                                     TextAnchor.UpperCenter, 0.18f,
                                     new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -8f));

            // .grid — repeat(auto-fill, minmax(96px,1fr)), gap 11, and the rows are
            // made square by the script once it knows how wide a cell came out.
            _grid = UiKit.Slot(col, "grid", 0f, Css.GridMax, 18f);

            // THE SEED BOX. Every generated cube in this game is a pure function of
            // its number, so the NUMBER IS THE SEED — a few characters that cut the
            // same puzzle on every device, forever. There has to be a way to type
            // one, and typing one is a seed viewer rather than a shortcut: a jump
            // past `reached` records nothing, unlocks nothing and posts nothing.
            RectTransform jump = UiKit.Slot(col, "edRow", Css.HCtl, Css.ManualMax, 10f);
            RectTransform jf = UiKit.Rect(jump, "jumpField", Vector2.zero, new Vector2(1f, 1f),
                                          Vector2.zero, new Vector2(-Css.HCtl * 1.6f - 8f, 0f));
            _jumpField = UiKit.Field(jf, "CUBE NUMBER");
            _jumpField.contentType = InputField.ContentType.IntegerNumber;
            RectTransform goSlot = UiKit.Rect(jump, "go", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                              new Vector2(-Css.HCtl * 1.6f, 0f), Vector2.zero);
            UiKit.PillBtn(goSlot, "[ GO ]", Jump);

            _jumpMsg = EdMsg(col, "jumpMsg");

            Row(col, "BACK", Back, UiKit.Kind.Ghost);
        }

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

        /// <summary>
        /// A CUBE IS A CARD, AND THE CARDS ARE SQUARE.
        ///
        /// `.grid` is `repeat(auto-fill, minmax(96px,1fr))` with an 11px gap, and
        /// the script sets `gridAutoRows` to whatever width a cell came out so the
        /// tiles are square. Three fixed columns is that layout at one width; on a
        /// wide phone the shipped build fits four.
        /// </summary>
        static void PaintVaults()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Object.Destroy(_grid.GetChild(i).gameObject);

            _vaultName.text = ("VAULT " + Vaults.RomanOf(_viewBand) + " / " + Vaults.VaultName(_viewBand))
                              .ToUpperInvariant();

            int start = Vaults.VaultStart(_viewBand);
            int size = Vaults.VaultSize(_viewBand);

            int cleared = 0;
            for (int q = 0; q < size; q++) if (Store.TryBest(start + q, out int _)) cleared++;
            UiKit.Rail(_vaultSwatch, Palette.Arc, size > 0 ? cleared / (float)size : 0f);

            UiKit.Disable(_vaultPrev, _viewBand == 0);
            UiKit.Disable(_vaultNext, Vaults.VaultStart(_viewBand + 1) > Store.Data.reached);

            const float Gap = 11f, Min = 96f;
            float avail = Mathf.Min(Css.GridMax, Css.VpW - Css.PadX * 2f);
            int cols = Mathf.Max(1, Mathf.FloorToInt((avail + Gap) / (Min + Gap)));
            float cell = (avail - Gap * (cols - 1)) / cols;
            int rows = Mathf.CeilToInt(size / (float)cols);

            _grid.sizeDelta = new Vector2(_grid.sizeDelta.x, rows * cell + (rows - 1) * Gap);

            for (int i = 0; i < size; i++)
            {
                int level = start + i;
                int cx = i % cols, cy = i / cols;
                RectTransform slot = UiKit.Rect(_grid, "c" + level,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                slot.pivot = new Vector2(0f, 1f);
                slot.sizeDelta = new Vector2(cell, cell);
                slot.anchoredPosition = new Vector2(cx * (cell + Gap), -cy * (cell + Gap));

                bool locked = level > Store.Data.reached;
                bool done = Store.TryBest(level, out int best);
                bool ranked = Store.TryPar(level, out int par) && done;

                // THREE MARKS, AND THEY ARE THE ONLY SCORE THIS SCREEN SHOWS. A raw
                // fold count means nothing without the par beside it; "how close to
                // perfect" is what a player wants off a grid, and it survives being
                // six pixels tall. A cube cleared before this build recorded pars
                // has a best and no par to judge it by — it gets one mark rather
                // than a guess, because inventing a rating is worse than showing
                // less.
                int marks = !done ? 0 : !ranked ? 1 : best <= par ? 3 : best <= par + 2 ? 2 : 1;

                Cube(slot, level, locked, done, marks);
            }
        }

        /// <summary>
        /// .cube — a card with its number, its name and its marks. `.done` lights
        /// the edge and the number in arc; `.perfect` fills the card at ten percent
        /// of it; `.locked` drops the whole thing to a third.
        /// </summary>
        static void Cube(RectTransform slot, int level, bool locked, bool done, int marks)
        {
            var plate = slot.gameObject.AddComponent<Image>();
            plate.sprite = UiKit.Frame(Css.RCard, 0f);
            plate.type = Image.Type.Sliced;
            plate.color = marks == 3
                ? new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, 0.10f)
                : Palette.Panel;

            Image edge = UiKit.Edge(slot, done
                ? new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, 0.55f)
                : Css.Edge2, Css.RCard, Css.Hair);

            // .cube .idx — t-2xl, dim, and arc once it is done
            UiKit.Label(slot, "idx", level.ToString(), Css.T2Xl, done ? Palette.Arc : Palette.Dim,
                        TextAnchor.LowerCenter, 0f,
                        new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -10f));

            // .cube .nm — t-micro, dim-2, and an em dash while it is locked
            UiKit.Label(slot, "nm", locked ? "—" : Vaults.LevelName(level), Css.TMicro, Palette.Dim2,
                        TextAnchor.UpperCenter, 0.13f,
                        new Vector2(0f, 0.2f), new Vector2(1f, 0.45f), new Vector2(6f, 0f), new Vector2(-6f, 5f));

            // .cube .marks i — six by six, square, hollow until it is earned
            RectTransform row = UiKit.Rect(slot, "marks", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                           Vector2.zero, Vector2.zero);
            row.sizeDelta = new Vector2(6f * 3f + 3f * 2f, 6f);
            row.anchoredPosition = new Vector2(0f, 14f);
            for (int m = 0; m < 3; m++)
            {
                RectTransform pip = UiKit.Rect(row, "m" + m, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                               Vector2.zero, Vector2.zero);
                pip.sizeDelta = new Vector2(6f, 6f);
                pip.anchoredPosition = new Vector2(m * 9f + 3f, 0f);
                if (m < marks)
                {
                    var on = pip.gameObject.AddComponent<Image>();
                    on.color = Palette.Arc;
                    on.raycastTarget = false;
                }
                else
                {
                    var off = pip.gameObject.AddComponent<Image>();
                    off.sprite = UiKit.Frame(0.5f, Css.Hair);
                    off.type = Image.Type.Sliced;
                    off.color = Palette.Dim2;
                    off.raycastTarget = false;
                }
            }

            int lv = level;
            bool lk = locked;
            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = plate;
            var colors = btn.colors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
            colors.fadeDuration = 0f;
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                if (lk) { _dir.Hud.Toast("SOLVE THE VAULTS BEFORE IT"); return; }
                Show(null);
                _dir.Play(lv, LoadKind.Vault);
            });

            var press = slot.gameObject.AddComponent<UiKit.Press>();
            press.plate = plate;
            press.edge = edge;
            press.plateUp = plate.color;
            press.plateDown = Palette.Rust;
            press.edgeUp = edge.color;
            press.edgeDown = Palette.Rust;

            // .cube.locked{ opacity:.34 }
            if (locked)
            {
                var cg = slot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.34f;
            }
        }

        // ---- the manual -----------------------------------------------------

        static void BuildManual()
        {
            RectTransform L = Layer("manual");
            // A page that scrolls is pinned to the top rather than centred, and
            // GOT IT is nailed to the bottom of the screen where a thumb already is
            // instead of at the end of the scroll.
            Flow col = UiKit.Column(L, false);

            H2(col, "h", "CALIBRATION");
            RectTransform pad = UiKit.Slot(col, "pad", 14f, 0f, 0f);

            Kicker(col, "THE RULE", true);
            MRow(col, "ONE FACE AT A TIME",
                 "Two traces that line up when you fold are connected, however far apart they look.",
                 Art.Dia("fold", Art.DiaFold));

            Kicker(col, "THE VERBS");
            MRow(col, "SWIPE TO FOLD",
                 "Past halfway it commits. The ticks say which folds have footing.",
                 Art.Dia("swipe", Art.DiaSwipe));
            MRow(col, "TAP TO MOVE",
                 "The singularity walks to any trace connected to the one under it.",
                 Art.Dia("tap", Art.DiaTap));
            MRow(col, "COLLAPSE INTO THE CORE", "Reach it and the vault is solved.",
                 Art.Dia("core", Art.DiaCore));

            Kicker(col, "THE OBJECTS");
            Objs(col);

            Kicker(col, "WORTH KNOWING");
            MRow(col, "HOLD FOR MATRIX",
                 "The lattice goes to glass. Keep holding and drag to turn it.",
                 Art.Dia("matrix", Art.DiaMatrix));
            MRow(col, "TO GO", "The fewest folds still possible from where you stand.",
                 Art.Dia("togo", Art.DiaToGo));

            Stick(L, "GOT IT", Back);
        }

        /// <summary>.meyebrow — t-tiny, .24em, rust, margin 14 above and 2 below.</summary>
        static void Kicker(Flow col, string s, bool first = false)
        {
            if (!first) col.Space(14f);
            RectTransform r = UiKit.Slot(col, s, Css.TTiny * 1.4f, Css.ManualMax, 2f);
            UiKit.Label(r, s, s, Css.TTiny, Palette.Rust, TextAnchor.MiddleLeft, 0.24f,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>
        /// .mrow — the text on the left and the drawing on the right, centred
        /// against each other with a 14 gap, and a hairline between one row and the
        /// next. The picture is 96 by 56 and does not shrink; the words take what
        /// is left.
        /// </summary>
        static void MRow(Flow col, string head, string body, Sprite dia)
        {
            const float DiaW = 96f, DiaH = 56f, Gap = 14f;

            RectTransform r = UiKit.Rect(((MonoBehaviour)col).transform, head,
                                         Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

            RectTransform txt = UiKit.Rect(r, "mtxt", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                           new Vector2(0f, 9f), new Vector2(-(DiaW + Gap), -9f));

            // .mtxt b — a block, ink, t-md, .07em, 3 under it
            UiKit.Label(txt, "b", head, Css.TMd, Palette.Ink, TextAnchor.UpperLeft, 0.07f,
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Css.TMd * 1.2f), Vector2.zero);
            Text line = UiKit.Para(txt, "t", body, Css.TSm, Palette.Dim, TextAnchor.UpperLeft, 0.1f);
            var lr = (RectTransform)line.transform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMax = new Vector2(0f, -(Css.TMd * 1.2f + 3f));

            UiKit.Mark(r, "dia", dia, Color.white, DiaW, DiaH, new Vector2(1f, 0.5f),
                       new Vector2(-DiaW * 0.5f, 0f));

            // the row is as tall as the picture or the words, whichever is more
            float textW = Mathf.Min(Css.ManualMax, Css.VpW - Css.PadX * 2f) - DiaW - Gap;
            float words = Css.TMd * 1.2f + 3f
                        + Flow.TextHeight(body.ToUpperInvariant(), textW, Css.TSm, 0.1f, 1.4f) + 18f;
            col.Add(r, Mathf.Max(DiaH + 18f, words), Css.ManualMax, 0f);
            Hairline(r);
        }

        static void Hairline(RectTransform r)
            => UiKit.Panel(r, "rule", Css.Edge2, new Vector2(0f, 1f), new Vector2(1f, 1f),
                           new Vector2(0f, -Css.Hair), Vector2.zero);

        /// <summary>
        /// .objs — four tiles, because these four are a SET and a list of four rows
        /// does not say so.
        /// </summary>
        static void Objs(Flow col)
        {
            const float Gap = 8f, IcSize = 30f;
            float h = 11f + IcSize + 5f + Css.TMicro * 1.3f + 5f + 8f * 1.3f + 9f;
            RectTransform row = UiKit.Slot(col, "objs", h, Css.ManualMax, 2f);

            (string key, string art, Color col, string name, string job)[] items =
            {
                ("trace", Art.ObjTrace, Palette.Trace, "TRACE", "CIRCUIT"),
                ("node", Art.ObjNode, Palette.Node, "NODE", "COLLECT"),
                ("lock", Art.ObjLock, Palette.Lock, "LOCK", "BARRIER"),
                ("plate", Art.ObjPlate, Palette.Rust, "PLATE", "INVERTS"),
            };

            for (int i = 0; i < 4; i++)
            {
                RectTransform t = UiKit.Rect(row, items[i].name,
                    new Vector2(i / 4f, 0f), new Vector2((i + 1) / 4f, 1f),
                    new Vector2(i == 0 ? 0f : Gap * i / 4f, 0f),
                    new Vector2(i == 3 ? 0f : -Gap * (3 - i) / 4f, 0f));

                var plate = t.gameObject.AddComponent<Image>();
                plate.sprite = UiKit.Frame(Css.RChip, 0f);
                plate.type = Image.Type.Sliced;
                plate.color = Palette.Panel;
                plate.raycastTarget = false;
                UiKit.Edge(t, Css.Edge2, Css.RChip, Css.Hair);

                UiKit.Mark(t, "ic", Art.Obj(items[i].key, items[i].art, items[i].col), Color.white,
                           IcSize, IcSize, new Vector2(0.5f, 1f), new Vector2(0f, -(11f + IcSize * 0.5f)));
                UiKit.Label(t, "b", items[i].name, Css.TMicro, Palette.Ink, TextAnchor.UpperCenter, 0.1f,
                            new Vector2(0f, 0f), new Vector2(1f, 0f),
                            new Vector2(0f, 9f + 8f * 1.3f + 5f), new Vector2(0f, 9f + 8f * 1.3f + 5f + Css.TMicro * 1.3f));
                UiKit.Label(t, "s", items[i].job, 8f, Palette.Dim2, TextAnchor.UpperCenter, 0.12f,
                            new Vector2(0f, 0f), new Vector2(1f, 0f),
                            new Vector2(0f, 9f), new Vector2(0f, 9f + 8f * 1.3f));
            }
        }

        /// <summary>
        /// .stick — the action nailed to the bottom of a page that scrolls, over a
        /// gradient so the last line fades out under it rather than being cut off
        /// by it.
        /// </summary>
        static void Stick(RectTransform L, string label, System.Action act)
        {
            float h = UiKit.BtnHeight(UiKit.Kind.Primary) + 44f;
            RectTransform bar = UiKit.Rect(L, "stick", new Vector2(0f, 0f), new Vector2(1f, 0f),
                                           Vector2.zero, new Vector2(0f, h));
            UiKit.StickFade(bar);
            RectTransform slot = UiKit.Rect(bar, "go", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                            Vector2.zero, Vector2.zero);
            slot.sizeDelta = new Vector2(Mathf.Min(Css.BtnMax, Css.VpW - Css.PadX * 2f),
                                         UiKit.BtnHeight(UiKit.Kind.Primary));
            slot.pivot = new Vector2(0.5f, 0f);
            slot.anchoredPosition = new Vector2(0f, 22f);
            UiKit.Btn(slot, label, act, UiKit.Kind.Primary);
        }

        public static void ShowManual() => Show("manual");

        // ---- the plate, taught once -----------------------------------------
        //
        // Shown the first time a cube carries one, and never again. A mechanic this
        // large should not arrive as a toast, and it should not arrive as a manual
        // page either — it arrives when the thing is on screen, with the thing
        // itself waiting behind the card.

        static void BuildPlateTeach()
        {
            RectTransform L = Layer("plate");
            Flow col = UiKit.Column(L, false);

            Eyebrow(col, "eyebrow", "A NEW COMPONENT", Palette.Rust, 8f);
            H2(col, "h", "PLATES");
            Sub(col, "sub", "Everything you know how to do is about to be worth less.", true);

            (string key, string head, string body)[] rows =
            {
                ("⊘", "THE LATTICE INVERTS",
                 "Every trace goes dead. Every dead cell lights up. Nothing moves — only what carries current."),
                ("⏱", "IT LASTS FIVE SECONDS",
                 "Then the lattice springs back and puts you on the nearest plate. " +
                 "Running out costs you the walk, never the vault. Plan the line before you press it."),
                ("⟲", "TAP IT AGAIN TO PRESS IT AGAIN",
                 "A plate is a door between two versions of the same engine, and the core is usually " +
                 "only reachable in one of them. You can always go back through."),
                ("◎", "IT ALWAYS GIVES FOOTING",
                 "Cut clean through the lattice, visible from every face. While you stand on a plate " +
                 "every fold is legal — and the clock cannot throw you off one."),
            };

            foreach (var r in rows) MKeyRow(col, r.key, r.head, r.body);

            Stick(L, "STEP ON IT", () => Show(null));
        }

        /// <summary>
        /// A .mrow whose left-hand mark is a glyph rather than a drawing — .mkey is
        /// 26 wide, t-xl, and rust on this screen.
        /// </summary>
        static void MKeyRow(Flow col, string key, string head, string body)
        {
            const float KeyW = 26f, Gap = 14f;
            RectTransform r = UiKit.Rect(((MonoBehaviour)col).transform, head,
                                         Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

            UiKit.Label(r, "mkey", key, Css.TXl, Palette.Rust, TextAnchor.MiddleCenter, 0f,
                        new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(KeyW, 0f));

            RectTransform txt = UiKit.Rect(r, "mtxt", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                           new Vector2(KeyW + Gap, 9f), new Vector2(0f, -9f));
            UiKit.Label(txt, "b", head, Css.TMd, Palette.Ink, TextAnchor.UpperLeft, 0.07f,
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Css.TMd * 1.2f), Vector2.zero);
            Text line = UiKit.Para(txt, "t", body, Css.TSm, Palette.Dim, TextAnchor.UpperLeft, 0.1f);
            var lr = (RectTransform)line.transform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMax = new Vector2(0f, -(Css.TMd * 1.2f + 3f));

            float textW = Mathf.Min(Css.ManualMax, Css.VpW - Css.PadX * 2f) - KeyW - Gap;
            col.Add(r, Css.TMd * 1.2f + 3f
                     + Flow.TextHeight(body.ToUpperInvariant(), textW, Css.TSm, 0.1f, 1.4f) + 18f,
                    Css.ManualMax, 0f);
            Hairline(r);
        }

        public static void ShowPlateTeach() => Show("plate");

        // ---- pause ----------------------------------------------------------

        static Text _pauseName, _pauseSub;

        static void BuildPause()
        {
            RectTransform L = Layer("pause");
            Flow col = UiKit.Column(L);

            // THE CARD IS ABOUT THE CUBE YOU ARE IN, NOT ABOUT BEING PAUSED.
            // "PAUSED" over six identical bars tells you one thing you already know
            // and then asks you to read six labels to find the one that says go
            // back. Naming the cube and its vault costs nothing and turns the card
            // into a place.
            _pauseName = H2(col, "pauseName", "PAUSED");
            _pauseSub = Sub(col, "pauseSub", "—", true);

            Row(col, "RESUME", () => Show(null), UiKit.Kind.Primary);

            Btn2(col, "three", false, true, new[]
            {
                ("RESET", (System.Action)(() => { Show(null); _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey); }), UiKit.Kind.Btn),
                ("VAULTS", (System.Action)OpenVaults, UiKit.Kind.Btn),
                ("MENU", (System.Action)ShowTitle, UiKit.Kind.Btn),
            });

            Btn2(col, "outs", false, false, new[]
            {
                ("BOARDS", (System.Action)ShowBoards, UiKit.Kind.Ghost),
                ("CALIBRATE", (System.Action)ShowCalibrate, UiKit.Kind.Ghost),
            });
        }

        public static void ShowPause(GameDirector dir)
        {
            Session sn = dir.S;
            int band = Vaults.VaultOf(sn.levelNo);
            _pauseName.text = (sn.IsMade ? (sn.lv.name ?? "MADE CUBE")
                            : sn.IsDaily ? "DAILY " + Daily.DayLabel()
                            : Vaults.LevelName(sn.levelNo)).ToUpperInvariant();
            _pauseSub.text = (sn.IsDaily
                ? "PAR " + sn.lv.par
                : "VAULT " + Vaults.RomanOf(band) + " / " + Vaults.VaultName(band) + " / PAR " + sn.lv.par)
                .ToUpperInvariant();
            Show("pause");
        }

        // ---- calibrate -------------------------------------------------------
        //
        // YOU CALIBRATE AN INSTRUMENT; YOU READ A MANUAL. These were the wrong way
        // round in an early build: the settings lived unlabelled inside the pause
        // card while the button called CALIBRATE opened the how-to, so a player
        // looking for brightness pressed CALIBRATE and got a rulebook.

        static void BuildCalibrate()
        {
            RectTransform L = Layer("calibrate");
            Flow col = UiKit.Column(L, false);

            H2(col, "h", "CALIBRATE");
            Sub(col, "sub", "TUNE THE INSTRUMENT", true);

            // .settings — one card with the rows inside it and a hairline between
            // each. A row of eight items that differ only in their wording has to be
            // read; a row where each carries a mark is RECOGNISED, which is what you
            // want on the screen somebody opens to change one thing they already had
            // in mind.
            RectTransform panel = UiKit.Slot(col, "settings", 0f, Css.BtnMax, 12f);
            var plate = panel.gameObject.AddComponent<Image>();
            plate.sprite = UiKit.Frame(Css.RCard, 0f);
            plate.type = Image.Type.Sliced;
            plate.color = Palette.Card;
            plate.raycastTarget = false;
            UiKit.Edge(panel, Css.Edge2, Css.RCard, Css.Hair);

            float y = 0f;
            RectTransform SetRow(string icon, string label, string hint, bool slide)
            {
                // .settings .row{ min-height:50px; padding:9px 14px }
                float h = slide ? 50f + 9f + 17f : string.IsNullOrEmpty(hint) ? 50f : 50f + Css.TMicro * 1.4f;
                RectTransform r = UiKit.Rect(panel, label, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                             Vector2.zero, Vector2.zero);
                r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0f, -y);
                r.sizeDelta = new Vector2(0f, h);

                if (y > 0f) Hairline(r);

                // .rIc — 19 by 19, and rust once the row is on
                UiKit.Mark(r, "ic", Art.Row(label, icon), Palette.Dim, 19f, 19f,
                           new Vector2(0f, 0.5f), new Vector2(14f + 9.5f, slide ? 8f : 0f));

                float top = slide || !string.IsNullOrEmpty(hint) ? Css.TSm * 1.4f : 0f;
                UiKit.Label(r, "lbl", label, Css.TSm, Palette.Ink, TextAnchor.MiddleLeft, 0.14f,
                            new Vector2(0f, string.IsNullOrEmpty(hint) && !slide ? 0f : 0.5f),
                            new Vector2(1f, 1f), new Vector2(45f, 0f), new Vector2(-14f, -9f));

                if (!string.IsNullOrEmpty(hint) && !slide)
                    UiKit.Label(r, "hint", hint, Css.TMicro, Palette.Dim2, TextAnchor.UpperLeft, 0.10f,
                                new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                                new Vector2(45f, 9f), new Vector2(-14f, -3f));

                y += h;
                return r;
            }

            void Toggle(string icon, string label, string hint, System.Func<int> get, System.Action<int> set)
            {
                RectTransform r = SetRow(icon, label, hint, false);
                RectTransform sw = UiKit.Rect(r, "sw", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                              Vector2.zero, Vector2.zero);
                sw.anchoredPosition = new Vector2(-14f - UiKit.SwW * 0.5f, 0f);
                UiKit.Switch(sw, get() != 0, _ =>
                {
                    int v = get() != 0 ? 0 : 1;
                    set(v);
                    Store.Save();
                    return v != 0;
                });
            }

            void Bar(string icon, string label, string readout, int lo, int hi,
                     System.Func<int> get, System.Action<int> set, bool percent)
            {
                RectTransform r = SetRow(icon, label, "", true);
                // .slide{ display:flex;gap:10px;margin-top:9px }
                RectTransform slide = UiKit.Rect(r, "slide", new Vector2(0f, 0f), new Vector2(1f, 0f),
                                                 new Vector2(45f, 9f), new Vector2(-14f, 26f));
                Text val = UiKit.Label(slide, "sLbl", percent ? get() + "%" : readout, Css.TMicro,
                                       Palette.Dim2, TextAnchor.MiddleLeft, 0.16f,
                                       new Vector2(0f, 0f), new Vector2(0f, 1f),
                                       Vector2.zero, new Vector2(Css.TMicro * 0.6f * 10f, 0f));
                RectTransform track = UiKit.Rect(slide, "range", new Vector2(0f, 0f), new Vector2(1f, 1f),
                                                 new Vector2(Css.TMicro * 0.6f * 10f + 10f, 0f), Vector2.zero);
                UiKit.Range(track, lo, hi, get(), v =>
                {
                    set(v);
                    Store.Save();
                    if (percent) val.text = v + "%";
                });
            }

            Toggle(Art.RicSound, "SOUND", "", () => Store.Data.sound,
                   v => { Store.Data.sound = v; if (v == 0) Sfx.I.StopAmbience(); else Sfx.I.Ambience(Vaults.VaultOf(_dir.S.levelNo)); });
            Toggle(Art.RicHaptic, "HAPTICS", "", () => Store.Data.haptic, v => Store.Data.haptic = v);
            Toggle(Art.RicFx, "EFFECTS", "SHAKE · SPARKS · BLOOM", () => Store.Data.fx,
                   v => { Store.Data.fx = v; _dir.Glow.Refresh(); });
            Toggle(Art.RicToGo, "FOLDS TO GO", "CYAN: PERFECT LINE · RUST: YOU SLIPPED",
                   () => Store.Data.togo, v => Store.Data.togo = v);
            Toggle(Art.RicDepth, "DEPTH READOUT", "DISTANCE PRINTED ON EVERY CELL",
                   () => Store.Data.depth, v => Store.Data.depth = v);
            // A SWITCH SAYS WHETHER, A SLIDER SAYS HOW MUCH. Haptics were one bit
            // for a thing that is not one bit: a pattern that reads as a ratchet on
            // one phone is a jolt on another, and the only person who can settle
            // that is holding the phone.
            Bar(Art.RicBuzz, "HAPTIC FEEDBACK", "INTENSITY", 0, 150,
                () => Store.Data.buzz, v => Store.Data.buzz = v, false);
            // A DARK GAME ON A BRIGHT DAY IS AN UNREADABLE GAME, and the phone's own
            // brightness slider moves the whole system, not this. Both default to
            // 100, and at 100 no filter is applied at all.
            Bar(Art.RicBright, "BRIGHTNESS", "", 60, 160,
                () => Store.Data.bright, v => Store.Data.bright = v, true);
            Bar(Art.RicContrast, "CONTRAST", "", 70, 150,
                () => Store.Data.contrast, v => Store.Data.contrast = v, true);

            panel.sizeDelta = new Vector2(panel.sizeDelta.x, y);
            col.Layout();

            Btn2(col, "feet", false, true, new[]
            {
                ("MANUAL", (System.Action)ShowManual, UiKit.Kind.Ghost),
                ("BACK", (System.Action)Back, UiKit.Kind.Ghost),
                ("MENU", (System.Action)ShowTitle, UiKit.Kind.Ghost),
            });
        }

        public static void ShowCalibrate() => Show("calibrate");

        // ---- boards ----------------------------------------------------------
        //
        // The bridge could always POST a score and there was never anywhere to go
        // and look at one. Every row here reads its own local record too, so the
        // screen is worth opening on a plane — the RANKING is the part that needs a
        // host, not the number.

        static RectTransform _boardList, _boardRail;
        static Text _boardHead;

        static void BuildBoards()
        {
            RectTransform L = Layer("boards");
            Flow col = UiKit.Column(L, false);

            RectTransform bar = UiKit.Slot(col, "vaultBar", Css.HCtl, Css.GridMax, 16f);
            RectTransform id = UiKit.Fill(bar, "vaultId");
            _boardRail = UiKit.Rect(id, "rail", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                    Vector2.zero, Vector2.zero);
            _boardRail.sizeDelta = new Vector2(Mathf.Min(180f, Css.Vw(52f)), 3f);
            _boardHead = UiKit.Label(id, "head", "BOARDS", Css.TTiny, Palette.Dim,
                                     TextAnchor.UpperCenter, 0.18f,
                                     new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -8f));

            _boardList = UiKit.Slot(col, "boardList", 0f, Css.BtnMax, 12f);
            var plate = _boardList.gameObject.AddComponent<Image>();
            plate.sprite = UiKit.Frame(Css.RCard, 0f);
            plate.type = Image.Type.Sliced;
            plate.color = Palette.Card;
            plate.raycastTarget = false;
            UiKit.Edge(_boardList, Css.Edge2, Css.RCard, Css.Hair);

            EdMsg(col, "boardMsg");

            Btn2(col, "outs", false, false, new[]
            {
                ("BACK", (System.Action)Back, UiKit.Kind.Ghost),
                ("MENU", (System.Action)ShowTitle, UiKit.Kind.Ghost),
            });
        }

        public static void ShowBoards()
        {
            for (int i = _boardList.childCount - 1; i >= 0; i--)
            {
                Transform ch = _boardList.GetChild(i);
                if (ch.name != "edge") Object.Destroy(ch.gameObject);
            }

            // LOCAL ONLY, SAID OUT LOUD. Every row here is this device's own record,
            // and a screen called BOARDS that does not say so is quietly implying a
            // leaderboard it does not have.
            _boardHead.text = "BOARDS — LOCAL ONLY";
            UiKit.Rail(_boardRail, Palette.Dim2, 1f);

            var rows = new List<(string k, string why, string v)>();

            // The daily first, because it is the one number shared with anybody else.
            int today = Daily.DayIndex();
            rows.Add(("TODAY", "FEWEST FOLDS ON " + Daily.DayLabel(),
                      Store.Data.dailySolved != 0 && Store.Data.dailyDay == today
                          ? Store.Data.dailyBest + " FOLDS" : "—"));

            // THE WINDOWS. A missed day is not a zero, it is a MISS, and it costs
            // more than the worst honest attempt — otherwise skipping a hard cube
            // would be the optimal play. So these read as "lower is better", and
            // turning up and playing badly always beats not turning up.
            foreach (DailyBoards.Period p in DailyBoards.Periods)
            {
                var (total, played, of) = DailyBoards.Running(p, today);
                rows.Add((p.label, p.why, played == 0 ? "0 / " + of + " DAYS"
                                                     : total + " · " + played + " / " + of + " DAYS"));
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

                rows.Add(("VAULT " + Vaults.RomanOf(band),
                          Vaults.VaultName(band).ToUpperInvariant() + " — TIME TO CLEAR",
                          have == size ? (total / 1000f).ToString("0.0") + "s" : have + "/" + size));
            }

            rows.Add(("STREAK", "CONSECUTIVE DAYS SOLVED", Store.Data.streakCur + " DAYS"));

            float y = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                float h = 50f + Css.TMicro * 1.4f;
                RectTransform r = UiKit.Rect(_boardList, "r" + i, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                             Vector2.zero, Vector2.zero);
                r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0f, -y);
                r.sizeDelta = new Vector2(0f, h);
                if (i > 0) Hairline(r);

                UiKit.Label(r, "k", rows[i].k, Css.TSm, Palette.Ink, TextAnchor.MiddleLeft, 0.14f,
                            new Vector2(0f, 0.5f), new Vector2(0.7f, 1f), new Vector2(14f, 0f), new Vector2(0f, -9f));
                UiKit.Label(r, "w", rows[i].why, Css.TMicro, Palette.Dim2, TextAnchor.UpperLeft, 0.10f,
                            new Vector2(0f, 0f), new Vector2(0.75f, 0.5f), new Vector2(14f, 9f), new Vector2(0f, -3f));
                // .bVal — arc, and dim when there is no host to rank it against
                UiKit.Label(r, "v", rows[i].v, Css.TTiny, Palette.Dim, TextAnchor.MiddleRight, 0.10f,
                            new Vector2(0.7f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-14f, 0f));
                y += h;
            }
            _boardList.sizeDelta = new Vector2(_boardList.sizeDelta.x, y);

            Show("boards");
        }

        // ---- the win card ---------------------------------------------------

        static Text _winWhat, _winVault, _winVerdict, _winFolds, _winPar, _winBest;
        static RectTransform _winNextRow, _winRetryRow;

        static void BuildWin()
        {
            RectTransform L = Layer("win");
            Flow col = UiKit.Column(L);

            _winWhat = Eyebrow(col, "winWhat", "VAULT SOLVED", Palette.Rust, 6f);
            _winVault = Eyebrow(col, "winVault", "", Palette.Arc, 0f);

            // .verdict — t-2xl, .2em, and it arrives by tightening from .52em on a
            // four-step ease, which is the one animation on this card.
            RectTransform v = UiKit.Slot(col, "verdict", Css.T2Xl * 1.5f, 0f, 3f);
            _winVerdict = UiKit.Type(v, "verdict", "AT PAR", Css.T2Xl, Palette.Arc,
                                     TextAnchor.MiddleCenter, 0.2f);

            col.Space(16f);

            // .card{ max-width:360;padding:26px 22px 20px;border-radius:var(--r-card);
            //        background:var(--card);box-shadow:inset 0 0 0 var(--hair) var(--edge) }
            float statH = Css.TLg * 1.4f + 22f;
            RectTransform card = UiKit.Slot(col, "card", 26f + statH * 3f + 20f, Css.CardMax, 20f);
            var plate = card.gameObject.AddComponent<Image>();
            plate.sprite = UiKit.Frame(Css.RCard, 0f);
            plate.type = Image.Type.Sliced;
            plate.color = Palette.Card;
            plate.raycastTarget = false;
            UiKit.Edge(card, Css.Edge, Css.RCard, Css.Hair);

            _winFolds = Stat(card, "FOLDS USED", 0, statH);
            _winPar = Stat(card, "PAR", 1, statH);
            _winBest = Stat(card, "YOUR BEST", 2, statH);

            _winNextRow = UiKit.Slot(col, "next", UiKit.BtnHeight(UiKit.Kind.Primary), Css.BtnMax, 16f);
            UiKit.Btn(_winNextRow, "NEXT", () => { Show(null); _dir.Play(_dir.S.levelNo + 1); }, UiKit.Kind.Primary);

            _winRetryRow = UiKit.Slot(col, "retry", UiKit.BtnHeight(UiKit.Kind.Ghost), Css.BtnMax, 0f);
            UiKit.Btn(_winRetryRow, "RETRY FOR PAR",
                      () => { Show(null); _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey); }, UiKit.Kind.Ghost);

            // The shelf button says where it actually goes — it led to the FORGE on
            // a built cube while still reading VAULTS — and there is a way to the
            // front from here, which there was not.
            Btn2(col, "outs", true, false, new[]
            {
                ("VAULTS", (System.Action)OpenVaults, UiKit.Kind.Ghost),
                ("MENU", (System.Action)ShowTitle, UiKit.Kind.Ghost),
            });
        }

        /// <summary>
        /// .stat — its name on the left, its number on the right, and a hairline
        /// between each so three of them read as one instrument. Close enough
        /// together that the eye pairs them without being told to.
        /// </summary>
        static Text Stat(RectTransform card, string key, int slot, float h)
        {
            RectTransform r = UiKit.Rect(card, key, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                         Vector2.zero, Vector2.zero);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -(26f + slot * h));
            r.sizeDelta = new Vector2(-44f, h);

            UiKit.Label(r, "k", key, Css.TFine, Palette.Dim, TextAnchor.MiddleLeft, 0.17f,
                        Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-100f, 0f));
            if (slot > 0) Hairline(r);

            return UiKit.Label(r, "v", "0", Css.TLg, Palette.Ink, TextAnchor.MiddleRight, 0f,
                               Vector2.zero, Vector2.one, new Vector2(0f, 0f), Vector2.zero);
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
            // .verdict.par is arc, .verdict.over is rust
            _winVerdict.color = s.turns <= s.lv.par ? Palette.Arc : Palette.Rust;

            // A vault boundary is worth marking — it is the only structure an
            // endless game has, and it is where the demand steps up.
            bool crossing = !s.IsDaily && !s.IsMade && Vaults.VaultOf(s.levelNo + 1) != Vaults.VaultOf(s.levelNo);
            _winVault.text = s.IsDaily ? "DAILY " + Daily.DayLabel()
                           : crossing ? "VAULT " + Vaults.RomanOf(Vaults.VaultOf(s.levelNo + 1)) + " / "
                                        + Vaults.VaultName(Vaults.VaultOf(s.levelNo + 1))
                           : "";

            // there is no next cube after the daily — there is tomorrow
            _winNextRow.gameObject.SetActive(!s.IsDaily && !s.IsMade);
            _winRetryRow.gameObject.SetActive(s.turns > s.lv.par);

            Show("win");
        }
    }
}
