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
        Image[] _ticks = new Image[4];
        readonly Button[] _foldBtns = new Button[4];
        RectTransform _root;

        readonly List<Text> _depthPool = new List<Text>();
        RectTransform _depthRoot;

        float _toastT, _flashT, _flashDur, _vig, _capT;
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

            _root = UiKit.Rect(_canvas.transform, "root", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- the flash and the vignette sit under everything -------------
            _flash = UiKit.Panel(_root, "flash", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vignette = UiKit.Panel(_root, "vignette", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

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
            _vault = UiKit.Label(top, "vault", "", 20, Palette.Dim2, TextAnchor.MiddleRight,
                                 new Vector2(0.5f, 0), new Vector2(1, 0.45f), Vector2.zero, new Vector2(-28, 0));

            UiKit.Rule(_root, 1f).rectTransform.anchoredPosition = new Vector2(0, -162);

            // ---- the plate clock ---------------------------------------------
            //
            // The count can be HEARD in the last three seconds, but it also has to
            // be SEEN, and the number cannot go where the eyes are — which is the
            // board. So it is a bar, at the top edge, draining.
            _plateBar = UiKit.Panel(_root, "plateBar", Palette.Rust,
                                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), new Vector2(0, 0));
            _plateClock = UiKit.Label(_root, "plateClock", "", 22, Palette.RustHi, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -166));
            _plateBar.gameObject.SetActive(false);

            // ---- the fold ticks ----------------------------------------------
            //
            // Four marks around the board saying which folds have footing. This is
            // the one piece of the HUD that is about the RULE rather than the run:
            // where you stand decides which folds you have.
            _ticks[0] = Tick("tickL", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, -34), new Vector2(24, 34));
            _ticks[1] = Tick("tickR", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, -34), new Vector2(-18, 34));
            _ticks[2] = Tick("tickU", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-34, -206), new Vector2(34, -200));
            _ticks[3] = Tick("tickD", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-34, 200), new Vector2(34, 206));

            // and the same four, as things you can press — see RefreshAssist.
            // The turn indices are InputRouter's: 0 left, 1 right, 2 up, 3 down.
            float T = Access.TapTarget, H = T * 0.5f;
            _foldBtns[0] = FoldButton("foldL", 0, "<", new Vector2(0, 0.5f), new Vector2(10, -H), new Vector2(10 + T, H));
            _foldBtns[1] = FoldButton("foldR", 1, ">", new Vector2(1, 0.5f), new Vector2(-10 - T, -H), new Vector2(-10, H));
            _foldBtns[2] = FoldButton("foldU", 2, "^", new Vector2(0.5f, 1), new Vector2(-H, -212 - T), new Vector2(H, -212));
            _foldBtns[3] = FoldButton("foldD", 3, "v", new Vector2(0.5f, 0), new Vector2(-H, 212), new Vector2(H, 212 + T));
            RefreshAssist();

            // ---- bottom band ---------------------------------------------------
            RectTransform bot = UiKit.Rect(_root, "barBot", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(0, 0), new Vector2(0, 128));
            Third(bot, 0, "MENU", () => Screens.ShowPause(_dir));
            Third(bot, 1, "UNDO", () => { if (_dir.S.Undo()) Sfx.I.Undo(); else Toast("NOTHING TO UNDO"); });
            Third(bot, 2, "HINT", DoHint);

            // DEPTH, PRINTED ON EVERY CELL. Off by default, because brightness IS
            // distance and a number on top of that is a second answer to a question
            // already answered. On, it is the fastest way to learn the ramp — and
            // for anyone who cannot read the ramp at all, it is the only way.
            _depthRoot = UiKit.Rect(_root, "depth", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _toast = UiKit.Label(_root, "toast", "", 26, Palette.Ink, TextAnchor.LowerCenter,
                                 new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 140), new Vector2(0, 220));
            _toast.color = new Color(1, 1, 1, 0);

            // THE CAPTION SITS UNDER THE PLATE CLOCK, NOT UNDER THE TOAST.
            //
            // A toast is the game talking; a caption is the game's SOUND written
            // down, and they are different enough that stacking them in one line
            // would make each look like an interruption of the other. It goes
            // just below the top rule for the same reason the plate clock does:
            // the eyes are on the board, and that is the nearest edge to them.
            _caption = UiKit.Label(_root, "caption", "", 19, Palette.Dim, TextAnchor.UpperCenter,
                                   new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -232), new Vector2(0, -196));
            _caption.color = new Color(1, 1, 1, 0);
        }

        Button FoldButton(string name, int turn, string arrow, Vector2 anchor, Vector2 oMin, Vector2 oMax)
        {
            RectTransform slot = UiKit.Rect(_root, name, anchor, anchor, oMin, oMax);
            return UiKit.Bracketed(slot, name, arrow, () =>
            {
                Session s = _dir.S;
                if (s?.lv == null || s.won) return;
                // the same two lines the swipe and the arrow keys both end in, so
                // an assisted fold is the same event and gets the same refusal
                if (!s.TryTurn(turn)) s.RefuseTurn(turn);
            }, 26);
        }

        Image Tick(string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            Image i = UiKit.Panel(_root, name, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.30f),
                                  aMin, aMax, oMin, oMax);
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
                if (_foldBtns[i] != null && _foldBtns[i].gameObject.activeSelf != on)
                    _foldBtns[i].gameObject.SetActive(on);
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

        public void Vignette(float amount) => _vig = Mathf.Max(_vig, amount);

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

        public void Refresh(Session s)
        {
            if (s?.lv == null) return;

            _foldN.text = s.turns.ToString();
            _parN.text = "/" + s.lv.par;

            bool hasKeys = s.lv.keys.Count > 0;
            _keyChip.gameObject.SetActive(hasKeys);
            if (hasKeys)
            {
                int held = 0;
                for (int i = 0; i < s.lv.keys.Count; i++) if ((s.kmask & (1 << i)) != 0) held++;
                _keyN.text = held.ToString();
                _keyTot.text = "/" + s.lv.keys.Count;
            }

            _hintN.text = s.hintsLeft.ToString();
            _lvNo.text = s.IsDaily ? "DAILY" : s.IsMade ? "FORGE" : s.levelNo.ToString();
            _lvName.text = s.lv.name ?? "";
            int band = Vaults.VaultOf(s.levelNo);
            _vault.text = s.IsDaily || s.IsMade ? "" : "VAULT " + Vaults.RomanOf(band) + " · " + Vaults.VaultName(band);

            RefreshTicks(s);
            PaintGo(s);
        }

        void RefreshTicks(Session s)
        {
            for (int t = 0; t < 4; t++)
            {
                bool ok = s.CanTurn(t);
                var c = _ticks[t].color;
                _ticks[t].color = new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, ok ? 0.85f : 0.14f);
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
                _plateClock.text = Mathf.CeilToInt(s.plateT / 1000f) + "S";
            }
            else _plateClock.text = "";

            Refresh(s);
        }

        /// <summary>
        /// One number per surface cell, placed where the cell actually is on
        /// screen. The labels are pooled and only ever repositioned, because this
        /// runs every frame while it is on.
        /// </summary>
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
                    t.text = sc.d.ToString();
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
