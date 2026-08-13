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
            BuildPause();
            BuildWin();
            HideAll();
        }

        // ---- plumbing -------------------------------------------------------

        static RectTransform Layer(string id)
        {
            RectTransform rt = UiKit.Rect(_canvas.transform, id, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = Palette.Scrim;
            Layers[id] = rt.gameObject;
            return rt;
        }

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

        static RectTransform Column(RectTransform parent, float top, float bottom)
            => UiKit.Rect(parent, "col", new Vector2(0, 0), new Vector2(1, 1),
                          new Vector2(40, bottom), new Vector2(-40, -top));

        static void Row(RectTransform col, int slot, int of, string label, System.Action act, bool primary = false)
        {
            float h = 1f / of;
            RectTransform r = UiKit.Rect(col, label, new Vector2(0, 1 - (slot + 1) * h), new Vector2(1, 1 - slot * h),
                                         new Vector2(0, 8), new Vector2(0, -8));
            UiKit.Bracketed(r, label, label, act, 28, primary);
        }

        // ---- title ----------------------------------------------------------

        static Text _playLabel, _resumeName;

        static void BuildTitle()
        {
            RectTransform L = Layer("title");
            L.GetComponent<Image>().color = new Color(0, 0, 0, 0.86f);

            UiKit.Label(L, "wordmark1", "SINGULARITY", 66, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, -90));
            UiKit.Label(L, "wordmark2", "ENGINE", 66, Palette.Rust, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -252), new Vector2(0, -172));

            _resumeName = UiKit.Label(L, "resume", "", 22, Palette.Dim, TextAnchor.UpperCenter,
                                      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -300), new Vector2(0, -262));

            UiKit.Label(L, "sub",
                "You are a black hole inside a broken machine.\nFold the engine until its circuits align,\nthen collapse into the core.",
                22, Palette.Dim, TextAnchor.UpperCenter,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -420), new Vector2(0, -330));

            RectTransform col = UiKit.Rect(L, "buttons", new Vector2(0, 0), new Vector2(1, 0),
                                           new Vector2(60, 90), new Vector2(-60, 460));
            Row(col, 0, 4, "CONTINUE", () => { Show(null); _dir.Play(Mathf.Max(1, Store.Data.reached)); }, true);
            Row(col, 1, 4, "DAILY", () => { Show(null); _dir.Play(Daily.SpecLevel, LoadKind.Daily); });
            Row(col, 2, 4, "VAULTS", OpenVaults);
            Row(col, 3, 4, "CALIBRATION", () => Show("manual"));

            _playLabel = col.Find("CONTINUE").GetComponentInChildren<Text>();
        }

        public static void ShowTitle()
        {
            int r = Mathf.Max(1, Store.Data.reached);
            if (_playLabel != null) _playLabel.text = r <= 1 ? "[ BEGIN ]" : "[ CONTINUE ]";
            if (_resumeName != null)
            {
                int band = Vaults.VaultOf(r);
                _resumeName.text = r <= 1 ? "" :
                    "VAULT " + Vaults.RomanOf(band) + " · " + Vaults.VaultName(band) + "   ·   " + Vaults.LevelName(r);
            }
            Stack.Clear();
            Show("title");
        }

        // ---- vault select ---------------------------------------------------

        static Text _vaultName;
        static RectTransform _grid;

        static void BuildVaults()
        {
            RectTransform L = Layer("vaults");

            _vaultName = UiKit.Label(L, "vaultName", "VAULT I", 32, Palette.Ink, TextAnchor.MiddleCenter,
                                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(120, -140), new Vector2(-120, -80));

            RectTransform prev = UiKit.Rect(L, "prev", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -140), new Vector2(140, -80));
            UiKit.Bracketed(prev, "prev", "<", () => { _viewBand = Mathf.Max(0, _viewBand - 1); PaintVaults(); }, 26);
            RectTransform next = UiKit.Rect(L, "next", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -140), new Vector2(-40, -80));
            UiKit.Bracketed(next, "next", ">", () => { _viewBand++; PaintVaults(); }, 26);

            _grid = UiKit.Rect(L, "grid", new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 200), new Vector2(-40, -170));

            RectTransform back = UiKit.Rect(L, "back", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(back, "back", "BACK", Back, 28);
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

            for (int i = 0; i < size; i++)
            {
                int level = start + i;
                int cx = i % cols, cy = i / cols;
                RectTransform slot = UiKit.Rect(_grid, "c" + level,
                    new Vector2(cx / (float)cols, 1f - (cy + 1f) / rows),
                    new Vector2((cx + 1f) / cols, 1f - cy / (float)rows),
                    new Vector2(6, 6), new Vector2(-6, -6));

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
            UiKit.Label(L, "h", "CALIBRATION", 40, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -80));

            // NINE LINES, NOT NINE PARAGRAPHS. Nobody reads a manual in a puzzle
            // game — they glance at it, twice, and go back to the cube. So every
            // rule is a LABEL you can read in one saccade and one line under it.
            string[] rows =
            {
                "THE RULE",
                "ONE FACE AT A TIME|Two traces that line up when you fold are connected,\nhowever far apart they look.",
                "THE VERBS",
                "SWIPE TO FOLD|Past halfway it commits. The ticks say which folds have footing.",
                "TAP TO MOVE|The singularity walks to any trace connected to the one under it.",
                "HOLD FOR MATRIX|The lattice goes to glass. Keep holding and drag to turn it.",
                "COLLAPSE INTO THE CORE|Reach it and the vault is solved.",
                "WORTH KNOWING",
                "PLATES INVERT|Every trace goes dead, every dead cell lights up, for five seconds.\nA plate always gives footing, so every fold is legal while you stand on one.",
                "TO GO|The fewest folds still possible from where you stand.",
            };

            float y = -190;
            foreach (string r in rows)
            {
                if (!r.Contains("|"))
                {
                    UiKit.Label(L, r, r, 18, Palette.Rust, TextAnchor.UpperLeft,
                                new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 30), new Vector2(-48, y));
                    y -= 46;
                    continue;
                }
                string[] parts = r.Split('|');
                UiKit.Label(L, parts[0], parts[0], 24, Palette.Ink, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 30), new Vector2(-48, y));
                UiKit.Label(L, parts[0] + "_d", parts[1], 19, Palette.Dim, TextAnchor.UpperLeft,
                            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, y - 92), new Vector2(-48, y - 32));
                y -= 106;
            }

            RectTransform got = UiKit.Rect(L, "got", new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 90), new Vector2(-60, 160));
            UiKit.Bracketed(got, "got", "GOT IT", Back, 28, true);
        }

        // ---- pause ----------------------------------------------------------

        static void BuildPause()
        {
            RectTransform L = Layer("pause");
            UiKit.Label(L, "h", "PAUSED", 44, Palette.Ink, TextAnchor.UpperCenter,
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -180), new Vector2(0, -120));

            RectTransform col = Column(L, 300, 200);
            Row(col, 0, 5, "RESUME", () => Show(null), true);
            Row(col, 1, 5, "RESET", () => { Show(null); _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey); });
            Row(col, 2, 5, "VAULTS", OpenVaults);
            Row(col, 3, 5, "CALIBRATION", () => Show("manual"));
            Row(col, 4, 5, "MENU", ShowTitle);
        }

        public static void ShowPause(GameDirector dir) => Show("pause");

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

            RectTransform col = Column(L, 560, 200);
            RectTransform n = UiKit.Rect(col, "next", new Vector2(0, 0.66f), new Vector2(1, 1), new Vector2(0, 8), new Vector2(0, -8));
            _winNext = UiKit.Bracketed(n, "next", "NEXT", () =>
            {
                Show(null);
                _dir.Play(_dir.S.levelNo + 1);
            }, 30, true);

            RectTransform rt = UiKit.Rect(col, "retry", new Vector2(0, 0.33f), new Vector2(1, 0.66f), new Vector2(0, 8), new Vector2(0, -8));
            _winRetry = UiKit.Bracketed(rt, "retry", "RETRY FOR PAR", () =>
            {
                Show(null);
                _dir.Play(_dir.S.levelNo, _dir.S.kind, _dir.S.madeKey);
            }, 26);

            RectTransform v = UiKit.Rect(col, "vaults", new Vector2(0, 0), new Vector2(1, 0.33f), new Vector2(0, 8), new Vector2(0, -8));
            UiKit.Bracketed(v, "vaults", "VAULTS", OpenVaults, 26);
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
            _winNext.gameObject.SetActive(!s.IsDaily && !s.IsMade);
            _winRetry.gameObject.SetActive(s.turns > s.lv.par);

            Show("win");
        }
    }
}
