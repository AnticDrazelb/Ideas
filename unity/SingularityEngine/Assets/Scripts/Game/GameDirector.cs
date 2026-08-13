using System.Collections;
using UnityEngine;
using Singularity.Core;
using Singularity.UI;

namespace Singularity.Game
{
    /// <summary>
    /// The thing that owns a run: it loads a cube, pumps the session, and turns
    /// the session's events into light and noise.
    ///
    /// The split is deliberate and it is the whole architecture. Session decides
    /// what is true. This decides what that looks like. Nothing in Session ever
    /// touches a mesh, which is why the rules can be tested with no scene loaded
    /// and why the port could be proved identical to the original before a single
    /// pixel existed.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector I { get; private set; }

        public Session S { get; private set; }
        public CubeView View { get; private set; }
        public CameraRig Rig { get; private set; }
        public Hud Hud { get; private set; }

        InputRouter _input;
        bool _screenUp = true;      // a menu is over the board

        void Awake()
        {
            I = this;
            Store.Load();

            S = new Session();
            Rig = gameObject.AddComponent<CameraRig>();
            Rig.Init();

            View = gameObject.AddComponent<CubeView>();
            View.Init(S);

            _input = new InputRouter(S, View, Rig.cam);

            Hud = Hud.Build(this);

            Wire();
            Screens.Build(this);
            Screens.ShowTitle();
        }

        void Wire()
        {
            S.On.Toast = msg => Hud.Toast(msg);

            S.On.Settled = () =>
            {
                View.Rebuild();
                Rig.Fit(S.N);
                Hud.Refresh(S);
            };

            S.On.Stepped = (cell, surf) => Rig.Shake(0.045f);

            S.On.DoorOpened = (cell, i) =>
            {
                Rig.Kick(5, 0, 1); Rig.Shake(0.34f); Rig.Punch(0.030f);
                Hud.Flash(Palette.Node, 0.30f);
            };

            S.On.KeyTaken = (cell, i) =>
            {
                Rig.Shake(0.20f); Rig.Punch(0.022f);
                Hud.Flash(Palette.Node, 0.26f);
            };

            S.On.FoldLanded = t =>
            {
                // THE LANDING IS THE VERB, and it gets four things at once on
                // purpose: an AIMED kick along the fold's own axis so the shove has
                // a direction, TRAUMA on top so the whole frame is briefly unsafe,
                // a PUNCH of zoom which is what makes it read as a hit rather than
                // a slide, and a square front leaving the cube at board scale.
                float sgn = (t == 1 || t == 2) ? 1f : -1f;
                bool axisX = t < 2;
                Rig.Kick(9, axisX ? -sgn : 0, axisX ? 0 : sgn);
                Rig.Shake(0.62f); Rig.Punch(0.055f); Rig.Squash(axisX, 0.075f);
                Hud.Flash(Palette.TraceNear, 0.20f);
                View.Rebuild();
            };

            S.On.FoldRefused = t =>
            {
                // A REFUSAL IS A COLLISION. More trauma than a successful fold and
                // none of the reward: the frame jolts, the walls close in for a
                // beat, and there is no flash at all. Light means yes in this game,
                // so no has to be told in the dark.
                float sgn = (t == 1 || t == 2) ? 1f : -1f;
                bool axisX = t < 2;
                Rig.Kick(5, axisX ? -sgn : 0, axisX ? 0 : sgn);
                Rig.Shake(0.5f); Rig.Punch(-0.012f); Rig.Squash(axisX, 0.045f);
                Hud.Vignette(1f);
            };

            S.On.Denied = (u, v, col) => { Hud.Vignette(0.55f); Rig.Shake(0.10f); };

            S.On.PlateFired = (cell, bit) =>
            {
                // THE BIGGEST EVENT IN THE GAME, and the only one that changes the
                // board rather than the view of it.
                View.Rebuild(true);
                Rig.Kick(13, 0, 1);
                Rig.Shake(0.95f); Rig.Punch(0.075f); Rig.Squash(true, 0.09f);
                Hud.Flash(Palette.Rust, 0.24f);
                Hud.Toast(S.world != 0 ? "INVERT — FIVE SECONDS" : "RESTORED");
            };

            S.On.Reverted = () => { View.Rebuild(true); Rig.Shake(0.42f); Hud.Flash(Palette.Rust, 0.18f); };
            S.On.ThrownToPlate = cell => { Rig.Shake(0.42f); Rig.Kick(9, 0, 1); };
            S.On.StuckChanged = () => Hud.Vignette(1f);
            S.On.Won = OnWin;
        }

        // ---- loading --------------------------------------------------------

        public void Play(int level, LoadKind how = LoadKind.Vault, string madeKey = null)
        {
            StartCoroutine(PlayRoutine(level, how, madeKey));
        }

        IEnumerator PlayRoutine(int level, LoadKind how, string madeKey)
        {
            // Jumping somewhere far from the cache costs a real mint, so say so
            // rather than dropping a frame and hoping. One paint, then the work.
            bool needsCut = how == LoadKind.Daily
                ? false
                : (how == LoadKind.Vault || how == LoadKind.Practice) && !LevelSupply.Has(level);

            if (needsCut)
            {
                Hud.Toast("CUTTING VAULT " + level + "…");
                yield return null;
                yield return null;
            }

            Level src = how switch
            {
                LoadKind.Daily => Daily.Data(),
                LoadKind.Made => DecodeMade(madeKey),
                _ => LevelSupply.Get(level)
            };

            if (src == null) { Hud.Toast("THAT CUBE IS NO LONGER HERE"); Screens.ShowTitle(); yield break; }

            if (how != LoadKind.Made && how != LoadKind.Daily) src.name = Vaults.LevelName(level);

            S.Load(src, level, how, madeKey);
            View.Rebuild(true);
            Rig.Fit(S.N);
            Hud.Refresh(S);
            SetScreenUp(false);

            // cut the next one while this one is played
            if (how == LoadKind.Vault) LevelSupply.Prebuild(level + 1);
        }

        static Level DecodeMade(string key)
        {
            MadeCube m = Store.Made(key);
            if (m == null) return null;
            Level lv = ShareCode.Decode(m.code);
            if (lv == null) return null;
            lv.par = m.par;
            lv.name = m.name;
            return lv;
        }

        void OnWin()
        {
            int folds = S.turns;
            long ms = SolveClock.Ms;

            if (S.IsDaily)
            {
                int today = Daily.DayIndex();
                if (Store.Data.dailyDay != today) { Store.Data.dailyDay = today; Store.Data.dailyBest = folds; }
                else Store.Data.dailyBest = Mathf.Min(Store.Data.dailyBest == 0 ? 999 : Store.Data.dailyBest, folds);
                Store.Data.dailySolved = 1;
                Store.SetDaily(today, folds);
            }
            else if (S.IsMade)
            {
                // A cube the player built keeps its own two records and touches
                // nothing else — no progression, no vault total, no board. Its par
                // is real (the solver proved it) but its AUTHOR is not trustworthy.
                MadeCube m = Store.Made(S.madeKey);
                if (m != null)
                {
                    if (m.best < 0 || folds < m.best) m.best = folds;
                    if (m.tbest < 0 || ms < m.tbest) m.tbest = ms;
                    Store.Save();
                }
            }
            else if (S.IsPractice)
            {
                // nothing. A cube visited by number is a cube seen, not one cleared.
            }
            else
            {
                // Folds and time are kept apart on purpose. They are improved by
                // different play — one by a better route, one by a faster hand — and
                // a single "best run" column would make beating one cost the other.
                if (!Store.TryBest(S.levelNo, out int b) || folds < b) Store.SetBest(S.levelNo, folds);
                if (!Store.TryTimeBest(S.levelNo, out long tb) || ms < tb) Store.SetTimeBest(S.levelNo, ms);
                if (S.levelNo + 1 > Store.Data.reached) { Store.Data.reached = S.levelNo + 1; Store.Save(); }
                LevelSupply.Prebuild(S.levelNo + 1);
            }

            Rig.Kick(11, 0, 1); Rig.Shake(1f); Rig.Punch(0.09f);
            Hud.Flash(Palette.Core, 0.22f);
            Haptics.Buzz(Haptics.Collapse);
            StartCoroutine(WinCard());
        }

        IEnumerator WinCard()
        {
            yield return new WaitForSecondsRealtime(0.88f);
            Screens.ShowWin(S, this);
        }

        public void SetScreenUp(bool up)
        {
            _screenUp = up;
            _input.Enabled = !up;
            Hud.SetVisible(!up);
            if (up) SolveClock.Hold(); else SolveClock.Go();
        }

        public bool ScreenUp => _screenUp;

        // ---- the frame ------------------------------------------------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float ms = dt * 1000f;

            Store.Tick();
            LevelSupply.Tick();

            if (S.lv != null)
            {
                if (!_screenUp)
                {
                    _input.Tick(dt);
                    S.AdvanceWalk(ms);
                    S.AdvanceAnim(ms);
                    S.StepPlateClock(ms, _screenUp);
                }
                S.Remain.Tick(S);
                View.Apply(Rig.cam);
                Hud.Tick(dt, S);
            }

            Rig.Tick(dt, View.cube);

            if (Input.GetKeyDown(KeyCode.Escape) && !_screenUp && S.lv != null) Screens.ShowPause(this);
        }

        void OnApplicationPause(bool paused)
        {
            // A phone call in the middle of a cube is not part of the solve.
            if (paused) { SolveClock.Hold(); Store.Flush(); }
            else if (!_screenUp) SolveClock.Go();
        }

        void OnApplicationQuit() => Store.Flush();
    }
}
