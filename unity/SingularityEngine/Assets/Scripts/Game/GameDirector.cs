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
        public ScreenFilter Filter { get; private set; }
        public Bloom Glow { get; private set; }

        InputRouter _input;
        Sfx _sfx;
        Fx _fx;
        PlayerOrb _orb;
        bool _screenUp = true;      // a menu is over the board
        int _lastW, _lastH;

        void Awake()
        {
            I = this;
            Store.Load();

            S = new Session();
            Rig = gameObject.AddComponent<CameraRig>();
            Rig.Init();

            View = gameObject.AddComponent<CubeView>();
            View.Init(S);

            _sfx = Sfx.Build(transform);
            _fx = Fx.Build(transform);
            _orb = PlayerOrb.Build(transform, S, View, Rig.cam, _fx);
            _input = new InputRouter(S, View, Rig.cam) { Sfx = _sfx };

            Hud = Hud.Build(this);

            // ORDER MATTERS: bloom first, so the brightness control raises the
            // finished picture rather than the picture the glow was computed from.
            Glow = Bloom.Attach(Rig.cam);
            Filter = ScreenFilter.Attach(Rig.cam);

            Wire();
            Screens.Build(this);
            Screens.ShowTitle();   // which starts the attract cube
        }

        /// <summary>
        /// Where a cell is, for the debris. The effects live in a screen-aligned
        /// plane and do not rotate with the cube — they belong to the event, not to
        /// the geometry — so only the projected position matters.
        /// </summary>
        Vector3 At(Int3 cell)
        {
            Int3 v = Projection.ViewOf(S.N, S.M, cell);
            float c = (S.N - 1) * 0.5f;
            return new Vector3(v.x - c, v.y - c, 0f);
        }

        Vector3 AtPlayer() => At(S.pos);

        void Wire()
        {
            S.On.Toast = msg => Hud.Toast(msg);

            S.On.Settled = () =>
            {
                View.Rebuild();
                View.UpdateReach();
                Rig.Fit(S.N);
                _fx.SetBoard(S.N);
                Hud.Refresh(S);
            };

            S.On.Stepped = (cell, surf) =>
            {
                Rig.Shake(0.045f);
                _sfx.Step();
                _fx.Footfall(At(cell), Palette.Tile(surf.t, surf.d, S.N));
            };

            S.On.Landed = cell =>
            {
                Rig.Kick(3, 0, 1); Rig.Shake(0.10f);
                _sfx.Land();
                _fx.Land(At(cell), Palette.GridHi);
                _orb.Hop();
                _orb.SetMood("land", 260f);
            };

            S.On.DoorOpened = (cell, i) =>
            {
                Rig.Kick(5, 0, 1); Rig.Shake(0.34f); Rig.Punch(0.030f);
                Hud.Flash(Palette.Node, 0.30f);
                _sfx.Lock();
                _fx.LockOpen(At(cell));
                _orb.SetMood("happy", 800f);
                TimeBend.Hitstop(46f);
            };

            S.On.KeyTaken = (cell, i) =>
            {
                Rig.Shake(0.20f); Rig.Punch(0.022f);
                Hud.Flash(Palette.Node, 0.26f);
                _sfx.Node();
                _fx.NodeTaken(At(cell));
                _orb.SetMood("happy", 900f);
                TimeBend.Hitstop(30f);
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
                _sfx.Fold();
                _fx.FoldLanded(S.N, AtPlayer());
                TimeBend.Hitstop(52f);
                View.Rebuild();
                View.RestartReveal();
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
                _sfx.Deny();
                _fx.FoldRefused(S.N, t);
                _orb.SetMood("wide", 620f);
                // a refusal gets a LONGER stop than a landing and none of the
                // reward: the cube hit a wall, and a wall is the one thing in this
                // game that should take a frame away from you
                TimeBend.Hitstop(78f);
            };

            S.On.Denied = (u, v, col) =>
            {
                Hud.Vignette(0.55f); Rig.Shake(0.10f); _sfx.Deny();
                float c = (S.N - 1) * 0.5f;
                _fx.Deny(new Vector3(u - c, v - c, 0f), col);
            };

            S.On.PlateFired = (cell, bit) =>
            {
                // THE BIGGEST EVENT IN THE GAME, and the only one that changes the
                // board rather than the view of it.
                View.Rebuild(true);
                Rig.Kick(13, 0, 1);
                Rig.Shake(0.95f); Rig.Punch(0.075f); Rig.Squash(true, 0.09f);
                Hud.Flash(Palette.Rust, 0.24f);
                _sfx.Plate(bit);
                _fx.PlateFired(S.N, At(cell));
                _orb.SetMood("wide", 700f);
                // the world turning inside out is worth a held breath: a long stop,
                // then a third of speed easing back over most of a second
                TimeBend.Hitstop(120f);
                TimeBend.Slowmo(0.34f, 620f);
                // the reachable sweep follows the front out from the plate rather
                // than racing it, so the two read as one event
                View.RestartReveal(0.26f * 26f);
                Hud.Toast(S.world != 0 ? "INVERT — FIVE SECONDS" : "RESTORED");
            };

            S.On.Reverted = () => { View.Rebuild(true); Rig.Shake(0.42f); Hud.Flash(Palette.Rust, 0.18f); _sfx.Plate(0); };
            S.On.ThrownToPlate = cell =>
            {
                Rig.Shake(0.42f); Rig.Kick(9, 0, 1); _sfx.Deny();
                _fx.Deny(At(cell), Palette.Fault);
                _orb.Hop();
                _orb.SetMood("wide", 620f);
            };
            S.On.StuckChanged = () => { Hud.Vignette(1f); _sfx.Stuck(); _orb.SetMood("wide", 1400f); };

            // one tick a second over the last three, so the plate clock can be
            // HEARD while the eyes are on the board — where they have to be, and
            // where the number is not
            S.On.PlateTick = seconds => _sfx.Tick(seconds);
            S.On.Won = OnWin;
        }

        /// <summary>
        /// Put a cube behind the title screen. It records nothing, unlocks nothing
        /// and posts nothing — the clock is stopped the instant it is loaded — and
        /// it is deliberately the cube the player is up to, so the front of the
        /// game is about where they are rather than about where the game starts.
        /// </summary>
        /// <summary>
        /// THE FRONT OF THE GAME IS NEVER A STILL PICTURE.
        ///
        /// This used to run once, at boot, and Play() turned it off — so the first
        /// visit to the menu had a turning cube and every visit after it had the
        /// dead board of whatever was last played. Showing the title is what asks
        /// for it now, so there is no way to arrive at that screen without it.
        /// </summary>
        public void ShowAttract()
        {
            if (View.attract) return;      // already turning; do not reload under it
            StartCoroutine(Attract());
        }

        IEnumerator Attract()
        {
            yield return null;
            int level = Mathf.Clamp(Store.Data.reached, 1, Baked.Levels.Length);
            S.Load(LevelSupply.Get(level), level, LoadKind.Practice);
            SolveClock.Stop();
            View.attract = true;
            View.Rebuild(true);
            View.SkipWave();
            // A LOT MORE MARGIN THAN A PLAYED BOARD GETS. The attract cube is
            // scenery on a screen whose job is words: at play margin it filled the
            // frame and the pitch was printed over the top of it. Sized to sit in
            // the band the title's gradient deliberately leaves clear.
            Rig.Fit(S.N, 2.30f);
            _fx.SetBoard(S.N);
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

            // The node chime climbs a stack of fifths across a cube, and the bed is
            // pitched off the vault — so both are reset here, where a cube begins.
            // The daily and a forged cube borrow vault V's difficulty, so they
            // borrow its room as well.
            _sfx.ResetNodes();
            _sfx.Ambience(Vaults.VaultOf(how == LoadKind.Vault || how == LoadKind.Practice
                                         ? level : Daily.SpecLevel));

            S.Load(src, level, how, madeKey);
            View.attract = false;
            View.Rebuild(true);
            // the board arrives one cell at a time, outward from where the player
            // is about to be standing
            View.StartWave(S.lv.start, true);
            View.RestartReveal(0.12f * 26f);
            _fx.SetBoard(S.N);
            // a new cube starts on a clean stage: debris, rings, a half-decayed
            // shake and a bent clock all belong to the cube that is over
            _fx.Clear();
            _orb.Reset();
            TimeBend.Reset();
            Rig.Fit(S.N);
            Hud.Refresh(S);
            SetScreenUp(false);

            // cut the next one while this one is played
            if (how == LoadKind.Vault) LevelSupply.Prebuild(level + 1);

            // TEACH IT WHEN IT SHOWS UP, ONCE. Not in the manual four screens
            // earlier, not as a toast that has gone before it is read — on the cube
            // that has one, with the thing itself waiting behind the card.
            if (Store.Data.sawPlate == 0 && new string(S.lv.vox).IndexOfAny(new[] { 'A', 'B' }) >= 0)
            {
                Store.Data.sawPlate = 1;
                Store.Save();
                yield return new WaitForSecondsRealtime(0.26f);
                if (!S.won) Screens.ShowPlateTeach();
                yield break;
            }

            // THE ONE NUDGE. A gesture nobody can see is a gesture nobody has, and
            // the matrix is the answer to the exact frustration that starts around
            // here — so it is offered once, on the third cube, and never again.
            if (Store.Data.sawPeek == 0 && Store.Data.peekHinted == 0 && S.levelNo >= 3)
            {
                Store.Data.peekHinted = 1;
                Store.Save();
                yield return new WaitForSecondsRealtime(1.1f);
                if (!S.won && S.walking == null && S.anim == null) Hud.Toast("HOLD ANYWHERE FOR MATRIX");
            }
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
                // The daily keeps its own score and never touches the progression:
                // it is not the forty-eighth vault, it just borrows its difficulty.
                int today = Daily.DayIndex();
                if (Store.Data.dailyDay != today) { Store.Data.dailyDay = today; Store.Data.dailyBest = folds; }
                else Store.Data.dailyBest = Mathf.Min(Store.Data.dailyBest == 0 ? 999 : Store.Data.dailyBest, folds);
                Store.Data.dailySolved = 1;

                // and everything about windows, streaks and pruning lives in one
                // place rather than being half here and half in the boards screen
                DailyBoards.Record(folds);
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
            _fx.Collapse(S.N, AtPlayer());
            _orb.SetMood("win", 1200f);
            // THE ONE PLACE THE CLOCK REALLY BENDS. A long stop, then a third of
            // speed for most of a second, so the board comes apart at the pace of
            // something ending rather than something being dismissed.
            TimeBend.Hitstop(150f);
            TimeBend.Slowmo(0.30f, 900f);

            // THE CARD DOES NOT APPEAR OVER A STILL IMAGE OF A SOLVED PUZZLE. The
            // board comes apart outward from the core first, and the card arrives
            // after the puzzle has left.
            StartCoroutine(ExitWave());

            // A PERFECT LINE IS A DIFFERENT EVENT. Clearing at par with the same
            // exit as clearing four over says the game does not care, and the card
            // saying PERFECT COLLAPSE forty frames later is a receipt, not a
            // celebration. It goes arc, which is the one step past gold that
            // anybody reads without being told.
            if (folds <= S.lv.par) StartCoroutine(PerfectLine());

            // TWO HUNDRED MILLISECONDS OF NOTHING, AND THEN ONE TONE. The bed is
            // ducked the instant the core takes you, the room goes quiet under a
            // board that is visibly coming apart, and the tone arrives into that
            // gap. It is the only silence in the game.
            _sfx.Duck(0.20f);
            StartCoroutine(WinTone());

            bool crossing = !S.IsDaily && !S.IsMade
                            && Vaults.VaultOf(S.levelNo + 1) != Vaults.VaultOf(S.levelNo);
            if (crossing) _sfx.Vault();
            StartCoroutine(WinCard());
        }

        IEnumerator ExitWave()
        {
            yield return new WaitForSecondsRealtime(0.19f);
            View.StartWave(S.lv.goal, false);
        }

        IEnumerator PerfectLine()
        {
            yield return new WaitForSecondsRealtime(0.21f);
            _fx.PerfectLine(S.N, AtPlayer());
            Hud.Flash(Palette.Arc, 0.30f, 0.9f);
            Rig.Shake(0.7f); Rig.Punch(0.05f);
            _sfx.Vault();
        }

        IEnumerator WinTone()
        {
            yield return new WaitForSecondsRealtime(0.20f);
            _sfx.Win();
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

            // Everything that MOVES runs on the bent clock; everything that
            // MEASURES runs on the real one. The solve clock is monotonic and
            // untouched by any of this, so a player cannot buy thinking time by
            // triggering impacts.
            float ms = TimeBend.Step(dt);

            Store.Tick();
            LevelSupply.Tick();

            // A ROTATION IS A RE-LAYOUT, NOT A RESIZE. The board is centred in the
            // space the HUD is not using, and that space is a different shape the
            // instant the device turns — so the fit is redone rather than stretched.
            if (Screen.width != _lastW || Screen.height != _lastH)
            {
                _lastW = Screen.width;
                _lastH = Screen.height;
                if (S.lv != null) { Rig.Fit(S.N); _fx.SetBoard(S.N); }
            }

            // measured, not guessed: see PerfWatch
            PerfWatch.Tick(dt);

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
                Hud.TickDepth(S, Rig.cam, View);
                _orb.Tick(ms, !_screenUp);
                _orb.EmitTrail();
                _fx.Tick(ms / 1000f);
            }

            Rig.Tick(dt, View.cube);
            Filter.Refresh();
            Glow.Refresh();

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
