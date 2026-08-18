using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// THE READOUT IS A ROW OF CHIPS.
    ///
    /// Two lines of bare text was legible and anonymous: three labels in a row,
    /// all the same shape, and nothing to tell you at a glance which number
    /// belonged to which idea. A chip with a SYMBOL in it is read before it is
    /// parsed — you find the green diamond, not the word NODES — and it survives
    /// being glanced at with a thumb over half of it. The label stays, quietly,
    /// because a symbol nobody has met yet is a rebus.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas _canvas;
        GameDirector _dir;

        Text _foldN, _parN, _keyN, _keyTot, _goN, _hintN, _lvNo, _lvName, _vault, _toast, _plateClock, _caption;
        Image _keyChip, _goChip, _flash, _vignette, _plateBar;
        RectTransform _aperture;
        Image[] _ticks = new Image[4];
        readonly Button[] _foldBtns = new Button[4];
        RectTransform _root;

        readonly List<Text> _depthPool = new List<Text>();
        RectTransform _depthRoot;

        /// <summary>The two verbs, taught once, on the cube that needs them. See Coach.</summary>
        public Coach Coach { get; private set; }

        float _toastT, _flashT, _flashDur, _vig, _capT;

        /// <summary>Whole seconds last printed on the plate clock; -1 means the clock is off.</summary>
        int _lastSecs = -1;
        Color _flashCol;
        string _goShown = "";

        // the last three flashes, on the real clock — see Flash
        readonly float[] _flashAt = new float[Access.FlashesPerSecond];
        int _flashIdx;

        public static Hud Build(GameDirector dir)
        {
            Canvas c = UiKit.Canvas("HUD", 10);
            var hud = c.gameObject.AddComponent<Hud>();
            hud._canvas = c;
            hud._dir = dir;
            hud.Compose();
            return hud;
        }

        void Compose()
        {
            // a zeroed timestamp is "now" during the first second of the process,
            // which would spend the whole flash budget before anything happened
            for (int i = 0; i < _flashAt.Length; i++) _flashAt[i] = -9f;

            // THE READOUT IS ON THE GLASS.
            //
            // The root used to be the whole display, which was survivable while
            // the housing was a twenty-eight unit border and wrong the whole time:
            // the chips are anchored to the top of this rect, so their upper edge
            // was under the bezel and the fold arrows were hard against it. With a
            // real case in front of them they were half swallowed, and the vault
            // name ran off the side of the machine altogether.
            //
            // Insetting HERE rather than in each control is also what keeps rule
            // one of the chassis true by construction — there is no longer
            // anywhere in this file that a word CAN be put on the metal, so it
            // cannot be done by accident later.
            //
            // TWO RECTS AND NOT ONE, and the reason is in UiKit.SafeArea: a direct
            // child of a canvas has its offsets zeroed the first time the safe
            // area is measured. The inset went on the direct child the first time
            // and lasted until frame one.
            RectTransform safe = UiKit.Rect(_canvas.transform, "safe",
                                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _root = UiKit.Rect(safe, "root", Vector2.zero, Vector2.one,
                               new Vector2(Layout.ChassisLeft, Layout.ChassisBottom),
                               new Vector2(-Layout.ChassisRight, -Layout.ChassisTop));

            // and clipped to it, so the readout cannot reach the metal however
            // long a vault is named or however narrow the display gets — the same
            // boundary every screen gets in Screens.Layer, for the same reason
            _root.gameObject.AddComponent<RectMask2D>();

            // ---- the flash and the vignette sit under everything -------------
            _flash = UiKit.Panel(_root, "flash", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vignette = UiKit.Panel(_root, "vignette", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- the aperture --------------------------------------------------
            //
            // THE BOARD IS INSIDE THE MACHINE, AND UNTIL NOW IT WAS NOT.
            //
            // Every other surface in this interface is a framed plate — that is
            // what makes the chrome read as an instrument rather than as words on
            // black — and the one thing the whole game is about had no housing at
            // all. It floated, and because a square cube fitted to a portrait
            // width cannot fill a portrait height, what it floated in was a large
            // and obviously unaccounted-for hole.
            //
            // This is the aperture that hole was always meant to be: the same
            // rounded stroke and the same --edge rust as every control, drawn
            // around exactly the rectangle Layout reserves for the board. It costs
            // one nine-sliced quad and it changes what the empty space MEANS —
            // from "the layout did not reach" to "this is the window".
            //
            // A STROKE, NOT A PLATE. The canvas is a screen-space overlay and
            // draws after the camera, so anything filled here would paint over the
            // cube. And the void inside it stays void: a cell that is not there is
            // still unrendered space, which is the one thing this game will not
            // decorate.
            // sixteen units in from the glass, and the bands are already measured
            // from it — this rect is a child of a root that is inset by the case,
            // so nothing here counts the bezel a second time
            //
            // AND THE FOUR NUMBERS COME FROM LAYOUT NOW, not from the two bands
            // and the two insets added up here. They used to be the same sum on
            // both sides, which is not the same thing as one definition: the
            // camera contains the solid inside ITS window, so a stroke drawn from
            // a private copy of the arithmetic is a line the cube can cross
            // without anything noticing. See Layout.ApertureInsets.
            Layout.ApertureInsets(out float apL, out float apB, out float apR, out float apT);
            _aperture = UiKit.Rect(_root, "aperture", new Vector2(0, 0), new Vector2(1, 1),
                                   new Vector2(apL, apB), new Vector2(-apR, -apT));
            var apEdge = _aperture.gameObject.AddComponent<Image>();
            apEdge.sprite = UiKit.RoundLine;
            apEdge.type = Image.Type.Sliced;
            apEdge.color = UiKit.Edge;
            apEdge.raycastTarget = false;

            // ON THE APERTURE, because everything it points at is in the window:
            // the ring lands on a square of the board and the swipe travels across
            // it. Built here rather than last so it draws UNDER the readout — a
            // prompt is the quietest thing on the screen, not the loudest.
            Coach = new Coach(_aperture);

            // ---- top band ----------------------------------------------------
            RectTransform top = UiKit.Rect(_root, "barTop", new Vector2(0, 1), new Vector2(1, 1),
                                           new Vector2(0, -160), new Vector2(0, 0));

            RectTransform Chip(RectTransform bar, string name, float x0, float x1, Color hue)
            {
                RectTransform rt = UiKit.Rect(bar, name, new Vector2(x0, 0.42f), new Vector2(x1, 1f),
                                              new Vector2(14, 8), new Vector2(-6, -10));
                UiKit.Framed(rt, new Color(hue.r, hue.g, hue.b, 0.10f),
                                 new Color(hue.r, hue.g, hue.b, 0.80f));
                return rt;
            }

            // FOUR READINGS, FOUR INSTRUMENTS.
            //
            // These were four runs of bare text sharing one strip, which is a
            // sentence rather than a panel: nothing said where one number stopped
            // and the next began, and the colours had to do all the separating on
            // their own. Each is now a lit chip in the colour of the thing it
            // counts — the same colour that thing is on the board — so the top of
            // the screen reads as instrumentation, and a glance can find the one
            // number it came for without parsing the other three.
            RectTransform foldChip = Chip(top, "foldChip", 0.00f, 0.25f, Palette.Rust);
            UiKit.Label(foldChip, "foldIcon", "▤", 20, Palette.Rust, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, new Vector2(14, 0), Vector2.zero);
            _foldN = UiKit.Label(foldChip, "foldN", "0", 34, Palette.Ink, TextAnchor.MiddleCenter,
                                 Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-26, 0));
            _parN = UiKit.Label(foldChip, "parN", "/0", 20, Palette.Dim, TextAnchor.MiddleRight,
                                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12, 0));

            _keyChip = Chip(top, "keyChip", 0.25f, 0.50f, Palette.Node).GetComponent<Image>();
            var keyRt = _keyChip.rectTransform;
            UiKit.Label(keyRt, "keyIcon", "◆", 20, Palette.Node, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, new Vector2(14, 0), Vector2.zero);
            _keyN = UiKit.Label(keyRt, "keyN", "0", 34, Palette.Node, TextAnchor.MiddleCenter,
                                Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-26, 0));
            _keyTot = UiKit.Label(keyRt, "keyTot", "/0", 20, Palette.Dim, TextAnchor.MiddleRight,
                                  Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12, 0));

            _goChip = Chip(top, "goChip", 0.50f, 0.78f, Palette.Arc).GetComponent<Image>();
            var goRt = _goChip.rectTransform;
            UiKit.Label(goRt, "goLbl", "TO GO", 21, Palette.Arc, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, new Vector2(14, 0), Vector2.zero);
            _goN = UiKit.Label(goRt, "goN", "·", 34, Palette.Arc, TextAnchor.MiddleRight,
                               Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14, 0));

            RectTransform hintChip = Chip(top, "hintChip", 0.78f, 1.00f, Palette.Lock);
            UiKit.Label(hintChip, "hintIcon", "⬡", 20, Palette.Lock, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, new Vector2(14, 0), Vector2.zero);
            _hintN = UiKit.Label(hintChip, "hintN", "3", 30, Palette.Lock, TextAnchor.MiddleRight,
                                 Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14, 0));

            _lvNo = UiKit.Label(top, "lvNo", "—", 22, Palette.Rust, TextAnchor.MiddleLeft,
                                new Vector2(0, 0), new Vector2(0.6f, 0.45f), new Vector2(28, 0), Vector2.zero);
            _lvName = UiKit.Label(top, "lvName", "—", 22, Palette.Dim, TextAnchor.MiddleLeft,
                                  new Vector2(0, 0), new Vector2(0.9f, 0.45f), new Vector2(96, 0), Vector2.zero);
            _vault = UiKit.Label(top, "vault", "", 20, Palette.Dim, TextAnchor.MiddleRight,
                                 new Vector2(0.5f, 0), new Vector2(1, 0.45f), Vector2.zero, new Vector2(-28, 0));

            // LAYOUT'S NUMBER, NOT A COPY OF IT. This rule closes the top band and
            // the camera frames the board against the same band, so a literal here
            // is the second definition that the aperture used to have — change one
            // and the readout draws a line where the board now starts.
            UiKit.Rule(_root, 1f).rectTransform.anchoredPosition = new Vector2(0, -Layout.TopBand);

            // ---- the plate clock ---------------------------------------------
            //
            // The count can be HEARD in the last three seconds, but it also has to
            // be SEEN, and the number cannot go where the eyes are — which is the
            // board. So it is a bar, at the top edge, draining.
            _plateBar = UiKit.Panel(_root, "plateBar", Palette.Rust,
                                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), new Vector2(0, 0));
            // AND IT IS INSIDE THE WINDOW NOW, because the band it used to sit in
            // is gone. It hung between the readout's rule and the top of the old
            // square aperture; the stroke reaches the rule now, so the place left
            // for it is under the stroke, past the fold mark. On the aperture, so
            // it goes where the window goes. See MarkBand.
            _plateClock = UiKit.Label(_aperture, "plateClock", "", 22, Palette.RustHi, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1),
                                      new Vector2(0, -(MarkBand + ClockHeight)), new Vector2(0, -MarkBand));
            _plateBar.gameObject.SetActive(false);

            // ---- the four folds, at the four edges ----------------------------
            //
            // FOUR ARROWS, NOT FOUR DASHES.
            //
            // This is the one piece of the HUD that is about the RULE rather than
            // the run — where you stand decides which folds you have — and it was
            // four six-unit bars. On a phone that is exactly what they looked like:
            // orange ticks at the edges of the screen with nothing to say which way
            // any of them meant. The rule was reading as dust on the lens.
            //
            // The arrow already exists; the manual draws the four folds with it. So
            // this is the mark the player was taught, pointing the way the fold
            // goes, lit when that fold has footing and nearly out when it does not.
            //
            // ONE MARK PER EDGE, IN BOTH MODES. The assisted fold buttons want to be
            // in the same four places, because that is where the answer to "can I
            // fold that way" already is — so they are, and the passive arrow steps
            // aside when the pressable one is on. Two arrows at one edge would be
            // the interface disagreeing with itself about which one to look at.
            //
            // THEY HANG ON THE APERTURE, NOT ON THE SCREEN. They were anchored to
            // the screen edges with hand-written offsets that happened to land near
            // the board, which is a coincidence that survives exactly until the
            // bands change height. Parented to the frame, "on the left edge of the
            // play area" is what the anchor SAYS, in both orientations and on any
            // phone, and the mark reads as part of the housing rather than as
            // something floating beside it.
            for (int t = 0; t < 4; t++)
            {
                _ticks[t] = Tick("tick" + t, FoldAnchor[t], FoldMark[t], FoldSpin[t]);
                _foldBtns[t] = FoldButton("fold" + t, t, FoldAnchor[t], FoldSlot[t], FoldMark[t], FoldSpin[t]);
            }
            RefreshAssist();

            // ---- bottom band ---------------------------------------------------
            RectTransform bot = UiKit.Rect(_root, "barBot", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(0, 0), new Vector2(0, Layout.BottomBand));
            Third(bot, 0, "MENU", () => Screens.ShowPause(_dir));
            Third(bot, 1, "UNDO", () => { if (_dir.S.Undo()) Sfx.I.Undo(); else Toast("NOTHING TO UNDO"); });
            Third(bot, 2, "HINT", DoHint);

            // DEPTH, PRINTED ON EVERY CELL. Off by default, because brightness IS
            // distance and a number on top of that is a second answer to a question
            // already answered. On, it is the fastest way to learn the ramp — and
            // for anyone who cannot read the ramp at all, it is the only way.
            _depthRoot = UiKit.Rect(_root, "depth", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // THE SAME MOVE AT THE OTHER END. The toast sat 140 units up from the
            // glass, which was the dead plate over the control band; that plate is
            // window now and 140 is under the down fold's arrow. It goes above the
            // coach's line, in the band between the marks and the board — the cube
            // is fitted inside the square with Layout.FitMargin to spare, and
            // LayoutChecks.Stack is what says that band is big enough.
            _toast = UiKit.Label(_aperture, "toast", "", 26, Palette.Ink, TextAnchor.LowerCenter,
                                 new Vector2(0, 0), new Vector2(1, 0),
                                 new Vector2(0, ToastFoot), new Vector2(0, ToastFoot + ToastBox));
            _toast.color = new Color(1, 1, 1, 0);

            // THE CAPTION SITS UNDER THE PLATE CLOCK, NOT UNDER THE TOAST.
            //
            // A toast is the game talking; a caption is the game's SOUND written
            // down, and they are different enough that stacking them in one line
            // would make each look like an interruption of the other. It goes
            // just below the top rule for the same reason the plate clock does:
            // the eyes are on the board, and that is the nearest edge to them.
            _caption = UiKit.Label(_aperture, "caption", "", 19, Palette.Dim, TextAnchor.UpperCenter,
                                   new Vector2(0, 1), new Vector2(1, 1),
                                   new Vector2(0, -(CaptionTop + CaptionHeight)), new Vector2(0, -CaptionTop));
            _caption.color = new Color(1, 1, 1, 0);
        }

        Button FoldButton(string name, int turn, Vector2 anchor, Vector2 seat, Vector2 mark, float spin)
        {
            float H = Access.TapTarget * 0.5f;
            RectTransform slot = UiKit.Rect(_aperture, name, anchor, anchor,
                                            new Vector2(seat.x - H, seat.y - H),
                                            new Vector2(seat.x + H, seat.y + H));

            // The plate is a hit target rather than a picture: the ARROW is the
            // picture, and it is the same arrow the passive tick draws. A bracketed
            // "<" would be a third thing meaning the same as the other two.
            Button b = UiKit.Bracketed(slot, name, "", () =>
            {
                Session s = _dir.S;
                if (s?.lv == null || s.won) return;
                // the same two lines the swipe and the arrow keys both end in, so
                // an assisted fold is the same event and gets the same refusal
                if (!s.TryTurn(turn)) s.RefuseTurn(turn);
            }, 1);

            Transform label = b.transform.Find("label");
            if (label != null) Object.Destroy(label.gameObject);

            // AND THE ARROW SITS WHERE THE PASSIVE ONE SITS, which is not the
            // middle of its own plate. The plate is eighty-eight units of thumb
            // and has to stay inside the housing; the arrow is a mark on the
            // stroke and belongs against it. Offsetting the glyph by the
            // difference puts the two modes' arrows at the same point on the
            // screen, so turning assist on moves a hit target and nothing the
            // player is looking at.
            Image glyph = UiKit.Icon((RectTransform)b.transform, "arrow", Palette.Rust,
                                     FoldIcon, new Vector2(0.5f, 0.5f), mark - seat);
            glyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, spin);
            return b;
        }

        // The turn indices are InputRouter's: 0 left, 1 right, 2 up, 3 down. One
        // table, so the passive mark and the pressable one cannot drift apart.
        static readonly Vector2[] FoldAnchor =
            { new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 1), new Vector2(0.5f, 0) };

        /// <summary>The fold arrow's box, in canvas units. The glyph draws to its edges.</summary>
        public const float FoldIcon = 30f;

        /// <summary>
        /// THE FIRST CLEAR UNIT INSIDE THE STROKE, at either end.
        ///
        /// Four lines used to live in the dead plate outside the window — the
        /// plate clock and the sound caption above it, the toast and the coach's
        /// line below — and there is no dead plate any more. So they come inside,
        /// and inside starts past the fold mark: the arrow's centre is FoldEdge in
        /// and its glyph reaches half a box either side of that. Plus eight, which
        /// is the air between a mark and a word.
        ///
        /// MEASURED FROM THE STROKE RATHER THAN FROM THE GLASS, and hung on the
        /// aperture rather than on the root, so the whole stack travels with the
        /// window when the display turns or the bands change height. The numbers
        /// under it were glass-relative literals, which is the same second
        /// definition of the window that the stroke itself used to have.
        /// </summary>
        public const float MarkBand = FoldEdge + FoldIcon * 0.5f + 8f;

        /// <summary>The plate clock's line, and the caption's under it.</summary>
        public const float ClockHeight = 24f, CaptionGap = 8f, CaptionHeight = 36f;
        public const float CaptionTop = MarkBand + ClockHeight + CaptionGap;

        /// <summary>
        /// The toast's foot, up from the stroke: past the coach's line, which is
        /// the one thing between it and the down fold's arrow. The box above it is
        /// slack for a second line — the text is set from the BOTTOM.
        /// </summary>
        public const float ToastFoot = MarkBand + Coach.LineHeight + 8f, ToastBox = 80f;

        /// <summary>
        /// HOW FAR THE ARROW'S CENTRE STANDS IN FROM THE APERTURE'S EDGE.
        ///
        /// It was half a tap target — forty-four units — because the mark and the
        /// pressable plate were one position, and the plate is what has to fit.
        /// That put the arrow twenty-nine clear units inside the stroke, floating
        /// in the window rather than sitting on the frame, and reading as four
        /// things beside the board instead of four marks on it.
        ///
        /// Half the glyph plus six, so the arrow's tip stands off the stroke by
        /// the width of the stroke and no more. The plate keeps its own number —
        /// see FoldSlot — and the glyph is offset back out to here inside it.
        /// </summary>
        public const float FoldEdge = FoldIcon * 0.5f + 6f;

        /// <summary>Where the arrow is drawn: against the inside of the stroke, at each edge.</summary>
        static readonly Vector2[] FoldMark =
        {
            new Vector2(FoldEdge, 0), new Vector2(-FoldEdge, 0),
            new Vector2(0, -FoldEdge), new Vector2(0, FoldEdge)
        };

        /// <summary>
        /// Where the assisted fold's HIT PLATE sits: in from the aperture's edge by
        /// half a tap target, so an eighty-eight unit target is entirely inside the
        /// housing rather than half of it on the bezel.
        /// </summary>
        static readonly Vector2[] FoldSlot =
        {
            new Vector2(Access.TapTarget * 0.5f, 0), new Vector2(-Access.TapTarget * 0.5f, 0),
            new Vector2(0, -Access.TapTarget * 0.5f), new Vector2(0, Access.TapTarget * 0.5f)
        };
        static readonly float[] FoldSpin = { 90f, -90f, 0f, 180f };

        Image Tick(string name, Vector2 anchor, Vector2 offset, float spin)
        {
            Image i = UiKit.Icon(_aperture, "arrow", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.30f),
                                 FoldIcon, anchor, offset);
            i.gameObject.name = name;
            // the glyph is drawn pointing up; every other direction is that one turned
            i.rectTransform.localEulerAngles = new Vector3(0f, 0f, spin);
            return i;
        }

        void Third(RectTransform parent, int i, string label, System.Action act)
        {
            // The band is 128 tall and these were inset 22 top and bottom, which
            // leaves 84 — four units under the target, on the three controls that
            // are pressed more than everything else in the game put together.
            const float Inset = (128f - Access.TapTarget) * 0.5f;
            RectTransform slot = UiKit.Rect(parent, label, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                                            new Vector2(10, Inset), new Vector2(-10, -Inset));
            UiKit.Bracketed(slot, label, label, act);
        }

        void DoHint()
        {
            if (_dir.S.Hint(out Act a))
            {
                Toast(a.isTurn
                    ? "FOLD " + Turns.All[a.dir].id.ToUpperInvariant()
                    : "STEP " + Turns.All[a.dir].id.ToUpperInvariant());
                Refresh(_dir.S);
            }
            else Toast(_dir.S.hintsLeft <= 0 ? "NO HINTS LEFT" : "NO ROUTE FROM HERE");
        }

        // ---- state ----------------------------------------------------------

        public void SetVisible(bool on) => _root.gameObject.SetActive(on);

        /// <summary>
        /// A FOLD THAT IS NOT A SWIPE.
        ///
        /// Both of this game's gestures are two-part — travel far enough, or
        /// stay still long enough — and neither is available to somebody using a
        /// head pointer, a switch, or one unsteady finger. There is no difficulty
        /// argument for the swipe either: the same four folds have been on the
        /// arrow keys since the first build, for anybody who happens to open this
        /// on a desktop. This is that keyboard, for a thumb.
        ///
        /// They sit ON the four ticks, which is the one place in the interface
        /// that already means "this is the fold in this direction" — so the
        /// buttons appear where the answer to "can I fold that way" already was,
        /// rather than as a fifth thing to learn.
        /// </summary>
        public void RefreshAssist()
        {
            bool on = Access.Assist;
            for (int i = 0; i < _foldBtns.Length; i++)
            {
                if (_foldBtns[i] != null && _foldBtns[i].gameObject.activeSelf != on)
                    _foldBtns[i].gameObject.SetActive(on);
                // the pressable arrow replaces the passive one rather than landing
                // on top of it — see the note where the four are built
                if (_ticks[i] != null && _ticks[i].gameObject.activeSelf == on)
                    _ticks[i].gameObject.SetActive(!on);
            }
        }

        /// <summary>
        /// THE WINDOW MOVES WHEN THE DISPLAY CHANGES SHAPE, and until it was a
        /// square it did not have to: four constant insets are the same four
        /// constants in any aspect. A square cut out of the glass is not — its
        /// side is the shorter of two things that both changed — so the one rect
        /// every fold mark hangs on has to be re-cut when the device turns.
        /// </summary>
        public void Relayout()
        {
            if (_aperture == null) return;
            Layout.ApertureInsets(out float l, out float b, out float r, out float t);
            _aperture.offsetMin = new Vector2(l, b);
            _aperture.offsetMax = new Vector2(-r, -t);
        }

        public void Toast(string msg)
        {
            _toast.text = msg.ToUpperInvariant();
            _toastT = 1.6f;
        }

        /// <summary>
        /// THE FLASH IS THE ONE EFFECT WITH A SEIZURE IN IT, and the game cannot
        /// promise 2.3.1 by being tasteful — it can only promise it by counting.
        ///
        /// Every flash here is raised by something the PLAYER did: a fold, a
        /// node, a lock, a plate. Nothing in the design limits how fast those can
        /// happen and nothing should, so on a small cube a quick hand can put
        /// four or five full-screen luminance changes inside one second without
        /// the game having done anything wrong. So the last few are remembered
        /// and a fourth inside a second is simply not raised: the event still
        /// gets its sound, its haptic, its shake and its debris, and loses only
        /// the one channel that was over budget.
        ///
        /// The window is the real clock, not the bent one — a hitstop must not
        /// buy back a flash.
        /// </summary>
        public void Flash(Color c, float amount, float dur = 0.5f)
        {
            amount *= Access.LightAmount;
            if (amount <= 0.001f) return;

            float now = Time.realtimeSinceStartup;
            int recent = 0;
            for (int i = 0; i < _flashAt.Length; i++) if (now - _flashAt[i] < 1f) recent++;
            if (recent >= Access.FlashesPerSecond) return;

            _flashAt[_flashIdx] = now;
            _flashIdx = (_flashIdx + 1) % _flashAt.Length;

            _flashCol = c;
            _flashT = amount;
            _flashDur = dur;
        }

        /// <summary>
        /// THE OTHER FULL-SCREEN EFFECT, AND THE ONE THAT WAS NOT GATED.
        ///
        /// Flash is careful: it scales by the light setting, it refuses a fourth
        /// inside a second, and NONE turns it off outright. This was one line
        /// with no gate on it at all — so a player who had set the light channel
        /// to NONE, which is the setting a photosensitive player is told to use,
        /// still got the whole screen pulsing to forty-two per cent black on
        /// every refused fold and every dead end. The setting said none and
        /// something was still flashing.
        ///
        /// It is scaled rather than switched, like Flash, so REDUCED gets a
        /// subtler one instead of a cliff.
        ///
        /// NOT IN THE THREE-PER-SECOND BUDGET, and that is a judgement rather
        /// than an oversight. 2.3.1's threshold is a PAIR of opposing luminance
        /// changes of at least a tenth of maximum; this is a darkening, on a
        /// screen that is already nearly black, so the excursion is small and
        /// downward. Sharing the budget would mean a burst of refusals could
        /// spend the flash allowance on vignettes and leave a genuine flash
        /// unable to fire, which trades a real signal for a theoretical one.
        ///
        /// Nothing is lost by gating it: every event that raises a vignette also
        /// raises a sound, a caption and the orb's mood, so this was never the
        /// only channel carrying the news.
        /// </summary>
        public void Vignette(float amount)
        {
            amount *= Access.LightAmount;
            if (amount <= 0.001f) return;
            _vig = Mathf.Max(_vig, amount);
        }

        /// <summary>
        /// The words for a sound that has just played. Short-lived, and it
        /// REPLACES rather than queues: two cues inside a second are two things
        /// that just happened, and a caption still showing the first of them is
        /// telling the player about the past.
        /// </summary>
        public void Caption(string words)
        {
            _caption.text = words;
            _capT = 1.1f;
        }

        // WHAT THE CHIPS ARE SHOWING, so that a frame in which nothing changed
        // costs nothing. Refresh is called from Tick, which is every frame, and
        // it used to rebuild eight strings each time — a caption that is three
        // concatenations, four counters, a name. uGUI compares the string before
        // it rebuilds the mesh, so the canvas was safe; the GARBAGE was not, and
        // a collection landing in the middle of a fold is the one hitch this
        // game cannot afford.
        //
        // Sentinels start at a value nothing can be, so the first Refresh always
        // writes. -1 is impossible for every counter here and _lastBand is only
        // consulted after the daily/forge case has been ruled out.
        int _lastTurns = -1, _lastPar = -1, _lastHeld = -1, _lastKeys = -1;
        int _lastHints = -1, _lastLvNo = -1, _lastBand = -1;
        bool _lastNamed;
        string _lastName = null;

        public void Refresh(Session s)
        {
            if (s?.lv == null) return;

            if (s.turns != _lastTurns) { _lastTurns = s.turns; _foldN.text = Num.Of(s.turns); }
            if (s.lv.par != _lastPar) { _lastPar = s.lv.par; _parN.text = Num.Over(s.lv.par); }

            bool hasKeys = s.lv.keys.Count > 0;
            _keyChip.gameObject.SetActive(hasKeys);
            if (hasKeys)
            {
                int held = 0;
                for (int i = 0; i < s.lv.keys.Count; i++) if ((s.kmask & (1 << i)) != 0) held++;
                if (held != _lastHeld) { _lastHeld = held; _keyN.text = Num.Of(held); }
                if (s.lv.keys.Count != _lastKeys) { _lastKeys = s.lv.keys.Count; _keyTot.text = Num.Over(s.lv.keys.Count); }
            }

            if (s.hintsLeft != _lastHints) { _lastHints = s.hintsLeft; _hintN.text = Num.Of(s.hintsLeft); }

            // THE CAPTION IS THE EXPENSIVE ONE — three concatenations and a roman
            // numeral, every frame, to produce a string that changes once a vault.
            // It is keyed on the two things that can change it and nothing else.
            bool named = !(s.IsDaily || s.IsMade);
            int band = named ? Vaults.VaultOf(s.levelNo) : -1;
            if (s.levelNo != _lastLvNo || named != _lastNamed)
            {
                _lastLvNo = s.levelNo;
                _lastNamed = named;
                _lvNo.text = s.IsDaily ? "DAILY" : s.IsMade ? "FORGE" : Num.Of(s.levelNo);
            }
            if (band != _lastBand)
            {
                _lastBand = band;
                _vault.text = named ? "VAULT " + Vaults.RomanOf(band) + " · " + Vaults.VaultName(band) : "";
            }

            // reference equality first: the level's name is the same object from
            // one frame to the next, so this is a pointer compare in the case
            // that happens every frame and a string compare only when it changed
            string nm = s.lv.name;
            if (!ReferenceEquals(nm, _lastName)) { _lastName = nm; _lvName.text = nm ?? ""; }

            RefreshTicks(s);
            PaintGo(s);
        }

        void RefreshTicks(Session s)
        {
            for (int t = 0; t < 4; t++)
            {
                bool ok = s.CanTurn(t);
                _ticks[t].color = new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, ok ? 0.85f : 0.14f);

                // A CONTROL THAT CANNOT BE USED SAYS SO BEFORE IT IS PRESSED.
                //
                // The passive arrow already answered "does this fold have footing"
                // by going nearly out; the pressable one has to answer it too, or
                // the assisted player is the only one who has to find out by being
                // refused. Set only on the change, because this runs every frame.
                Button b = _foldBtns[t];
                if (b != null && b.interactable != ok) b.interactable = ok;
            }
        }

        void PaintGo(Session s)
        {
            _goChip.gameObject.SetActive(Store.Data.togo != 0 && !s.won);
            if (Store.Data.togo == 0 || s.won) return;

            var (text, slip, dead, thinking) = s.Remain.Pill(s);
            _goN.text = text;

            // A stale number must not be judged: folds have already gone up and the
            // count has not come down yet, so the verdict freezes at its last true
            // value until the answer catches up.
            if (thinking && s.Remain.Value != -2)
            {
                _goN.color = new Color(_goN.color.r, _goN.color.g, _goN.color.b, 0.45f);
                return;
            }

            _goN.color = dead ? Palette.Fault : slip ? Palette.Lock : Palette.Arc;
            _goShown = text;
        }

        public void Tick(float dt, Session s)
        {
            if (_toastT > 0f)
            {
                _toastT -= dt;
                float a = Mathf.Clamp01(_toastT / 0.4f);
                _toast.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, a);
            }

            if (_capT > 0f)
            {
                _capT -= dt;
                float a = Mathf.Clamp01(_capT / 0.3f);
                _caption.color = new Color(Palette.Dim.r, Palette.Dim.g, Palette.Dim.b, a);
            }

            if (_flashT > 0f)
            {
                _flashT = Mathf.Max(0f, _flashT - dt / Mathf.Max(0.05f, _flashDur));
                _flash.color = new Color(_flashCol.r, _flashCol.g, _flashCol.b, _flashT * 0.6f);
            }

            _vig = Mathf.Max(0f, _vig - dt * 1.8f);
            _vignette.color = new Color(0, 0, 0, _vig * 0.42f);

            // the plate clock, draining
            bool plate = s.world != 0 && s.plateT > 0f;
            if (_plateBar.gameObject.activeSelf != plate) _plateBar.gameObject.SetActive(plate);
            if (plate)
            {
                float k = s.plateT / Session.PlateMs;
                _plateBar.rectTransform.anchorMax = new Vector2(k, 1);
                // ONE STRING A SECOND, NOT ONE A FRAME. The clock counts whole
                // seconds and there are five of them, so the concatenation ran
                // sixty times for every value it produced.
                int secs = Mathf.CeilToInt(s.plateT / 1000f);
                if (secs != _lastSecs) { _lastSecs = secs; _plateClock.text = Num.Of(secs) + "S"; }
            }
            else if (_lastSecs != -1) { _lastSecs = -1; _plateClock.text = ""; }

            Refresh(s);
        }

        /// <summary>
        /// One number per surface cell, placed where the cell actually is on
        /// screen. The labels are pooled and only ever repositioned, because this
        /// runs every frame while it is on.
        /// </summary>
        /// <summary>
        /// The coach rides with the depth pass because it needs the same two
        /// things — the camera and the view — to put a mark on a square of the
        /// board, and because both are the parts of the readout that are about
        /// the CELLS rather than about the run.
        /// </summary>
        public void TickCoach(Session s, Camera cam, CubeView view, float dt)
            => Coach?.Tick(s, cam, view, dt);

        public void TickDepth(Session s, Camera cam, CubeView view)
        {
            bool on = Store.Data.depth != 0 && s?.lv != null && !s.won;
            if (!on)
            {
                if (_depthRoot.gameObject.activeSelf) _depthRoot.gameObject.SetActive(false);
                return;
            }
            if (!_depthRoot.gameObject.activeSelf) _depthRoot.gameObject.SetActive(true);

            int n = s.N, want = n * n, used = 0;
            while (_depthPool.Count < want)
                _depthPool.Add(UiKit.Label(_depthRoot, "d" + _depthPool.Count, "", 21, Palette.Ink,
                                           TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                                           new Vector2(-24, -12), new Vector2(24, 12)));

            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                {
                    Surf sc = s.surf[u * n + v];
                    if (!sc.has) continue;
                    Text t = _depthPool[used++];
                    // EIGHTY-ONE OF THESE, EVERY FRAME, on a nine-cube. A depth is
                    // 0..8 by construction, so the table always hits and the
                    // reference it hands back is the one the Text already holds —
                    // which is what makes the assignment free rather than merely
                    // cheap, because uGUI skips the mesh rebuild on a match.
                    t.text = Num.Of(sc.d);
                    // the number takes the cell's own colour, so it never claims to
                    // be a different material from the block it is sitting on
                    Color c = Palette.Tile(sc.t, sc.d, n);
                    t.color = new Color(c.r + 0.35f, c.g + 0.35f, c.b + 0.35f, 0.85f);
                    Vector3 world = view.cube.TransformPoint(CubeMesh.CellToObject(n, sc.w));
                    t.rectTransform.position = cam.WorldToScreenPoint(world);
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                }

            for (int i = used; i < _depthPool.Count; i++)
                if (_depthPool[i].gameObject.activeSelf) _depthPool[i].gameObject.SetActive(false);
        }
    }
}
