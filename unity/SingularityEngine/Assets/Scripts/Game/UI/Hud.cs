using System.Collections;
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

        Text _foldN, _parN, _keyN, _keyTot, _goN, _hintN, _lvNo, _lvName, _vault, _toast, _plateClock;
        Image _keyChip, _goChip, _flash, _vignette, _plateBar;
        Image[] _ticks = new Image[4];
        RectTransform _root;

        float _toastT, _flashT, _flashDur, _vig;
        Color _flashCol;
        string _goShown = "";

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
            _root = UiKit.Rect(_canvas.transform, "root", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- the flash and the vignette sit under everything -------------
            _flash = UiKit.Panel(_root, "flash", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vignette = UiKit.Panel(_root, "vignette", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- top band ----------------------------------------------------
            RectTransform top = UiKit.Rect(_root, "barTop", new Vector2(0, 1), new Vector2(1, 1),
                                           new Vector2(0, -160), new Vector2(0, 0));

            _foldN = UiKit.Label(top, "foldN", "0", 40, Palette.Ink, TextAnchor.MiddleLeft,
                                 new Vector2(0, 0.45f), new Vector2(0, 1), new Vector2(28, 0), new Vector2(120, 0));
            _parN = UiKit.Label(top, "parN", "/0", 26, Palette.Dim, TextAnchor.MiddleLeft,
                                new Vector2(0, 0.45f), new Vector2(0, 1), new Vector2(78, 0), new Vector2(180, 0));

            _keyChip = UiKit.Panel(top, "keyChip", new Color(0, 0, 0, 0),
                                   new Vector2(0, 0.45f), new Vector2(0, 1), new Vector2(190, 0), new Vector2(330, 0));
            _keyN = UiKit.Label(_keyChip.rectTransform, "keyN", "0", 34, Palette.Node, TextAnchor.MiddleLeft,
                                Vector2.zero, Vector2.one, new Vector2(28, 0), Vector2.zero);
            UiKit.Label(_keyChip.rectTransform, "keyIcon", "◆", 26, Palette.Node, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _keyTot = UiKit.Label(_keyChip.rectTransform, "keyTot", "/0", 22, Palette.Dim, TextAnchor.MiddleLeft,
                                  Vector2.zero, Vector2.one, new Vector2(58, 0), Vector2.zero);

            _goChip = UiKit.Panel(top, "goChip", new Color(0, 0, 0, 0),
                                  new Vector2(1, 0.45f), new Vector2(1, 1), new Vector2(-330, 0), new Vector2(-150, 0));
            UiKit.Label(_goChip.rectTransform, "goLbl", "TO GO", 18, Palette.Dim, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _goN = UiKit.Label(_goChip.rectTransform, "goN", "·", 36, Palette.Arc, TextAnchor.MiddleRight,
                               Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _hintN = UiKit.Label(top, "hintN", "3", 28, Palette.Dim, TextAnchor.MiddleRight,
                                 new Vector2(1, 0.45f), new Vector2(1, 1), new Vector2(-120, 0), new Vector2(-28, 0));

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

            // ---- bottom band ---------------------------------------------------
            RectTransform bot = UiKit.Rect(_root, "barBot", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(0, 0), new Vector2(0, 128));
            Third(bot, 0, "MENU", () => Screens.ShowPause(_dir));
            Third(bot, 1, "UNDO", () => { if (!_dir.S.Undo()) Toast("NOTHING TO UNDO"); });
            Third(bot, 2, "HINT", DoHint);

            _toast = UiKit.Label(_root, "toast", "", 26, Palette.Ink, TextAnchor.LowerCenter,
                                 new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 140), new Vector2(0, 220));
            _toast.color = new Color(1, 1, 1, 0);
        }

        Image Tick(string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            Image i = UiKit.Panel(_root, name, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.30f),
                                  aMin, aMax, oMin, oMax);
            return i;
        }

        void Third(RectTransform parent, int i, string label, System.Action act)
        {
            RectTransform slot = UiKit.Rect(parent, label, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                                            new Vector2(10, 22), new Vector2(-10, -22));
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

        public void Toast(string msg)
        {
            _toast.text = msg.ToUpperInvariant();
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
    }
}
