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
            BuildChapter();
            BuildPause();
            BuildCalibrate();
            BuildAccess();
            BuildWin();
            ForgeScreens.Build(dir);
            HideAll();
        }

        // ---- plumbing -------------------------------------------------------

        /// <summary>
        /// A LAYER IS NOW TWO THINGS, AND EVERY SCREEN GOT THE SECOND ONE FREE.
        ///
        /// The full-bleed rect stays, because something has to cover the whole
        /// display so nothing behind a screen can be pressed through it. What it
        /// no longer does is CARRY the background: that has moved to a rounded
        /// plate inset by the chassis, so the interface reads as a screen
        /// recessed into a panel rather than as paint on the glass.
        ///
        /// The plate is what gets returned, so every screen in this file builds
        /// itself inside the housing without one of them having to know that the
        /// housing exists. Their anchors are all relative and they were already
        /// inset by twenty-eight or more; nothing had to be re-laid out.
        ///
        /// FOUR INSETS RATHER THAN ONE, because the case is a real object and its
        /// bezel is deeper at the top and bottom than at the sides. A single
        /// number here put the plate's own corner radius over the metal on two
        /// edges and left a band of it showing on the other two.
        /// </summary>
        internal static RectTransform Layer(string id)
        {
            RectTransform rt = UiKit.Rect(_canvas.transform, id, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var catcher = rt.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            // Every layer fades in, so the group it fades with is part of what a
            // layer is rather than something the transition goes looking for.
            rt.gameObject.AddComponent<CanvasGroup>();
            Layers[id] = rt.gameObject;

            RectTransform plate = UiKit.Rect(rt, "plate", Vector2.zero, Vector2.one,
                                             new Vector2(Layout.ChassisLeft, Layout.ChassisBottom),
                                             new Vector2(-Layout.ChassisRight, -Layout.ChassisTop));
            var bg = plate.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundFill;
            bg.type = Image.Type.Sliced;
            bg.color = Palette.Scrim;

            // THE GLASS IS A BOUNDARY, NOT A SUGGESTION.
            //
            // "No type ever sits on the metal" is the first rule of the chassis
            // and until now it was a thing every screen had to remember. It is a
            // clip now. A sentence that outgrows its box, a name longer than the
            // gap left for it, a control somebody anchors past the edge later —
            // none of them can reach the bezel, whatever anyone does upstream.
            //
            // It is a floor and not a fix: a word cut off at the edge of the glass
            // is still a bug, and this only guarantees that it is a bug INSIDE the
            // screen. Text that has to fit wraps or shrinks — see UiKit.Label and
            // UiKit.Fit.
            plate.gameObject.AddComponent<RectMask2D>();
            return plate;
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
                GameObject go = Layers[id];
                go.SetActive(true);
                if (Stack.Count == 0 || Stack[Stack.Count - 1] != id) Stack.Add(id);
                _dir.StartCoroutine(Arrive(go));
            }
            else Stack.Clear();
            _dir.SetScreenUp(id != null);
        }

        /// <summary>
        /// A SCREEN THAT SNAPS IN HAS NO WEIGHT.
        ///
        /// A hundred and forty milliseconds of fade and a few pixels of rise, and
        /// the difference is not that it looks nicer — it is that the eye is TOLD
        /// something arrived, so it goes looking for what changed instead of
        /// re-reading a screen it thinks it was already on. Short enough that
        /// nobody waiting to press a button ever waits.
        ///
        /// On the UNSCALED clock, because menus are opened during hitstop and a
        /// card that eases in at a third of speed reads as the game hanging.
        /// </summary>
        static System.Collections.IEnumerator Arrive(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            CanvasGroup cg = UiKit.Ensure<CanvasGroup>(go);

            const float Dur = 0.14f, Rise = 18f;
            float t = 0f;
            while (t < Dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Dur);
                float e = 1f - (1f - k) * (1f - k);          // out-quad: fast, then settles
                cg.alpha = e;
                rt.anchoredPosition = new Vector2(0f, (1f - e) * -Rise);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = Vector2.zero;
        }

        public static void Back()
        {
            if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
            Show(Stack.Count > 0 ? Stack[Stack.Count - 1] : null);
        }

        // ---- the back button --------------------------------------------------
        //
        // THERE IS A STACK AND NOTHING WAS ROUTED TO IT.
        //
        // Show pushes, Back pops, and the only caller either had was a button
        // labelled BACK. Android's back gesture arrives as KeyCode.Escape, and the
        // one line reading it did this:
        //
        //     if (Escape && !_screenUp && S.lv != null) ShowPause();
        //
        // — so back opened the pause card while playing and did NOTHING on every
        // menu in the game. The manual, the vault rack, calibrate, access, the
        // Forge and its editor: a player swiping back on any of them got no
        // response at all, which on Android does not read as "that is not a
        // control here", it reads as the app having hung.
        //
        // THE ROOT IS THE ONE THAT NEEDS A DECISION. Popping the title leaves an
        // empty stack, which is a turning cube with no interface on it and no way
        // out — so the title is where back means LEAVE. It asks first: a single
        // press says so on the footer, a second inside two seconds takes it, and
        // anything else cancels. Everything in this game is saved, so an accidental
        // exit costs nothing but the animation — but a game that closes on one
        // stray gesture feels careless, and the prompt is one label that already
        // exists on that screen.
        //
        // AND A WON CUBE HAS NO BACK. Popping the win card would drop the player
        // onto the board they have just finished, with the collapse over and
        // nothing to do on it. Back there means the same thing MENU does.

        static Text _foot;
        const string FootLine = "DIAGNOSTIC BUILD · NO DEAD PIXELS";
        static float _leaveAskedAt = -99f;

        /// <summary>What a back press means, given what is on top of the stack.</summary>
        public enum BackAct
        {
            /// <summary>Nothing is up; the caller decides (it opens the pause card).</summary>
            None,
            /// <summary>Pop one screen.</summary>
            Pop,
            /// <summary>A finished cube has no back — go to the front instead.</summary>
            Menu,
            /// <summary>The root. Offer to leave.</summary>
            Ask,
            /// <summary>Asked already, and recently. Close the application.</summary>
            Leave
        }

        /// <summary>
        /// THE DECISION, WITH NOTHING IN IT THAT NEEDS A CANVAS — so the harness
        /// can assert what every screen does with a back press without building
        /// one. Same reason Coach.Next is a pure function.
        /// </summary>
        public static BackAct DecideBack(string top, bool asking)
        {
            if (string.IsNullOrEmpty(top)) return BackAct.None;
            if (top == "title") return asking ? BackAct.Leave : BackAct.Ask;
            if (top == "win") return BackAct.Menu;
            return BackAct.Pop;
        }

        /// <summary>How long the offer to leave stands.</summary>
        public const float LeaveWindow = 2f;

        /// <summary>
        /// Handle a back press against whatever is on screen.
        /// Returns true only when the caller should close the application.
        /// </summary>
        public static bool BackPressed()
        {
            string top = Stack.Count > 0 ? Stack[Stack.Count - 1] : null;
            bool asking = Time.unscaledTime - _leaveAskedAt < LeaveWindow;

            switch (DecideBack(top, asking))
            {
                case BackAct.Leave:
                    return true;

                case BackAct.Ask:
                    _leaveAskedAt = Time.unscaledTime;
                    if (_foot != null)
                    {
                        _foot.text = "PRESS BACK AGAIN TO LEAVE";
                        _dir.StartCoroutine(RestoreFoot());
                    }
                    return false;

                case BackAct.Menu:
                    ShowTitle();
                    return false;

                case BackAct.Pop:
                    Back();
                    return false;

                default:
                    return false;
            }
        }

        static System.Collections.IEnumerator RestoreFoot()
        {
            // the real clock, because the offer is a real two seconds however
            // slowly the game happens to be running
            yield return new WaitForSecondsRealtime(LeaveWindow);
            if (_foot != null && Time.unscaledTime - _leaveAskedAt >= LeaveWindow) _foot.text = FootLine;
        }

        internal static RectTransform Column(RectTransform parent, float top, float bottom)
            => UiKit.Rect(parent, "col", new Vector2(0, 0), new Vector2(1, 1),
                          new Vector2(40, bottom), new Vector2(-40, -top));

        /// <summary>One of the four quiet destinations under the primary: 0,1 top row; 2,3 bottom.</summary>
        static Button Quad(RectTransform col, int i, string label, System.Action act)
        {
            float x = i % 2, y = i / 2;
            float top = -(UiKit.PrimaryH + 16f + y * (UiKit.BtnH + 12f));
            RectTransform r = UiKit.Rect(col, label,
                new Vector2(x * 0.5f, 1), new Vector2((x + 1) * 0.5f, 1),
                new Vector2(x == 0 ? 0 : 6, top - UiKit.BtnH), new Vector2(x == 0 ? -6 : 0, top));
            return UiKit.Bracketed(r, label, label, act, 24);
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

        /// <summary>
        /// THE FOURTH DOOR ANSWERS TO WHO IS STANDING AT IT.
        ///
        /// The front door offered DAILY, VAULTS, FORGE and CALIBRATE, and the
        /// rules were behind none of them — the manual was reachable only from
        /// inside the settings screen, which nobody guesses. Meanwhile a level
        /// editor with a verify pass and share codes is the least useful thing in
        /// this game to somebody who has not yet finished a cube: it is a tool for
        /// making the thing they have not played.
        ///
        /// So for exactly as long as the save has never cleared anything, this
        /// slot is MANUAL. The first clear turns it into FORGE and it never turns
        /// back — the same reading of `reached` the coach migrates on, so "has
        /// never finished a cube" has one definition in this game rather than two.
        /// </summary>
        static Button _makeQuad;

        static bool BeforeTheFirstClear => Store.Data.reached <= 1;

        static void OpenForgeOrManual()
        {
            if (BeforeTheFirstClear) ShowManual();
            else ForgeScreens.OpenShelf();
        }

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
            _resumeVault = UiKit.Label(L, "resumeVault", "", 19, Palette.Rust, TextAnchor.UpperCenter,
                                       new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -290), new Vector2(0, -262));
            _resumeName = UiKit.Label(L, "resume", "", 24, Palette.Ink, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -322), new Vector2(0, -292));

            // .mastRule — one pixel of rust, min(88vw, 420px) wide, 11px above and
            // 12px below. It is the only rule on the screen and it closes the
            // masthead: everything above it is what the game is called and where
            // you are in it, everything below is the machine itself.
            //
            // THE PITCH IS GONE AND THE SPACE IT HAD IS NOT RECLAIMED. Three lines
            // of prose used to sit under this rule — "You are a black hole inside a
            // broken machine…" — and the ninety units they occupied are deliberately
            // left empty rather than closed up. The cube is turning behind this
            // screen and it is the best argument the title has; air between the
            // masthead and the object is the composition, not a hole where a
            // paragraph used to be.
            UiKit.Panel(L, "mastRule", Palette.Rust,
                        new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-317, -346), new Vector2(317, -344));

            // ONE PRIMARY, THEN A GRID OF FOUR.
            //
            // Five equal rows is a list, and a list has no answer to "what do I
            // press". The thing you came here to do is a full-width solid; the four
            // places you might go instead are a quiet two-by-two under it. That
            // shape also buys back most of the height five stacked rows were
            // spending, which is what leaves room for the cube to be the biggest
            // object on its own title screen.
            // Heights are the shipped ones rather than fractions of a guessed
            // column: 120 for the primary, 104 for a control, stacked from the
            // floor so the block is exactly as tall as its contents.
            RectTransform col = UiKit.Rect(L, "buttons", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(48, 96), new Vector2(-48, 452));

            RectTransform go = UiKit.Rect(col, "CONTINUE", new Vector2(0, 1), new Vector2(1, 1),
                                          new Vector2(0, -UiKit.PrimaryH), new Vector2(0, 0));
            UiKit.Bracketed(go, "CONTINUE", "CONTINUE",
                            () => { Show(null); _dir.Play(Vaults.Resume(Store.Data.reached)); }, 30, true);

            Quad(col, 0, "DAILY", () => { Show(null); _dir.Play(Daily.SpecLevel, LoadKind.Daily); });
            Quad(col, 1, "VAULTS", OpenVaults);
            _makeQuad = Quad(col, 2, "FORGE", OpenForgeOrManual);
            Quad(col, 3, "CALIBRATE", ShowCalibrate);

            _playLabel = go.GetComponentInChildren<Text>();

            _foot = UiKit.Label(L, "foot", FootLine, 19, Palette.Dim, TextAnchor.LowerCenter,
                                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40), new Vector2(0, 76));
        }

        public static void ShowTitle()
        {
            int r = Vaults.Resume(Store.Data.reached);
            bool done = Vaults.Cleared(Store.Data.runs);
            // A machine that has never been started is INITIALISED, not continued.
            //
            // AND ONE THAT HAS BEEN FINISHED IS NEITHER. It used to read THE CORE,
            // because a finished save sat past the last cube and the button was
            // offering it back. Finishing relocks the ladder now and hands it over
            // at cube one, so the button is genuinely starting again — and AGAIN
            // is the honest word for that, as well as being the one the second
            // chapter says. INITIALISE would be a lie to somebody who has already
            // been to the core.
            if (_playLabel != null)
                _playLabel.text = done && r <= 1 ? "[ AGAIN ]"
                                : r <= 1 ? "[ INITIALISE ]" : "[ CONTINUE ]";
            if (_makeQuad != null)
                UiKit.SetLabel(_makeQuad, BeforeTheFirstClear ? "MANUAL" : "FORGE");

            int band = Vaults.VaultOf(r);
            if (_resumeVault != null)
                _resumeVault.text = "VAULT " + Vaults.RomanOf(band) + ": " + Vaults.VaultName(band);
            if (_resumeName != null)
            {
                // the cube's own name and what it costs, so the button has a subject
                int par = Baked.TryAt(r, out BakedLevel bl) ? bl.par : -1;
                _resumeName.text = Vaults.LevelName(r) + (par >= 0 ? "  /  PAR " + par : "");
            }
            Stack.Clear();
            _leaveAskedAt = -99f;
            if (_foot != null) _foot.text = FootLine;
            Show("title");
            _dir.ShowAttract();
        }

        // ---- vault select ---------------------------------------------------

        static Text _vaultName;
        static RectTransform _grid;
        static Button _prevBtn, _nextBtn;

        static void BuildVaults()
        {
            RectTransform L = Layer("vaults");
            Solid(L);

            // It says "VAULT I" here and "VAULT X · SINGULARITY" by the end, in
            // a gap fixed by the two arrows either side of it — so it shrinks to
            // fit rather than sliding underneath them. See UiKit.Fit.
            _vaultName = UiKit.Fit(
                UiKit.Label(L, "vaultName", "VAULT I", 32, Palette.Ink, TextAnchor.MiddleCenter,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(120, -140), new Vector2(-120, -80)), 18);

            // AND BOTH ARROWS STOP. The rack is `RankedVaults` wide and neither end
            // of it is a suggestion: past the last one VaultName starts inventing
            // titles for bands that hold no cubes. The buttons are kept and made
            // INERT at the ends rather than hidden, because a control that vanishes
            // moves everything beside it and a player reads that as the screen
            // changing rather than as the list ending.
            RectTransform prev = UiKit.Rect(L, "prev", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -154), new Vector2(140, -66));
            _prevBtn = UiKit.Bracketed(prev, "prev", "<",
                () => { _viewBand = Mathf.Max(0, _viewBand - 1); PaintVaults(); }, 26);
            RectTransform next = UiKit.Rect(L, "next", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -154), new Vector2(-40, -66));
            _nextBtn = UiKit.Bracketed(next, "next", ">",
                () => { _viewBand = Mathf.Min(Vaults.LastBand, _viewBand + 1); PaintVaults(); }, 26);

            _grid = UiKit.Rect(L, "grid", new Vector2(0, 0), new Vector2(1, 1),
                               new Vector2(RackInset, 300), new Vector2(-RackInset, -RackTop));

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
            // A FIXED FOOT, AND THE RACK TAKES WHAT IS LEFT.
            //
            // These used to follow the rack — placed directly under however many
            // rows PaintVaults drew — which fixed one hole by opening another: a
            // ten-cube vault put the seed box at 724 and BACK at 950, so a quarter
            // of the screen was a single continuous black band with a link
            // floating at the bottom of it. Now the foot is where a foot goes and
            // the CARDS grow to meet it, which is the one thing on this screen
            // that is worth more space.
            _jumpRow = UiKit.Rect(L, "jumpRow", new Vector2(0, 1), new Vector2(1, 1),
                                  new Vector2(40, -JumpTop), new Vector2(-40, -(JumpTop + Access.TapTarget)));
            _jumpField = UiKit.Field(_jumpRow, "jump", "CUBE NUMBER",
                                     new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, new Vector2(-8, 0));
            _jumpField.contentType = InputField.ContentType.IntegerNumber;
            RectTransform goSlot = UiKit.Rect(_jumpRow, "goSlot", new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Bracketed(goSlot, "go", "GO", Jump, 24);

            _jumpMsg = UiKit.Label(L, "jumpMsg", "", 18, Palette.Dim, TextAnchor.UpperCenter,
                                   new Vector2(0, 1), new Vector2(1, 1),
                                   new Vector2(40, -(JumpTop + Access.TapTarget + 8f)),
                                   new Vector2(-40, -(JumpTop + Access.TapTarget + 42f)));

            // A LINK, NOT A PLATE. This screen's subject is the rack; BACK was a
            // full-width filled control at the bottom of it, which made the heaviest
            // object on the screen the one way OFF the screen. The house rule is
            // already written down in UiKit: one plate per screen, and everything
            // else is a bracketed label with nothing behind it.
            RectTransform back = UiKit.Rect(L, "back", new Vector2(0, 0), new Vector2(1, 0),
                                            new Vector2(60, FootFloor), new Vector2(-60, FootFloor + Access.TapTarget));
            UiKit.Link(back, "back", "BACK", Back, 24, Palette.Ink);
        }

        /// <summary>
        /// HOW MANY COLUMNS, AND HOW BIG A CARD — one answer, computed from the
        /// vault's size and the space there is for it.
        ///
        /// THE SHAPE OF A CARD IS THE CONSTRAINT, NOT THE NUMBER OF THEM. A card
        /// carries a number over a name over three pips, and that stack stops
        /// working at both ends: past <see cref="CardTallest"/> it is a tower,
        /// under <see cref="CardFlattest"/> it is a bar in a chart with writing
        /// on it. Every column count that produces a card between those two is
        /// allowed, and the one with the MOST CARD wins — area, because all three
        /// things on it get easier as it grows.
        ///
        /// It falls out at three columns for the fifteen every vault holds, and
        /// the rack then fills the height it is given exactly. At twenty-five it
        /// falls out at five, which is what the constant this replaced was right
        /// about, for that size. At ten it is three again, four rows.
        ///
        /// Static and given its numbers rather than reading the layout, so
        /// RackChecks can ask it about sizes this game does not currently ship.
        /// </summary>
        internal const float CardTallest = 1.30f, CardFlattest = 0.62f;

        /// <summary>The column counts worth considering. Two is a list; seven is a keyboard.</summary>
        internal const int MinCols = 3, MaxCols = 6;

        internal static (int cols, float w, float h) Rack(int size, float gridW, float avail)
        {
            int bestCols = MaxCols;
            float bestW = gridW / MaxCols, bestH = 1f, bestArea = -1f;

            for (int cols = MinCols; cols <= MaxCols; cols++)
            {
                int rows = Mathf.CeilToInt(size / (float)cols);
                if (rows < 1) continue;

                float w = gridW / cols;
                float h = Mathf.Min(w * CardTallest, avail / rows);
                if (h < w * CardFlattest) continue;      // a bar, not a card

                float area = w * h;
                if (area <= bestArea) continue;
                bestCols = cols; bestW = w; bestH = h; bestArea = area;
            }

            // NOTHING QUALIFYING IS STILL AN ANSWER. A vault big enough that every
            // column count draws bars gets the most columns and whatever height is
            // left, which is cramped and legible — the alternative is a rack drawn
            // off the bottom of the screen.
            if (bestArea < 0f)
            {
                int rows = Mathf.Max(1, Mathf.CeilToInt(size / (float)MaxCols));
                bestH = Mathf.Min(bestW * CardTallest, avail / rows);
            }
            return (bestCols, bestW, Mathf.Max(1f, bestH));
        }

        /// <summary>Where the rack starts, and where the seed box that follows it does.</summary>
        const float RackTop = 170f;

        /// <summary>The rack's margin either side of the plate.</summary>
        const float RackInset = 40f;

        /// <summary>
        /// The rack's room, in canvas units: how wide the grid is and how much
        /// height there is between the heading and the seed box. Twenty-six of
        /// that height is air under the last row.
        ///
        /// It answers in REFERENCE units rather than from the live rect, because
        /// RackChecks asks it before any canvas has been laid out — and because
        /// the plate is the same 625 by 1127 on every display, which is the whole
        /// point of authoring against one.
        /// </summary>
        internal static (float gridW, float avail) RackSpace()
            => (Layout.PlateWidth - RackInset * 2f, JumpTop - RackTop - 26f);
        /// <summary>
        /// The seed box, measured up from the foot: BACK, twenty-four of air, the
        /// message line, eight, and then the box itself.
        /// </summary>
        internal static float JumpTop => Layout.PlateHeight - FootFloor - Access.TapTarget * 2f - 66f;

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

            // ---- AND A CUBE YOU HAVE NOT REACHED IS SHUT ---------------------
            //
            // This used to hand the cube over as PRACTICE — playable, with nothing
            // recorded — which meant the ladder had no lock on it at all: type any
            // number and the cube arrives. A gate that lets everybody through is a
            // gate nobody notices, and it made the relock after finishing
            // meaningless, because everything was open the whole time anyway.
            //
            // TWO REFUSALS, BECAUSE THEY ARE DIFFERENT FACTS. A cube nobody has
            // reached is ahead of the player. A cube they solved and cannot open
            // is sealed — the machine has taken it back — and the word says that
            // without saying where the key is, because finding the switch is the
            // joke.
            if (!Store.Open(level))
            {
                _jumpMsg.text = Store.EverCleared(level)
                              ? "CUBE " + level + " IS SEALED"
                              : "CUBE " + level + " IS NOT OPEN YET";
                _jumpMsg.color = Palette.Fault;
                return;
            }

            _jumpMsg.text = "";
            Show(null);
            _dir.Play(level, LoadKind.Vault);
        }

        static void OpenVaults()
        {
            // Resume rather than reached, because a finished machine stores 501 and
            // VaultOf would open the rack on a vault that does not exist.
            _viewBand = Vaults.VaultOf(Vaults.Resume(Store.Data.reached));
            PaintVaults();
            Show("vaults");
        }

        static void PaintVaults()
        {
            // BOTH ENDS, ONCE, HERE — rather than trusting the two arrows to have
            // clamped themselves. This is the one function that reads _viewBand, so
            // it is the one place that can promise the band is a real vault.
            _viewBand = Mathf.Clamp(_viewBand, 0, Vaults.LastBand);
            if (_prevBtn != null) _prevBtn.interactable = _viewBand > 0;
            if (_nextBtn != null) _nextBtn.interactable = _viewBand < Vaults.LastBand;

            for (int i = _grid.childCount - 1; i >= 0; i--) Object.Destroy(_grid.GetChild(i).gameObject);

            _vaultName.text = "VAULT " + Vaults.RomanOf(_viewBand) + " · " + Vaults.VaultName(_viewBand);

            int start = Vaults.VaultStart(_viewBand);
            int size = Vaults.VaultSize(_viewBand);

            // THREE COLUMNS OF CARDS, NOT FIVE OF NUMBERS.
            //
            // A rack of bare numbers is unreadable twice over: the number printed on
            // a cleared cube was its BEST SCORE while the number on an unplayed one
            // was its LEVEL, so the same glyph meant two different things and
            // neither was labelled. And a cube in this game has a NAME — the vault
            // list is the only place a player ever sees it, and a name is what makes
            // "the one with the plates" a thing you can go back to.
            //
            // So each cube gets a card: its number, its name, and how well it went.
            //
            // FIVE ACROSS, AND THE ROWS FOLLOW FROM THE CHAPTER.
            //
            // It was three columns, chosen for a ladder whose early vaults held
            // ten. At the twenty-five-cube vault that was NINE ROWS: the height
            // between the heading and the seed box divides to sixty-nine units
            // against a hundred and eighty-two of width, and a card at 0.38 does
            // not read as a tile in a rack, it reads as a bar in a chart. It
            // wasted the height twice over, because the cards were capped by the
            // WIDTH long before they had spent it — a hundred and sixty units of
            // black sat under the last row.
            //
            // Five divides both twenty-five and the fifteen a chapter holds now,
            // so there is no ragged last row at either size. `rows` is computed
            // rather than written down, and the height cap below is what stops a
            // three-row rack from drawing towers: the cell settles at a hundred
            // and nine by a hundred and forty-two, a portrait card that fills the
            // width it is given. The names go to two lines to pay for it.
            // AND THE RACK CHOOSES ITS OWN, because a constant cannot.
            //
            // Five was right for a vault of twenty-five and it is wrong for the
            // fifteen every vault holds: five across is three rows, the card is
            // capped by its own shape at 142 units tall, and the rack ends two
            // hundred units above the seed box. A quarter of this screen was a
            // black band with nothing in it — the same fault the foot was moved
            // to fix, reappearing above the foot instead of below it.
            //
            // Rack computes it instead, from the size of the vault and the space
            // there is. See the note over it.
            (int cols, float cellW, float cellH) = Rack(size, _grid.rect.width, JumpTop - RackTop - 26f);
            int rows = Mathf.CeilToInt(size / (float)cols);

            for (int i = 0; i < size; i++)
            {
                int level = start + i;
                int cx = i % cols, cy = i / cols;

                // THE LAST ROW IS CENTRED, NOT LEFT-ALIGNED.
                //
                // A vault of ten in three columns is three, three, three and then
                // ONE CARD alone against the left edge — which does not read as a
                // tenth cube, it reads as a layout that broke. Shifting the short
                // row by half a cell per missing card costs nothing and is the
                // difference between a rack and an accident.
                int inRow = Mathf.Min(cols, size - cy * cols);
                float nudge = (cols - inRow) * 0.5f / cols;

                RectTransform slot = UiKit.Rect(_grid, "c" + level,
                    new Vector2(cx / (float)cols + nudge, 1f), new Vector2((cx + 1f) / cols + nudge, 1f),
                    new Vector2(6, -(cy + 1) * cellH + 6), new Vector2(-6, -cy * cellH - 6));

                bool reached = Store.Open(level);
                bool cleared = Store.TryBest(level, out int best);
                bool ranked = Store.TryPar(level, out int par) && cleared;

                // THREE PIPS, AND THEY ARE THE ONLY SCORE THIS SCREEN SHOWS.
                // A raw fold count means nothing without the par beside it; "how
                // close to perfect" is the thing a player actually wants off a
                // grid, and it survives being three pixels tall.
                // A cube cleared before this build recorded pars has a best and no
                // par to judge it by. It gets one pip — cleared — rather than a
                // guess, because inventing a rating is worse than showing less.
                int pips = !cleared ? 0
                         : !ranked ? 1
                         : best <= par ? 3
                         : best <= par + 2 ? 2
                         : 1;

                Color edge = !reached ? Palette.Dim2
                           : pips == 3 ? Palette.Arc
                           : Palette.Trace;

                int lv = level;
                var btn = UiKit.Bracketed(slot, "b", "", () =>
                {
                    // A LOCKED CARD IS INERT, not a door to a practice run. It is
                    // already drawn in Dim2 and it already reads as unavailable;
                    // opening the cube anyway was the screen contradicting itself.
                    if (!reached) return;
                    Show(null);
                    _dir.Play(lv, LoadKind.Vault);
                }, 1);

                // Bracketed gives every control the same plate and edge; a card that
                // is ABOUT its state overrides both, and drops the empty label.
                Object.Destroy(btn.transform.Find("label").gameObject);
                btn.GetComponent<Image>().color = pips == 3
                    ? new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, 0.16f)
                    : Palette.Panel;
                btn.transform.Find("edge").GetComponent<Image>().color = edge;

                var card = (RectTransform)btn.transform;
                UiKit.Label(card, "n", level.ToString(), 30,
                            reached ? Palette.Ink : Palette.Dim, TextAnchor.LowerCenter,
                            new Vector2(0, 0.56f), new Vector2(1, 0.94f), Vector2.zero, Vector2.zero);

                // THE NAME WRAPS TO TWO LINES, AT FIFTEEN, AND THAT IS MEASURED.
                //
                // A card is ninety-seven units wide inside its inset. The longest
                // name this game prints is NOTHING UNDERNEATH — eighteen
                // characters, a hundred and eighty-four units at seventeen point,
                // which on one line runs out through both sides of the card. So it
                // wraps; but at seventeen the wrap does not save it either, because
                // TURNED INSIDE OUT breaks into THREE lines at that width and
                // sixty-seven units of text will not go into a forty-unit band.
                //
                // Fifteen is the largest size at which every name this game can
                // produce comes out at two lines or fewer — thirty-nine and a half
                // units against the band's forty-one. See tools/type/reflow.py,
                // which measures all ten of the long ones rather than the one
                // somebody happened to think of.
                //
                // Fit stays as the backstop, because a name is DATA: it is the
                // thing that catches a name added later that this arithmetic never
                // saw. Truncating is not an option when the game has cubes whose
                // names differ only in the last word.
                if (reached)
                    UiKit.Fit(UiKit.Label(card, "name", Vaults.LevelName(level), 15, Palette.Dim,
                                          TextAnchor.UpperCenter,
                                          new Vector2(0, 0.16f), new Vector2(1, 0.52f),
                                          Vector2.zero, Vector2.zero, true), 12);

                for (int p = 0; p < 3; p++)
                    UiKit.Icon(card, p < pips ? "sqfill" : "sq",
                               p < pips ? Palette.Arc : Palette.Dim2, 9f,
                               new Vector2(0.5f, 0.09f), new Vector2((p - 1) * 13f, 0f));
            }
        }

        // ---- the manual -----------------------------------------------------

        static void BuildManual()
        {
            RectTransform L = Layer("manual");
            Solid(L);
            // A SCREEN IS NAMED AFTER THE CONTROL THAT OPENS IT. This was
            // CALIBRATION and the settings screen is CALIBRATE — two headings one
            // word apart, and the button that opens this one says MANUAL, so the
            // player had three names for two screens.
            UiKit.Label(L, "h", "MANUAL", 40, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -80));

            // The title stays put and the rules scroll under it, so the heading is
            // always there to say what you are reading.
            RectTransform M = UiKit.Scroll(L, "scroll", new Vector2(0, 0), new Vector2(1, 1),
                                           new Vector2(0, 178), new Vector2(0, -150));

            // NINE LINES, NOT NINE PARAGRAPHS — AND A PICTURE FOR EACH.
            //
            // Nobody reads a manual in a puzzle game. They glance at it, twice, and
            // go back to the cube. So every rule is a label you can take in with one
            // saccade, a line under it, and a DIAGRAM beside it — because the four
            // things this page has to explain are all spatial, and a sentence about
            // a fold is a worse description of a fold than two squares and an arrow.
            float y = 24f;

            void Kicker(string s)
            {
                UiKit.Label(M, s, s, 19, Palette.Rust, TextAnchor.LowerLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -(y + 24)), new Vector2(-44, -y));
                y += 28;
            }

            RectTransform Entry(string head, string body, float h = 88f)
            {
                UiKit.Label(M, head, head, 23, Palette.Ink, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -(y + 30)), new Vector2(-190, -y));
                // WRAPPED, because it is a sentence. The line breaks in these
                // strings were authored against a plate that was thirty-nine units
                // wider than the one the case leaves, and every one of them that
                // no longer fits used to run out over the bezel and lose its last
                // word. The authored breaks stay — they are where the sense
                // breaks — and wrapping only catches the lines that overrun.
                UiKit.Label(M, head + "_d", body, 17, Palette.Dim, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -(y + h)), new Vector2(-190, -(y + 32)),
                            wrap: true);

                // the diagram lives in a fixed gutter on the right, so every picture
                // on the page shares one optical column
                RectTransform art = UiKit.Rect(M, head + "_art", new Vector2(1, 1), new Vector2(1, 1),
                                               new Vector2(-176, -(y + 96)), new Vector2(-44, -(y + 4)));
                y += h + 16f;
                return art;
            }

            Image Mark(RectTransform art, string role, Color c, float size, float x, float v)
                => UiKit.Icon(art, role, c, size, new Vector2(0.5f, 0.5f), new Vector2(x, v));

            Kicker("THE RULE");
            // NO AUTHORED BREAKS IN A BODY ANY MORE. They were put in against a
            // wider plate and against a label that could not wrap, and now that it
            // can they only force a short line in the middle of a good one — this
            // one broke after "connected," and left the word alone on a line. The
            // heights below are what a body of three or four wrapped lines needs;
            // the page scrolls, so the cost of the taller ones is nothing.
            RectTransform a1 = Entry("ONE FACE AT A TIME",
                "Two traces that line up when you fold are connected, however far apart they look.", 104f);
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
            RectTransform cards = UiKit.Rect(M, "cards", new Vector2(0, 1), new Vector2(1, 1),
                                             new Vector2(44, -(y + 92)), new Vector2(-44, -y));
            Card(cards, 0, "sqfill", Palette.Trace, "TRACE", "CIRCUIT");
            Card(cards, 1, "node", Palette.Node, "NODE", "COLLECT");
            Card(cards, 2, "lock", Palette.Lock, "LOCK", "BARRIER");
            Card(cards, 3, "plate", Palette.Rust, "PLATE", "INVERTS");
            Card(cards, 4, "evert", Palette.Evert, "EVERTER", "FAR SIDE");
            y += 106f;

            Kicker("WORTH KNOWING");
            RectTransform a5 = Entry("HOLD FOR MATRIX",
                "The lattice goes to glass. Keep holding and drag to turn it.");
            // the solid, seen through — three faces of one box
            Mark(a5, "sq", Palette.Arc, 44, -8, -6);
            Mark(a5, "sq", new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, 0.45f), 44, 12, 12);
            Mark(a5, "sqfill", Palette.Trace, 14, -14, -12);

            // THREE LINES, NOT FOUR — the gap under this entry was the complaint.
            // At four lines the box has to be 124 tall while its diagram gutter is
            // only 96, so twenty-eight units of empty column sat between this and
            // TO GO and read as a hole in the page. The last clause was the one
            // carrying least: "so every fold is legal on one" is the plate lesson
            // in full and this is the manual's one-line reminder of it.
            RectTransform a6 = Entry("PLATES INVERT",
                "Every trace goes dead and every dead cell lights up for five seconds. A plate is always footing.", 102f);
            Mark(a6, "sqfill", Palette.Trace, 26, -16, 14);
            Mark(a6, "sq", Palette.Dim2, 26, 14, 14);
            Mark(a6, "sq", Palette.Dim2, 26, -16, -16);
            Mark(a6, "sqfill", Palette.Trace, 26, 14, -16);

            RectTransform a7 = Entry("TO GO", "The fewest folds still possible from where you stand.", 82f);
            Mark(a7, "i.togo", Palette.Arc, 44, 0, 0);

            // THE LAST RULE, AND IT ARRIVES LAST ON PURPOSE. Vault IX is where
            // the ladder stops getting harder, so it is where a new idea has to
            // land — and the four cubes there teach it without a word. This entry
            // is for the player who came back to the manual afterwards to check
            // they had understood, which is the only thing a manual is good for.
            RectTransform a8 = Entry("STAND ON AN EVERTER",
                "The engine turns inside out. Every column shows its far side instead of its near one — the same solid, the other way round.", 124f);
            Mark(a8, "evert", Palette.Evert, 40, -14, 10);
            Mark(a8, "sq", Palette.Trace, 24, 16, -12);

            // THE COLOPHON, AND WHY A PUZZLE GAME HAS ONE.
            //
            // The typeface is licensed under the SIL OFL, which asks that the
            // copyright notice and the licence travel with the font wherever it
            // goes. The licence text itself does travel — it is a TextAsset in
            // Resources beside the .ttf, so it is inside the shipped player and
            // not only in the repository. This is the part a person can find.
            //
            // Gated on the font having actually loaded: if the import failed and
            // the game is drawing in the builtin, crediting JetBrains Mono for
            // what you are looking at would simply be false. It also means this
            // line is a live check on the import — no credit, no font.
            if (UiKit.HasMono)
            {
                Kicker("THE FACE");
                UiKit.Label(M, "colophon",
                            "JetBrains Mono, by the JetBrains Mono Project Authors, under the SIL Open Font License 1.1. The licence ships with the game.",
                            17, Palette.Dim, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(44, -(y + 76)), new Vector2(-44, -y), wrap: true);
                y += 76f;
            }

            UiKit.EndScroll(M, y + 24f);

            RectTransform got = UiKit.Rect(L, "got", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(got, "got", "GOT IT", Back, 28, true);
        }

        /// <summary>One of the four object cards: the mark, its name, and its job.</summary>
        static void Card(RectTransform row, int i, string glyph, Color col, string name, string job)
        {
            RectTransform c = UiKit.Rect(row, name, new Vector2(i / 5f, 0), new Vector2((i + 1) / 5f, 1),
                                         new Vector2(5, 0), new Vector2(-5, 0));
            UiKit.Framed(c, Palette.PanelHi, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.5f));
            UiKit.Icon(c, glyph, col, 34, new Vector2(0.5f, 1f), new Vector2(0, -32));
            UiKit.Label(c, "n", name, 19, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 26), new Vector2(0, 48));
            UiKit.Label(c, "j", job, 17, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 26));
        }

        public static void ShowManual() => Show("manual");

        // ---- pause ----------------------------------------------------------

        static Text _pauseName, _pauseWhere;

        static void BuildPause()
        {
            RectTransform L = Layer("pause");
            // Dark enough that the board is a memory rather than a distraction. The
            // pause card is still the one screen that lets the puzzle through — you
            // are in the middle of it — but at 0.72 the cube was legible straight
            // through the words, which is not "letting it through", it is clutter.
            L.GetComponent<Image>().color = new Color(0, 0, 0, 0.90f);

            // THE CARD IS ABOUT THE CUBE YOU ARE IN, NOT ABOUT BEING PAUSED.
            //
            // "PAUSED" over six identical bars tells you one thing you already know
            // and then asks you to read six labels to find the one that says go
            // back. Naming the cube and its vault costs nothing and turns the card
            // into a place; RESUME takes the only plate, the three ways to a
            // different board share a row, and the two settings screens are text.
            // AND IT IS A SCREEN, NOT A LUMP IN THE MIDDLE OF ONE.
            //
            // Every one of these was anchored to the plate's CENTRE, so the whole
            // card — name, vault, RESUME, three ways out and a link — occupied 420
            // units of an eleven-hundred-unit plate with three hundred and fifty of
            // nothing above it and the same below. Two holes.
            //
            // It takes the win card's shape now, because they are the same object
            // seen twice: what cube this is at the top, what you can do about it
            // stacked UP from the foot, and the space between them in one piece
            // rather than two. That space is not empty either — this is the one
            // card that lets the board through, and what shows in the middle of it
            // is the puzzle you are about to go back to.
            _pauseName = UiKit.Label(L, "h", "", 40, Palette.Ink, TextAnchor.LowerCenter,
                                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -206), new Vector2(-40, -150));
            _pauseWhere = UiKit.Label(L, "where", "", 17, Palette.Rust, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -244), new Vector2(-40, -214));

            float y = Layout.PlateHeight - FootFloor;

            // MEASURED DOWN FROM THE TOP, WHICH MEANS THE BOTTOM EDGE IS THE MORE
            // NEGATIVE OF THE TWO. offsetMin is the bottom-left corner and
            // offsetMax the top-right; writing them the other way round gives every
            // row a height of MINUS eighty-eight, and a rect with negative height
            // lays its children out inside nothing. That is what happened here —
            // the card kept its name and its vault line, which are anchored
            // normally, and lost RESUME, RESET, VAULTS, MENU and CALIBRATE
            // entirely, leaving a screen that could only be left by solving the
            // cube. FlowWin, which this was copied from, has it the right way up.
            RectTransform Up(string name, float h, float gap)
            {
                RectTransform r = UiKit.Rect(L, name, new Vector2(0, 1), new Vector2(1, 1),
                                             new Vector2(72, -y), new Vector2(-72, -(y - h)));
                y -= h + gap;
                return r;
            }

            // TWO LINKS, AND THE RULES ARE THE ONE THAT MATTERS.
            //
            // This was CALIBRATE alone, and the manual was reachable only from
            // inside it — the front door offers DAILY, VAULTS, FORGE and
            // CALIBRATE, so a player wanting to know what a plate does had to
            // guess that the rules live behind the settings. Nobody guesses that.
            //
            // The pause card is where somebody goes when they are stuck, which is
            // exactly when a reference is worth having, so this is the right two
            // taps: MENU, then MANUAL. The row was built for a pair once and lost
            // one; it has a pair again.
            RectTransform links = Up("links", Access.TapTarget, 18f);
            RectTransform manSlot = UiKit.Rect(links, "m", new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            UiKit.Link(manSlot, "manual", "MANUAL", ShowManual, 20);
            RectTransform calSlot = UiKit.Rect(links, "c", new Vector2(0.5f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Link(calSlot, "calibrate", "CALIBRATE", ShowCalibrate, 20);

            RectTransform three = Up("three", Access.TapTarget, 26f);
            Third(three, 0, "RESET", () => { Show(null); _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey); });
            Third(three, 1, "VAULTS", OpenVaults);
            Third(three, 2, "MENU", ShowTitle);

            RectTransform go = Up("resume", UiKit.PrimaryH, 0f);
            UiKit.Bracketed(go, "resume", "RESUME", () => Show(null), 28, true);
        }

        public static void ShowPause(GameDirector dir)
        {
            Session sn = dir.S;
            int band = Vaults.VaultOf(sn.levelNo);
            _pauseName.text = sn.IsMade ? (sn.lv.name ?? "MADE CUBE")
                            : sn.IsDaily ? "DAILY " + Daily.DayLabel()
                            : Vaults.LevelName(sn.levelNo);
            _pauseWhere.text = sn.IsDaily
                ? "PAR " + sn.lv.par
                : "VAULT " + Vaults.RomanOf(band) + " / " + Vaults.VaultName(band) + " / PAR " + sn.lv.par;
            Show("pause");
        }

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
            // TWO LINES, BECAUSE THE FACE IS A MONOSPACE NOW. Fifty-six characters
            // at twenty points across the plate is one line in a proportional face
            // and two in this one — every glyph is 0.6em, and lowercase is where
            // that costs. The box is as tall as the sentence actually needs; the
            // gap down to the first row was carrying the slack anyway.
            //
            // AND THE SAME FORTY-EIGHT OF MARGIN THE ROWS UNDER IT HAVE. It had
            // none because it had never wrapped: a single centred line sat well
            // inside the plate on its own. Two lines fill the measure, and at zero
            // margin the second one would have run to the literal edge of the
            // glass with the bezel starting at the next pixel.
            UiKit.Label(L, "sub", "Everything you know how to do is about to be worth less.",
                        20, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -272), new Vector2(-48, -200),
                        wrap: true);

            // NO AUTHORED BREAKS IN A BODY. Exactly the change the manual got, for
            // exactly the reason, and this screen is where it actually bit: the
            // breaks below were placed where a proportional face ran out of plate,
            // so under a monospace each one forced a short line AND the wrap that
            // was going to happen anyway. Four lines where two were meant, in a box
            // built for two, on all four rows. Let it wrap and it is three.
            string[] rows =
            {
                "THE LATTICE INVERTS|Every trace goes dead. Every dead cell lights up. Nothing moves — only what carries current.",
                "IT LASTS FIVE SECONDS|Then the lattice springs back and puts you on the nearest plate. Running out costs you the walk, never the vault.",
                "TAP IT AGAIN TO PRESS IT AGAIN|A plate is a door between two versions of the same engine, and the core is usually only reachable in one of them.",
                "IT ALWAYS GIVES FOOTING|Cut clean through the lattice, visible from every face. While you stand on a plate every fold is legal.",
            };

            // Three wrapped lines at nineteen points is a hair over seventy-five
            // units of type, so the body box is seventy-eight and the row pitch is
            // a hundred and thirty. Four rows from -290 put the last body's floor
            // at -790, and the STEP ON IT button's ceiling is at -967 — the column
            // still ends well clear of it.
            float y = -290;
            foreach (string r in rows)
            {
                string[] p = r.Split('|');
                UiKit.Label(L, p[0], p[0], 23, Palette.Ink, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 30), new Vector2(-48, y));
                UiKit.Label(L, p[0] + "_d", p[1], 19, Palette.Dim, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 110), new Vector2(-48, y - 32),
                            wrap: true);
                y -= 130;
            }

            RectTransform go = UiKit.Rect(L, "go", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(go, "go", "STEP ON IT", () => Show(null), 28, true);
        }

        public static void ShowPlateTeach() => Show("plate");

        // ---- the word after a chapter ----------------------------------------
        //
        // ONE WORD ON A BLACK SCREEN, TYPED. Nothing else is on it — no heading, no
        // chapter number, no button. A caption would make it a card and a button
        // would make it a step; it is neither, it is the machine saying something
        // and then not being there any more.
        //
        // It is also the only screen in the game with no way out drawn on it,
        // which is deliberate and is why it must never need one: it leaves on its
        // own after a beat, and a touch anywhere takes it early. See Chapters.

        static Text _chapterWord;
        static string _chapterFull = "";
        static float _chapterT;
        static System.Action _chapterThen;

        static void BuildChapter()
        {
            RectTransform L = Layer("chapter");
            Solid(L);

            // CENTRED ON THE PLATE, not on the aperture. There is no board behind
            // this and nothing to sit beside, so the word goes in the middle of the
            // screen the way a word alone in the dark should.
            _chapterWord = UiKit.Label(L, "word", "", 52, Palette.Ink, TextAnchor.MiddleCenter,
                                       new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                                       new Vector2(60, -70), new Vector2(-60, 70));
        }

        /// <summary>
        /// Put the word up and call `then` when it has been read — by the clock or
        /// by a touch. The caller does not need to know which.
        /// </summary>
        public static void ShowChapterWord(string word, System.Action then)
        {
            _chapterFull = word ?? "";
            _chapterThen = then;
            _chapterT = 0f;
            _chapterWord.text = "";
            Show("chapter");
        }

        public static bool ChapterUp => _chapterFull.Length > 0;

        /// <summary>
        /// Pumped from GameDirector's own Update on the UNSCALED clock, because the
        /// bent clock belongs to the board and there is no board here.
        /// </summary>
        public static void TickChapter(float dt)
        {
            if (!ChapterUp) return;
            _chapterT += dt;

            int chars = Mathf.Clamp(
                Mathf.FloorToInt((_chapterT - Chapters.Lead) / Chapters.PerChar),
                0, _chapterFull.Length);
            _chapterWord.text = _chapterFull.Substring(0, chars);

            bool touched = _chapterT > Chapters.DeafFor && UiKit.AnyPress();
            if (touched || _chapterT >= Chapters.Seconds(_chapterFull)) CloseChapter();
        }

        static void CloseChapter()
        {
            System.Action then = _chapterThen;
            _chapterFull = "";
            _chapterThen = null;
            Show(null);
            then?.Invoke();
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
                                             new Vector2(28, PanelFloor), new Vector2(-28, -200));
            UiKit.Framed(panel, Palette.Panel, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.55f));

            _calRow = 0;
            Toggle(panel, "i.sound", "SOUND", "", () => Store.Data.sound,
                   v => { Store.Data.sound = v; if (v == 0) Sfx.I.StopAmbience(); else Sfx.I.Ambience(Vaults.VaultOf(_dir.S.levelNo)); });
            // THE ON SWITCH IS THE BOTTOM OF THE SLIDER, and there is no reason
            // for both. This panel carried a HAPTICS toggle and a HAPTIC FEEDBACK
            // strength slider two rows apart, which is two controls for one
            // setting — and Haptics has always read them as one anyway: it returns
            // early on `haptic == 0 || buzz <= 0`, so zero on the slider was
            // already off. One row, named for the thing rather than for the
            // measurement, and a row of the panel's height back for the rest.
            // THE EFFECTS SWITCH WAS TWO SETTINGS WEARING ONE COAT, and both of
            // them belong on the screen that is about being able to play at all
            // rather than on the one about taste. What is left here is a door to
            // it, which is also how somebody finds a screen they did not know
            // the game had.
            Link(panel, "i.fx", "ACCESS", "MOTION · LIGHT · CONTRAST · WORDS", ShowAccess);
            Toggle(panel, "i.togo", "FOLDS TO GO", "CYAN: PERFECT · RUST: YOU SLIPPED",
                   () => Store.Data.togo, v => Store.Data.togo = v);
            // DEPTH READOUT USED TO BE HERE TOO, and it is on ACCESS as well —
            // the same switch, wired to the same field, drawn on two screens. That
            // was invisible while both had room and it is not free: eight rows is
            // what this panel holds at an 88-unit tap target, PanelChecks proves
            // it, and a duplicate was occupying one of them.
            //
            // It stays on ACCESS, which is the screen it belongs to: printing the
            // distance on every cell is a legibility aid, not a preference.

            // ---- the two looks, side by side on a real phone ------------------
            //
            // A switch rather than a rewrite. The austere board is what this was
            // built as; the warm one is rock and ice with a creature on it, and it
            // exists because a store page is won on screenshots. Both go through
            // the same contrast gate — see Palette.Warm and AccessChecks, which
            // walks every vault in both looks — so this chooses between two things
            // that are known to be readable rather than between rigour and appeal.
            Toggle(panel, "i.bright", "WARM LOOK", "ROCK AND ICE, WITH A CREATURE",
                   () => Store.Data.look, v => Store.Data.look = v);

            // Reading through the old flag rather than migrating the save: anybody
            // who had haptics switched off has haptic 0 and a strength they set
            // before that, and the slider must show them OFF rather than the
            // number it would buzz at if they turned it back on.
            Bar(panel, "i.haptic", "HAPTICS", "0 IS OFF", 0, 150,
                () => Store.Data.haptic == 0 ? 0 : Store.Data.buzz,
                v => { Store.Data.buzz = v; Store.Data.haptic = v > 0 ? 1 : 0; });
            // Both default to 100, and at 100 no filter is applied at all — the
            // cost of these two is zero for anyone who leaves them alone.
            Bar(panel, "i.bright", "BRIGHTNESS", "", 60, 160, () => Store.Data.bright, v => Store.Data.bright = v);
            Bar(panel, "i.contrast", "CONTRAST", "", 70, 150, () => Store.Data.contrast, v => Store.Data.contrast = v);

            // ---- and the way out of the soft lock, LAST ---------------------
            //
            // IT IS NOT DRAWN UNTIL THERE IS SOMETHING TO UNLOCK. On a first run
            // the whole ladder is ahead of the player and a switch offering to
            // open it is offering to spoil the thing they just started.
            //
            // AND IT IS THE LAST ROW BECAUSE IT IS THE HIDDEN ONE. CalRows is the
            // divisor for every row's height, so the list is eight tall whether or
            // not this one is drawn; a hidden row in the MIDDLE would be a hole
            // between DEPTH READOUT and HAPTICS, and a hidden row at the bottom is
            // a little more air above the feet, which nobody reads as missing.
            _unlockRow = Toggle(panel, "i.togo", "UNSEAL CUBES", "EVERYTHING YOU HAVE SOLVED",
                   () => Store.Data.unlocked, v => Store.Data.unlocked = v);

            RectTransform feet = UiKit.Rect(L, "feet", new Vector2(0, 0), new Vector2(1, 0),
                                            new Vector2(40, FootFloor), new Vector2(-40, FootFloor + Access.TapTarget));
            Third(feet, 0, "MANUAL", ShowManual);
            Third(feet, 1, "BACK", Back);
            Third(feet, 2, "MENU", ShowTitle);
        }

        static int _calRow;
        /// <summary>
        /// EIGHT, AND THE COMMENT THAT USED TO BE HERE WAS RIGHT.
        ///
        /// It said: "it is the divisor for the row height, so it is not a comment
        /// about the list — it IS the list, and a row added below without changing
        /// it draws on top of the last one." UNSEAL CUBES was added and this was
        /// left at seven, so CONTRAST drew through the feet — on a screen where the
        /// bottom row is a slider somebody is meant to drag.
        ///
        /// So it is not written down twice any more. CalRow counts what it lays
        /// out and LayoutChecks holds the result against the panel it has to fit
        /// in and against the tap target every row owes a finger.
        /// </summary>
        public const int CalRows = 8;

        /// <summary>
        /// WHERE THE THREE WAYS OFF A SETTINGS SCREEN SIT, and how much air is
        /// above them.
        ///
        /// CALIBRATE and ACCESS both put their panel's floor at 190 and their feet
        /// at 90, which leaves TWELVE UNITS between a bordered panel and a row of
        /// framed buttons — near enough to touching that the feet read as a fourth
        /// row of the panel that had fallen off the bottom of it, while ninety
        /// units of nothing sat underneath. The gap and the margin have swapped
        /// most of the way round.
        /// </summary>
        const float FootFloor = 62f;
        const float FootGap = 58f;
        static float PanelFloor => FootFloor + Access.TapTarget + FootGap;

        static void Third(RectTransform parent, int i, string label, System.Action act)
        {
            RectTransform slot = UiKit.Rect(parent, label, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                                            new Vector2(6, 0), new Vector2(-6, 0));
            UiKit.Bracketed(slot, label, label, act, 24);
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
                UiKit.Label(r, "h", hint, 17, Palette.Dim, TextAnchor.MiddleLeft,
                            new Vector2(0, 0), new Vector2(wideHint ? 0.72f : 0.30f, 0.40f),
                            new Vector2(58, 0), Vector2.zero);

            // a hairline under every row but the last, so eight rows read as one
            // instrument rather than eight unrelated controls
            if (slot < CalRows - 1)
                UiKit.Panel(r, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.20f),
                            new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 0), new Vector2(-20, 1));
            return r;
        }

        static RectTransform Toggle(RectTransform panel, string icon, string label, string hint,
                           System.Func<int> get, System.Action<int> set)
        {
            RectTransform r = CalRow(panel, icon, label, hint, true);
            UiKit.Switch(r, "sw", get() != 0, _ =>
            {
                int v = get() != 0 ? 0 : 1;
                set(v);
                Store.Save();
                return v != 0;
            }, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-108, -Access.TapTarget * 0.5f), new Vector2(-18, Access.TapTarget * 0.5f));
            return r;
        }

        static void Bar(RectTransform panel, string icon, string label, string hint,
                        int lo, int hi, System.Func<int> get, System.Action<int> set)
        {
            // The reading sits AFTER the hint rather than on top of it. Both were
            // pinned to the same left edge of the same band, so "120%" was printed
            // straight through the word INTENSITY.
            // ONE COLUMN FOR THE READING, ON EVERY ROW.
            //
            // It was anchored into the hint's band, so a row WITH a hint printed
            // the number under the label and a row without one printed it beside —
            // three sliders, two different layouts, and the odd one out was the
            // only row that also had a second word under its name. It now sits in
            // its own column, right-aligned hard against the track, so the three
            // readings line up with each other whatever the row above them says.
            RectTransform r = CalRow(panel, icon, label, hint, false);
            Text val = UiKit.Label(r, "v", get() + "%", 21, Palette.Rust, TextAnchor.MiddleRight,
                                   new Vector2(0.30f, 0), new Vector2(0.50f, 1f), Vector2.zero, new Vector2(-10, 0));
            UiKit.Bar(r, "bar", lo, hi, get(), v =>
            {
                set(v);
                Store.Save();
                val.text = v + "%";
            }, new Vector2(0.52f, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -Access.TapTarget * 0.5f), new Vector2(-18, Access.TapTarget * 0.5f));
        }

        /// <summary>
        /// A calibrate row whose control is a way somewhere else.
        ///
        /// A BRACKETED LABEL, NOT A PLATE. This was a full framed button a
        /// hundred and forty units wide and a tap target tall, which made the one
        /// row on the panel that goes nowhere by itself the heaviest thing on the
        /// screen — and it sat on top of its own hint, so the row that says what
        /// is behind the door was the row you could not read. The kit's own note
        /// on Link says why the brackets are enough: they are what says
        /// "pressable", and a plate on top of that is weight this row has not
        /// earned. Narrower too, so the hint has somewhere to be.
        /// </summary>
        static void Link(RectTransform panel, string icon, string label, string hint, System.Action act)
        {
            RectTransform r = CalRow(panel, icon, label, hint, true);
            RectTransform slot = UiKit.Rect(r, "go", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                                            new Vector2(-124, -Access.TapTarget * 0.5f),
                                            new Vector2(-18, Access.TapTarget * 0.5f));
            UiKit.Link(slot, "open", "OPEN", act, 20, Palette.Ink);
        }

        static RectTransform _unlockRow;

        public static void ShowCalibrate()
        {
            if (_unlockRow != null)
                _unlockRow.gameObject.SetActive(Vaults.Cleared(Store.Data.runs));
            Show("calibrate");
        }

        // ---- access ----------------------------------------------------------
        //
        // THE SCREEN THIS GAME NEEDED AND DID NOT HAVE.
        //
        // Everything here answers a question of the form "can this player take
        // delivery of what the game is saying", which is a different question
        // from the one CALIBRATE answers — that one is about taste and about the
        // room you are sitting in. Keeping them apart is not tidiness: a player
        // looking for the setting that stops the screen shaking should not have
        // to go through eight rows about brightness to find out whether it
        // exists.
        //
        // Three sections, one per sense, in the order the grades are stated:
        // what you can see, what you can hear, what your hand has to do.

        static int _accRow;
        const int AccRows = 8;

        static void BuildAccess()
        {
            RectTransform L = Layer("access");
            Solid(L);
            UiKit.Label(L, "h", "ACCESS", 40, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -90));
            UiKit.Label(L, "sub", "SIGHT · SOUND · HAND", 20, Palette.Dim, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -176), new Vector2(0, -146));

            RectTransform panel = UiKit.Rect(L, "panel", new Vector2(0, 0), new Vector2(1, 1),
                                             new Vector2(28, PanelFloor), new Vector2(-28, -200));
            UiKit.Framed(panel, Palette.Panel, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.55f));

            _accRow = 0;

            // ---- sight -------------------------------------------------------
            Steps(panel, "i.fx", "MOTION", "SHAKE · ZOOM · CLOCK",
                  new[] { "FULL", "LESS", "STILL" },
                  () => Store.Data.motion, v => Store.Data.motion = v);

            Steps(panel, "i.fx", "LIGHT", "SPARKS · BLOOM · FLASH",
                  new[] { "FULL", "LESS", "NONE" },
                  () => Store.Data.light, v => { Store.Data.light = v; _dir.Glow.Refresh(); });

            // THE ONE SETTING THAT REPAINTS THE GAME UNDERNEATH ITSELF, which is
            // exactly why it is worth doing in place: the row you just pressed is
            // still under your thumb, and the contrast you asked for is a thing
            // you can now see on the screen you asked for it on.
            AccToggle(panel, "i.bright", "LEGIBILITY", "EVERY LABEL AT 7:1",
                      () => Store.Data.legible,
                      v =>
                      {
                          Store.Data.legible = v;
                          UiKit.Repaint();
                          _dir.View.PushPalette();
                          _dir.Filter.Refresh();
                          Scanlines.Refresh();  // the rows go
                          Glass.Refresh();      // and the grease on the screen with them
                      });

            // ---- sound -------------------------------------------------------
            AccToggle(panel, "i.sound", "CAPTIONS", "EVERY CUE, IN WORDS",
                      () => Store.Data.captions, v => Store.Data.captions = v);
            // Bus.Apply is what makes these two faders audible WHILE THEY MOVE.
            // Without a mixer it does nothing and the level reaches the next
            // sound to be played, exactly as it always has; with one, the group's
            // volume is the bus everything is already passing through, so the bed
            // follows the ROOM slider under your thumb.
            AccBar(panel, "i.sound", "INSTRUMENT", "", 0, 150, () => Store.Data.vol,
                   v => { Store.Data.vol = v; Bus.Apply(); });
            AccBar(panel, "i.sound", "ROOM", "", 0, 150, () => Store.Data.room,
                   v => { Store.Data.room = v; Bus.Apply(); });

            // ---- hand --------------------------------------------------------
            AccToggle(panel, "i.haptic", "FOLD BUTTONS", "FOUR ARROWS · NO SWIPE NEEDED",
                      () => Store.Data.assist,
                      v => { Store.Data.assist = v; _dir.Hud.RefreshAssist(); });
            AccToggle(panel, "i.depth", "DEPTH READOUT", "DISTANCE PRINTED ON EVERY CELL",
                      () => Store.Data.depth, v => Store.Data.depth = v);

            RectTransform feet = UiKit.Rect(L, "feet", new Vector2(0, 0), new Vector2(1, 0),
                                            new Vector2(40, FootFloor), new Vector2(-40, FootFloor + Access.TapTarget));
            Third(feet, 0, "MANUAL", ShowManual);
            Third(feet, 1, "BACK", Back);
            Third(feet, 2, "MENU", ShowTitle);
        }

        public static void ShowAccess() => Show("access");

        /// <summary>
        /// A row of the access panel.
        ///
        /// <paramref name="hintTo"/> IS NOT A TASTE, IT IS THE CONTROL'S SHADOW.
        /// A switch is ninety units of the right-hand end and a hint can run most
        /// of the way to it; three named stops are a third of the row, and a hint
        /// written for the switch rows walked straight under the first of them —
        /// MOTION and LIGHT both had their last term printed behind [FULL]. The
        /// row that owns the widest control is the row that has to say so.
        /// </summary>
        static RectTransform AccRow(RectTransform panel, string icon, string label, string hint,
                                    float hintTo = 0.72f)
        {
            float h = 1f / AccRows;
            int slot = _accRow++;
            RectTransform r = UiKit.Rect(panel, label, new Vector2(0, 1 - (slot + 1) * h), new Vector2(1, 1 - slot * h),
                                         new Vector2(0, 2), new Vector2(0, -2));

            UiKit.Icon(r, icon, Palette.Rust, 26, new Vector2(0, 0.5f), new Vector2(34, 0));
            UiKit.Label(r, "l", label, 21, Palette.Ink, TextAnchor.MiddleLeft,
                        new Vector2(0, string.IsNullOrEmpty(hint) ? 0 : 0.40f), new Vector2(0.55f, 1),
                        new Vector2(58, 0), Vector2.zero);
            if (!string.IsNullOrEmpty(hint))
                UiKit.Label(r, "h", hint, 17, Palette.Dim, TextAnchor.MiddleLeft,
                            new Vector2(0, 0), new Vector2(hintTo, 0.40f), new Vector2(58, 0), Vector2.zero);

            if (slot < AccRows - 1)
                UiKit.Panel(r, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.20f),
                            new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 0), new Vector2(-20, 1));
            return r;
        }

        static void AccToggle(RectTransform panel, string icon, string label, string hint,
                              System.Func<int> get, System.Action<int> set)
        {
            RectTransform r = AccRow(panel, icon, label, hint);
            UiKit.Switch(r, "sw", get() != 0, _ =>
            {
                int v = get() != 0 ? 0 : 1;
                set(v);
                Store.Save();
                return v != 0;
            }, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
               new Vector2(-108, -Access.TapTarget * 0.5f), new Vector2(-18, Access.TapTarget * 0.5f));
        }

        static void AccBar(RectTransform panel, string icon, string label, string hint,
                           int lo, int hi, System.Func<int> get, System.Action<int> set)
        {
            RectTransform r = AccRow(panel, icon, label, hint);
            Text val = UiKit.Label(r, "v", get() + "%", 21, Palette.Rust, TextAnchor.MiddleRight,
                                   new Vector2(0.30f, 0), new Vector2(0.50f, 1f), Vector2.zero, new Vector2(-10, 0));
            UiKit.Bar(r, "bar", lo, hi, get(), v =>
            {
                set(v);
                Store.Save();
                val.text = v + "%";
            }, new Vector2(0.52f, 0.5f), new Vector2(1, 0.5f),
               new Vector2(0, -Access.TapTarget * 0.5f), new Vector2(-18, Access.TapTarget * 0.5f));
        }

        /// <summary>
        /// THREE STATES IS NOT A SWITCH AND IT IS NOT A SLIDER.
        ///
        /// A switch can only say two things, and this setting has three because
        /// the middle one is the honest answer for most people — a slider would
        /// invite a player to hunt for a number when there are only three
        /// answers and each is named. So: three labelled stops, the current one
        /// lit the way the primary is lit, and the whole row is a target.
        /// </summary>
        static void Steps(RectTransform panel, string icon, string label, string hint, string[] names,
                          System.Func<int> get, System.Action<int> set)
        {
            // TWO UNITS SHORT, WHICH WAS HALF THE BUG. The hint ran to 0.50 of the
            // row and the three stops began at 0.52, which fitted the proportional
            // face this was measured against with room to spare. In the mono,
            // SPARKS · BLOOM · FLASH is 224.4 units against 226.5 of space —
            // inside by two — and on a device it printed SPARKS · BLOOM · FLAS.
            // A hint that loses its last word is worse than no hint, and a
            // two-unit margin is not a margin. The stops moved right and the hint
            // got thirty-six units of air.
            //
            // AND THE OTHER HALF WAS MEASURING THE WRONG STRING. That fix claimed
            // the three slots were "more than [ NONE ] needs" — on the strength of
            // having measured NONE. UiKit.Bracketed renders [ LABEL ], four
            // characters more, and at seventeen point every one of these six stops
            // overflowed its 67.7-unit slot: FULL and LESS and NONE by fourteen,
            // STILL by twenty-four. They ran under each other and out through the
            // side of the panel.
            //
            // So they are Segments, which is the same plate without the brackets —
            // see UiKit.Segment for why a lit plate in a group of three does not
            // need them. STILL is 51 units now, in a slot of 67.7.
            RectTransform r = AccRow(panel, icon, label, hint, 0.56f);
            RectTransform row = UiKit.Rect(r, "steps", new Vector2(0.58f, 0.5f), new Vector2(1, 0.5f),
                                           new Vector2(0, -Access.TapTarget * 0.5f),
                                           new Vector2(-18, Access.TapTarget * 0.5f));

            var plates = new Image[names.Length];
            var labels = new Text[names.Length];

            void Paint()
            {
                int now = Mathf.Clamp(get(), 0, names.Length - 1);
                for (int i = 0; i < names.Length; i++)
                {
                    if (plates[i] == null) continue;
                    plates[i].color = i == now ? Palette.Rust : Palette.Panel;
                    labels[i].color = i == now ? Palette.Void : Palette.Dim;
                }
            }

            for (int i = 0; i < names.Length; i++)
            {
                int which = i;
                RectTransform slot = UiKit.Rect(row, names[i],
                    new Vector2(i / (float)names.Length, 0), new Vector2((i + 1f) / (float)names.Length, 1),
                    new Vector2(i == 0 ? 0 : 3, 0), new Vector2(i == names.Length - 1 ? 0 : -3, 0));

                Button b = UiKit.Segment(slot, names[i], names[i], () =>
                {
                    set(which);
                    Store.Save();
                    Paint();
                }, 17);
                plates[i] = b.GetComponent<Image>();
                labels[i] = b.GetComponentInChildren<Text>();
            }
            Paint();
        }

        // ---- the win card ---------------------------------------------------

        static Text _winWhat, _winVerdict, _winFolds, _winPar, _winBest, _winVault;
        static RectTransform _winNextRow, _winRetryRow, _winOutsRow, _winStats;
        const float WinPad = 84f;

        /// <summary>The instrument: three rows deep enough to read as one panel.</summary>
        const float StatsH = 204f;

        /// <summary>Air under the last way out, before the bezel.</summary>
        const float WinFoot = 62f;

        /// <summary>Where the heading block ends: the vault line, plus its air.</summary>
        const float WinHead = 286f;
        static Button _winNext, _winRetry;

        static void BuildWin()
        {
            RectTransform L = Layer("win");
            L.GetComponent<Image>().color = new Color(0, 0, 0, 0.92f);

            // A CARD, NOT A SCREENFUL.
            //
            // This was full-bleed: the stat names sat against the left edge and
            // their numbers against the right, six hundred pixels away, so nothing
            // read as a pair. Under it were three identical framed buttons, which is
            // three equal offers on a card that has exactly one thing you came here
            // to do. Everything now lives in a centred column the width of the thing
            // it is reporting on, the numbers sit inside a bordered plate with a
            // hairline between each row, and only NEXT keeps a plate.
            //
            // AND IT IS THREE BLOCKS, NOT ONE.
            //
            // Fixing the column left a worse problem behind: everything was still
            // measured downward from the top, so the whole card — name, verdict,
            // instrument and all three ways out — finished two thirds of the way up
            // a plate that is eleven hundred units tall, and the bottom third was
            // empty. It read as a dialogue that had been dropped on the screen
            // rather than a screen.
            //
            // So the three blocks are anchored to three different things, because
            // they answer to three different things. WHAT HAPPENED goes to the top.
            // WHAT IT COST is the instrument and sits on the middle of the plate.
            // WHAT NOW is anchored to the BOTTOM and stacks upward from there,
            // which is both where a thumb is and the only arrangement in which a
            // hidden RETRY cannot open a hole — see FlowWin.

            _winWhat = UiKit.Label(L, "what", "VAULT SOLVED", 17, Palette.Rust, TextAnchor.LowerCenter,
                                   new Vector2(0, 1), new Vector2(1, 1), new Vector2(WinPad, -150), new Vector2(-WinPad, -122));
            _winVerdict = UiKit.Label(L, "verdict", "AT PAR", 46, Palette.Arc, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(WinPad, -224), new Vector2(-WinPad, -158));
            _winVault = UiKit.Label(L, "vault", "", 18, Palette.Arc, TextAnchor.UpperCenter,
                                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(WinPad, -262), new Vector2(-WinPad, -232));

            // BETWEEN THE TWO OF THEM, and FlowWin decides where — it is the only
            // block with slack either side of it, so it is the one that absorbs a
            // missing RETRY row instead of letting the hole land somewhere it can
            // be read as a mistake.
            _winStats = UiKit.Rect(L, "stats", new Vector2(0, 1), new Vector2(1, 1),
                                   new Vector2(WinPad, -474), new Vector2(-WinPad, -306));
            RectTransform stats = _winStats;
            UiKit.Framed(stats, Palette.PanelHi, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.55f));
            _winFolds = Stat(stats, "FOLDS USED", 0);
            _winPar   = Stat(stats, "PAR", 1);
            _winBest  = Stat(stats, "YOUR BEST", 2);

            _winNextRow = UiKit.Rect(L, "next", new Vector2(0, 1), new Vector2(1, 1),
                                     new Vector2(WinPad, -498 - UiKit.PrimaryH), new Vector2(-WinPad, -498));
            RectTransform go = _winNextRow;
            _winNext = UiKit.Bracketed(go, "next", "NEXT", () =>
            {
                // AND IF THAT CUBE ENDED A CHAPTER, THE MACHINE HAS SOMETHING TO
                // SAY FIRST. It goes between the card and the next board rather
                // than over the win: the card is about the cube just solved and
                // the word is about the fifteen of them.
                int at = _dir.S.levelNo;
                if (Chapters.Due(at, _dir.S.IsDaily, _dir.S.IsMade, Store.Data.saidChapter))
                {
                    Store.Data.saidChapter = Chapters.Mark(Store.Data.saidChapter, at);
                    Store.Save();
                    ShowChapterWord(Chapters.WordAfter(at), () => _dir.Play(at + 1));
                    return;
                }
                Show(null);
                _dir.Play(at + 1);
            }, 28, true);

            // The rest are text. They are ways OUT of this card rather than things
            // it is for, and a bracketed label with nothing behind it says that
            // without needing to be smaller or greyer than it can be read at.
            _winRetryRow = UiKit.Rect(L, "retry", new Vector2(0, 1), new Vector2(1, 1),
                                      new Vector2(WinPad, -640), new Vector2(-WinPad, -592));
            RectTransform retry = _winRetryRow;
            _winRetry = UiKit.Link(retry, "retry", "RETRY FOR PAR", () =>
            {
                Show(null);
                _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey);
            }, 21, Palette.Ink);

            _winOutsRow = UiKit.Rect(L, "outs", new Vector2(0, 1), new Vector2(1, 1),
                                     new Vector2(WinPad, -700), new Vector2(-WinPad, -652));
            RectTransform outs = _winOutsRow;
            RectTransform vSlot = UiKit.Rect(outs, "v", new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            UiKit.Link(vSlot, "vaults", "VAULTS", OpenVaults, 20);
            RectTransform mSlot = UiKit.Rect(outs, "m", new Vector2(0.5f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Link(mSlot, "menu", "MENU", ShowTitle, 20);
        }

        /// <summary>
        /// AND THE ACTIONS FLOW, because two of the three come and go: NEXT is
        /// meaningless after the daily and RETRY FOR PAR only exists if you went
        /// over. Fixed positions would leave the hole this card had before — and a
        /// PERFECT COLLAPSE, the best outcome in the game, is exactly the case that
        /// hides one.
        ///
        /// IT STACKS UPWARD, from the bottom of the plate. Flowing downward from a
        /// fixed top put the hole back in a different place: with RETRY hidden the
        /// stack simply ended a hundred units higher and left the bottom of the
        /// screen empty. Anchored to the foot, a missing row costs air ABOVE the
        /// primary — which is the one direction where extra space reads as
        /// composition rather than as something failing to load.
        ///
        /// It also puts the three of them in the reverse of their importance from
        /// the bottom up: the ways out are furthest from the thumb, NEXT is nearest
        /// the eye, and the last row is always at the same height however many rows
        /// there are.
        /// </summary>
        static void FlowWin()
        {
            // measured downward from the top of the plate, because that is the
            // frame the offsets are in — the foot is just where we start
            float y = Layout.PlateHeight - WinFoot;

            void Place(RectTransform r, float h, float gap)
            {
                if (!r.gameObject.activeSelf) return;
                r.offsetMax = new Vector2(-WinPad, -(y - h));
                r.offsetMin = new Vector2(WinPad, -y);
                y -= h + gap;
            }

            Place(_winOutsRow, Access.TapTarget, 18f);
            Place(_winRetryRow, Access.TapTarget, 26f);
            Place(_winNextRow, UiKit.PrimaryH, 0f);

            // AND THE INSTRUMENT TAKES THE MIDDLE OF WHATEVER IS LEFT. It is the
            // only block with slack on both sides, so it is where a missing RETRY
            // row is allowed to show up — as equal air above and below a panel,
            // which is a composition, rather than as a gap at one end, which is a
            // screen that failed to finish loading.
            // y is now the top of the stack — the last Place took no gap after it
            float mid = (WinHead + y) * 0.5f;
            _winStats.offsetMax = new Vector2(-WinPad, -(mid - StatsH * 0.5f));
            _winStats.offsetMin = new Vector2(WinPad, -(mid + StatsH * 0.5f));
        }

        /// <summary>
        /// One reading of the card: its name on the left, its number on the right,
        /// and a hairline under it so three of them read as one instrument. Close
        /// enough together that the eye pairs them without being told to.
        /// </summary>
        static Text Stat(RectTransform card, string key, int slot)
        {
            const int Rows = 3;
            float h = 1f / Rows;
            RectTransform r = UiKit.Rect(card, key, new Vector2(0, 1 - (slot + 1) * h), new Vector2(1, 1 - slot * h),
                                         Vector2.zero, Vector2.zero);

            UiKit.Label(r, "k", key, 17, Palette.Dim, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-120, 0));
            if (slot < Rows - 1)
                UiKit.Panel(r, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.22f),
                            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 0), new Vector2(-22, 1));

            return UiKit.Label(r, "v", "0", 26, Palette.Ink, TextAnchor.MiddleRight,
                               Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-28, 0));
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
            _winVerdict.color = s.turns <= s.lv.par ? Palette.Arc : Palette.Rust;

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
