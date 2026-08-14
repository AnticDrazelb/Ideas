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
    ///
    /// The shipped bar is ONE ROW that fills the width, with elastic hairline
    /// separators between the chips and a context line under it. This port had it
    /// as four panels pinned to fixed fractions of the screen, which is a
    /// different object: the separators are what make four readings read as one
    /// instrument, and a chip that is only as wide as its own contents is what
    /// lets the row stay honest when the fold count goes from 9 to 10.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas _canvas;
        GameDirector _dir;

        Text _turnN, _parN, _keyN, _keyTot, _goN, _hintN, _lvNo, _lvName, _vault, _toast;
        Text _worldName, _worldClock;
        Image _flash, _vignette, _worldBar;
        RectTransform _keyChip, _keyStep, _goChip, _worldTag, _toastBox, _chips, _root;
        Image[] _ticks = new Image[4];

        readonly List<Text> _depthPool = new List<Text>();
        RectTransform _depthRoot;

        float _toastT, _flashT, _flashDur, _vig;
        Color _flashCol;

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
            _root = UiKit.Fill(_canvas.transform, "root");

            // the flash and the vignette sit under everything
            _flash = UiKit.Panel(_root, "flash", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vignette = UiKit.Panel(_root, "vignette", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- #barTop -----------------------------------------------------
            //
            // top:calc(var(--sa-top) + 8px); a column of two lines with a 3 gap,
            // padded 12 either side.
            float chipH = ChipPad * 2f + Mathf.Max(IcSize, NSize);
            float ctxH = Css.TFine * 1.6f;
            RectTransform top = UiKit.Rect(_root, "barTop", new Vector2(0f, 1f), new Vector2(1f, 1f),
                                           new Vector2(12f, -(8f + chipH + 3f + ctxH)), new Vector2(-12f, -8f));

            _chips = UiKit.Rect(top, "chips", new Vector2(0f, 1f), new Vector2(1f, 1f),
                                new Vector2(0f, -chipH), Vector2.zero);

            // FOUR READINGS, FOUR INSTRUMENTS, each in the colour of the thing it
            // counts — the same colour that thing is on the board.
            RectTransform turn = Chip("turnPill", Palette.Rust, Art.Chip("turn", Art.IcTurn));
            _turnN = ChipN(turn, "0", Palette.Ink);
            _parN = ChipSub(turn, "/0");

            Csep("sep0");

            _keyChip = Chip("keyPill", Palette.Node, Art.Chip("node", Art.IcNode));
            _keyN = ChipN(_keyChip, "0", Palette.Ink);
            _keyTot = ChipSub(_keyChip, "/0");
            _keyStep = Csep("sepKey");

            _goChip = Chip("goPill", Palette.Arc, Art.Chip("go", Art.IcGo));
            ChipLbl(_goChip, "TO GO");
            _goN = ChipN(_goChip, "·", Palette.Ink);

            Csep("sep2");

            RectTransform hint = Chip("hintPill", Palette.Lock, Art.Chip("hint", Art.IcHint));
            _hintN = ChipN(hint, "3", Palette.Ink);

            // ---- #hudCtx -----------------------------------------------------
            //
            // The cube's number and name in a pill on the left, and the vault it
            // belongs to pushed to the right by `margin-left:auto`.
            RectTransform ctx = UiKit.Rect(top, "hudCtx", new Vector2(0f, 0f), new Vector2(1f, 0f),
                                           Vector2.zero, new Vector2(0f, ctxH));
            RectTransform lvPill = UiKit.Rect(ctx, "lvName", new Vector2(0f, 0f), new Vector2(0.62f, 1f),
                                              Vector2.zero, Vector2.zero);
            _lvNo = UiKit.Label(lvPill, "no", "—", Css.TMicro, Palette.Dim2, TextAnchor.MiddleLeft, 0.06f,
                                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(12f, 0f), new Vector2(52f, 0f));
            _lvName = UiKit.Label(lvPill, "txt", "—", Css.TFine, Palette.Dim, TextAnchor.MiddleLeft, 0.055f,
                                  new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(60f, 0f), Vector2.zero);
            _vault = UiKit.Label(ctx, "hudVault", "", Css.TFine, Palette.Dim, TextAnchor.MiddleRight, 0.16f,
                                 new Vector2(0.62f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // ---- #worldTag ---------------------------------------------------
            //
            // The plate's clock. The count can be HEARD in the last three seconds,
            // but it also has to be SEEN, and the number cannot go where the eyes
            // are — which is the board. So it is a tag under the readout with a bar
            // draining along its bottom edge.
            _worldTag = UiKit.Rect(_root, "worldTag", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   Vector2.zero, Vector2.zero);
            _worldTag.pivot = new Vector2(0.5f, 1f);
            _worldTag.sizeDelta = new Vector2(220f, Css.TMicro + 10f);
            _worldTag.anchoredPosition = new Vector2(0f, -(8f + Css.HRead + 10f));
            UiKit.Edge(_worldTag, Palette.Rust, Css.RChip, Css.Hair);
            _worldName = UiKit.Label(_worldTag, "name", "", Css.TMicro, Palette.Rust, TextAnchor.MiddleCenter, 0.22f,
                                     new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(14f, 0f), Vector2.zero);
            _worldClock = UiKit.Label(_worldTag, "clock", "", Css.TMicro, Palette.Rust, TextAnchor.MiddleLeft, 0.22f,
                                      new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(11f, 0f), new Vector2(-14f, 0f));
            _worldBar = UiKit.Panel(_worldTag, "wbar", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.5f),
                                    new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));
            _worldTag.gameObject.SetActive(false);

            // ---- #dpad -------------------------------------------------------
            //
            // Four marks saying which folds have footing. This is the one piece of
            // the HUD that is about the RULE rather than the run: where you stand
            // decides which folds you have. They sit together as a row above the
            // controls rather than scattered around the board, because four ticks
            // in a line can be compared and four ticks at the compass points have
            // to be hunted for.
            const float TickW = 26f, TickH = 5f, TickGap = 7f;
            RectTransform dpad = UiKit.Rect(_root, "dpad", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                            Vector2.zero, Vector2.zero);
            dpad.pivot = new Vector2(0.5f, 0f);
            dpad.sizeDelta = new Vector2(TickW * 4f + TickGap * 3f, TickH);
            dpad.anchoredPosition = new Vector2(0f, 12f + Css.HCtl + 14f);

            // the shipped order across the row is L, D, U, R
            int[] order = { 0, 3, 2, 1 };
            for (int i = 0; i < 4; i++)
            {
                RectTransform t = UiKit.Rect(dpad, "tick" + i, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                             Vector2.zero, Vector2.zero);
                t.sizeDelta = new Vector2(TickW, TickH);
                t.anchoredPosition = new Vector2(i * (TickW + TickGap) + TickW * 0.5f, 0f);
                var img = t.gameObject.AddComponent<Image>();
                img.sprite = UiKit.Frame(3f, 0f);
                img.type = Image.Type.Sliced;
                img.color = Css.Edge;
                img.raycastTarget = false;
                _ticks[order[i]] = img;
            }

            // ---- #barBot -----------------------------------------------------
            RectTransform bot = UiKit.Rect(_root, "barBot", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                           Vector2.zero, Vector2.zero);
            bot.pivot = new Vector2(0.5f, 0f);
            bot.anchoredPosition = new Vector2(0f, 12f);

            const float Gap = 12f;
            float pw = Mathf.Max(Css.HCtl * 2.1f, 96f);
            bot.sizeDelta = new Vector2(pw * 3f + Gap * 2f, Css.HCtl);
            BotPill(bot, 0, pw, Gap, "[ MENU ]", Palette.Dim, () => Screens.ShowPause(_dir));
            BotPill(bot, 1, pw, Gap, "[ UNDO ]", Palette.Rust,
                    () => { if (_dir.S.Undo()) Sfx.I.Undo(); else Toast("NOTHING TO UNDO"); });
            BotPill(bot, 2, pw, Gap, "[ HINT ]", Palette.Dim, DoHint);

            // DEPTH, PRINTED ON EVERY CELL. Off by default, because brightness IS
            // distance and a number on top of that is a second answer to a question
            // already answered. On, it is the fastest way to learn the ramp — and
            // for anyone who cannot read the ramp at all, it is the only way.
            _depthRoot = UiKit.Fill(_root, "depth");

            // ---- #toast ------------------------------------------------------
            _toastBox = UiKit.Rect(_root, "toast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   Vector2.zero, Vector2.zero);
            _toastBox.pivot = new Vector2(0.5f, 0f);
            _toastBox.sizeDelta = new Vector2(300f, Css.TFine + 20f);
            _toastBox.anchoredPosition = new Vector2(0f, 120f);
            var tp = _toastBox.gameObject.AddComponent<Image>();
            tp.sprite = UiKit.Frame(Css.RCtl, 0f);
            tp.type = Image.Type.Sliced;
            tp.color = Palette.PanelHi;
            tp.raycastTarget = false;
            UiKit.Edge(_toastBox, Css.Edge, Css.RCtl, Css.Hair);
            _toast = UiKit.Type(_toastBox, "t", "", Css.TFine, Palette.Rust, TextAnchor.MiddleCenter, 0.16f);
            _toastBox.gameObject.SetActive(false);
        }

        // ---- the chip row ---------------------------------------------------
        //
        // Every measurement here is `clamp(a, Nvw, b) * var(--hud-k)`, where hud-k
        // is the shrink the script applies when the row will not fit. The clamps
        // are the sheet's; the shrink is applied in Fit().

        static float IcSize => Mathf.Clamp(Css.Vw(4.6f), 15f, 21f);
        static float NSize => Mathf.Clamp(Css.Vw(5.6f), 17f, 26f);
        static float SubSize => Mathf.Clamp(Css.Vw(3.6f), 12f, 17f);
        static float LblSize => Mathf.Clamp(Css.Vw(3.0f), 10f, 14f);
        static float ChipPad => Mathf.Clamp(Css.Vw(2.4f), 7f, 12f);
        static float ChipPadX => Mathf.Clamp(Css.Vw(3.3f), 9f, 16f);
        static float ChipGap => Mathf.Clamp(Css.Vw(1.9f), 5f, 9f);
        static float SepMargin => Mathf.Clamp(Css.Vw(1.6f), 3f, 9f);

        readonly List<RectTransform> _rowItems = new List<RectTransform>();
        readonly List<float> _rowWidths = new List<float>();
        readonly List<bool> _rowElastic = new List<bool>();

        RectTransform Chip(string name, Color hue, Sprite icon)
        {
            RectTransform rt = UiKit.Rect(_chips, name, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0f, 0.5f);

            // box-shadow:inset 0 0 0 var(--rule) currentColor — the chip's border is
            // its own colour, which is what makes the row read as four instruments
            // rather than four labels
            UiKit.Edge(rt, hue, Css.RChip, Css.Rule);
            UiKit.Mark(rt, "ic", icon, hue, IcSize, IcSize, new Vector2(0f, 0.5f),
                       new Vector2(ChipPadX + IcSize * 0.5f, 0f));

            _rowItems.Add(rt);
            _rowWidths.Add(0f);
            _rowElastic.Add(false);
            return rt;
        }

        Text ChipN(RectTransform chip, string s, Color c)
            => UiKit.Label(chip, "n", s, NSize, c, TextAnchor.MiddleLeft, 0f,
                           new Vector2(0f, 0f), new Vector2(1f, 1f),
                           new Vector2(ChipPadX + IcSize + ChipGap, 0f), new Vector2(-ChipPadX, 0f));

        Text ChipSub(RectTransform chip, string s)
            => UiKit.Label(chip, "sub2", s, SubSize, Palette.Dim, TextAnchor.MiddleRight, 0f,
                           new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-ChipPadX, 0f));

        Text ChipLbl(RectTransform chip, string s)
            => UiKit.Label(chip, "lbl2", s, LblSize, Palette.Dim, TextAnchor.MiddleLeft, 0.12f,
                           new Vector2(0f, 0f), new Vector2(1f, 1f),
                           new Vector2(ChipPadX + IcSize + ChipGap, 0f), Vector2.zero);

        /// <summary>
        /// .csep — `flex:1 1 auto; height:1px; background:var(--edge-2)`. It is a
        /// hairline that takes whatever width is going, which is what closes the
        /// gaps between chips into one continuous readout.
        /// </summary>
        RectTransform Csep(string name)
        {
            RectTransform rt = UiKit.Rect(_chips, name, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                          Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 1f);
            UiKit.Panel(rt, "l", Css.Edge2, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _rowItems.Add(rt);
            _rowWidths.Add(0f);
            _rowElastic.Add(true);
            return rt;
        }

        void BotPill(RectTransform bar, int i, float w, float gap, string label, Color tint, System.Action act)
        {
            RectTransform slot = UiKit.Rect(bar, label, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                            Vector2.zero, Vector2.zero);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.sizeDelta = new Vector2(w, 0f);
            slot.anchoredPosition = new Vector2(i * (w + gap), 0f);
            UiKit.PillBtn(slot, label, act, tint, Css.TFine, 0.16f);
            // #barBot .pill carries the heavier rule and a seat, unlike a bare pill
            UiKit.Seat(slot, new Color(0f, 0f, 0f, 0.5f), Css.RCtl, Css.Rule);
        }

        /// <summary>
        /// LAY THE ROW OUT, THEN SHRINK IT UNTIL IT FITS.
        ///
        /// The shipped build measures the row and steps `--hud-k` down in
        /// twentieths until it stops overflowing, dropping the TO GO label on the
        /// way — "a fifth off the type is far cheaper than a label vanishing and
        /// reappearing as the fold count ticks over". This is the same idea with
        /// the measurement done directly: the chips are as wide as their contents,
        /// the separators take the rest, and if there is no rest the chips give
        /// back proportionally.
        /// </summary>
        void Fit()
        {
            float avail = _chips.rect.width;
            if (avail <= 1f) return;

            float fixedW = 0f;
            int elastic = 0;
            for (int i = 0; i < _rowItems.Count; i++)
            {
                if (!_rowItems[i].gameObject.activeSelf) continue;
                if (_rowElastic[i]) { elastic++; continue; }
                float w = ChipWidth(_rowItems[i]);
                _rowWidths[i] = w;
                fixedW += w;
            }

            float slack = avail - fixedW - elastic * SepMargin * 2f;
            float k = slack < 0f && fixedW > 0f ? Mathf.Max(0.72f, (avail - elastic * SepMargin * 2f) / fixedW) : 1f;
            float sep = elastic > 0 ? Mathf.Max(0f, slack) / elastic : 0f;

            float x = 0f;
            for (int i = 0; i < _rowItems.Count; i++)
            {
                RectTransform rt = _rowItems[i];
                if (!rt.gameObject.activeSelf) continue;
                if (_rowElastic[i])
                {
                    rt.sizeDelta = new Vector2(sep, 1f);
                    rt.anchoredPosition = new Vector2(x + SepMargin, 0f);
                    x += sep + SepMargin * 2f;
                }
                else
                {
                    float w = _rowWidths[i] * k;
                    rt.sizeDelta = new Vector2(w, 0f);
                    rt.anchoredPosition = new Vector2(x, 0f);
                    rt.localScale = new Vector3(1f, k < 1f ? k : 1f, 1f);
                    x += w;
                }
            }
        }

        float ChipWidth(RectTransform chip)
        {
            float w = ChipPadX * 2f + IcSize + ChipGap;
            var n = chip.Find("n_t");
            var sub = chip.Find("sub2_t");
            var lbl = chip.Find("lbl2_t");
            if (lbl != null) w += lbl.GetComponent<Text>().preferredWidth + ChipGap;
            if (n != null) w += n.GetComponent<Text>().preferredWidth;
            if (sub != null) w += sub.GetComponent<Text>().preferredWidth;
            return w;
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

        public void Toast(string msg)
        {
            _toast.text = msg.ToUpperInvariant();
            _toastBox.sizeDelta = new Vector2(_toast.preferredWidth + 32f + msg.Length * Css.TFine * 0.16f,
                                              Css.TFine + 20f);
            _toastBox.gameObject.SetActive(true);
            _toastT = 1.6f;
        }

        public void Flash(Color c, float amount, float dur = 0.5f)
        {
            _flashCol = c;
            _flashT = amount;
            _flashDur = dur;
        }

        public void Vignette(float amount) => _vig = Mathf.Max(_vig, amount);

        public void Refresh(Session s)
        {
            if (s?.lv == null) return;

            _turnN.text = s.turns.ToString();
            _parN.text = "/" + s.lv.par;
            // #turnPill.over{ color:var(--fault) }
            _turnN.color = s.turns > s.lv.par ? Palette.Fault : Palette.Ink;

            bool hasKeys = s.lv.keys.Count > 0;
            if (_keyChip.gameObject.activeSelf != hasKeys)
            {
                _keyChip.gameObject.SetActive(hasKeys);
                _keyStep.gameObject.SetActive(hasKeys);
            }
            if (hasKeys)
            {
                int held = 0;
                for (int i = 0; i < s.lv.keys.Count; i++) if ((s.kmask & (1 << i)) != 0) held++;
                _keyN.text = held.ToString();
                _keyTot.text = "/" + s.lv.keys.Count;
            }

            _hintN.text = s.hintsLeft.ToString();
            _lvNo.text = s.IsDaily ? "DAILY" : s.IsMade ? "FORGE" : s.levelNo.ToString();
            _lvName.text = (s.lv.name ?? "").ToUpperInvariant();
            int band = Vaults.VaultOf(s.levelNo);
            _vault.text = s.IsDaily || s.IsMade ? ""
                        : ("VAULT " + Vaults.RomanOf(band) + " · " + Vaults.VaultName(band)).ToUpperInvariant();

            RefreshTicks(s);
            PaintGo(s);
            Fit();
        }

        void RefreshTicks(Session s)
        {
            for (int t = 0; t < 4; t++)
            {
                bool ok = s.CanTurn(t);
                // .tick.ok is rust at .95; .tick.no is edge-2
                _ticks[t].color = ok
                    ? new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.95f)
                    : Css.Edge2;
            }
        }

        void PaintGo(Session s)
        {
            bool on = Store.Data.togo != 0 && !s.won;
            if (_goChip.gameObject.activeSelf != on) _goChip.gameObject.SetActive(on);
            if (!on) return;

            var (text, slip, dead, thinking) = s.Remain.Pill(s);
            _goN.text = text;

            // A stale number must not be judged: folds have already gone up and the
            // count has not come down yet, so the verdict freezes at its last true
            // value until the answer catches up. #goPill.think is opacity .5.
            if (thinking && s.Remain.Value != -2)
            {
                _goN.color = new Color(_goN.color.r, _goN.color.g, _goN.color.b, 0.5f);
                return;
            }

            // #goPill.slip is rust, #goPill.dead is fault, and otherwise arc
            _goN.color = dead ? Palette.Fault : slip ? Palette.Rust : Palette.Arc;
        }

        public void Tick(float dt, Session s)
        {
            if (_toastT > 0f)
            {
                _toastT -= dt;
                float a = Mathf.Clamp01(_toastT / 0.4f);
                var cg = UiKit.Ensure<CanvasGroup>(_toastBox.gameObject);
                cg.alpha = a;
                if (_toastT <= 0f) _toastBox.gameObject.SetActive(false);
            }

            if (_flashT > 0f)
            {
                _flashT = Mathf.Max(0f, _flashT - dt / Mathf.Max(0.05f, _flashDur));
                _flash.color = new Color(_flashCol.r, _flashCol.g, _flashCol.b, _flashT * 0.6f);
            }

            _vig = Mathf.Max(0f, _vig - dt * 1.8f);
            _vignette.color = new Color(0, 0, 0, _vig * 0.42f);

            // #worldTag — the plate's world and its clock, draining
            bool plate = s.world != 0 && s.plateT > 0f;
            if (_worldTag.gameObject.activeSelf != plate) _worldTag.gameObject.SetActive(plate);
            if (plate)
            {
                float k = s.plateT / Session.PlateMs;
                _worldBar.rectTransform.anchorMax = new Vector2(k, 0f);
                _worldName.text = "INVERTED";
                _worldClock.text = Mathf.CeilToInt(s.plateT / 1000f) + "S";
                // #worldTag.hot — the last three seconds, and the clock goes fault
                bool hot = s.plateT <= 3000f;
                _worldClock.color = hot ? Palette.Fault : Palette.Rust;
            }

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
            {
                RectTransform slot = UiKit.Rect(_depthRoot, "d" + _depthPool.Count,
                                                Vector2.zero, Vector2.zero,
                                                new Vector2(-24f, -12f), new Vector2(24f, 12f));
                _depthPool.Add(UiKit.Type(slot, "d", "", Css.TTiny, Palette.Ink, TextAnchor.MiddleCenter, 0f));
            }

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
                    ((RectTransform)t.transform.parent).position = cam.WorldToScreenPoint(world);
                    if (!t.transform.parent.gameObject.activeSelf) t.transform.parent.gameObject.SetActive(true);
                }

            for (int i = used; i < _depthPool.Count; i++)
                if (_depthPool[i].transform.parent.gameObject.activeSelf)
                    _depthPool[i].transform.parent.gameObject.SetActive(false);
        }
    }
}
