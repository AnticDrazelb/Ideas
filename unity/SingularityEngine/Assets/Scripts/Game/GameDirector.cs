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

        Sky _sky;
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

            // AND THE SKY UNDER EVERYTHING, before the housing goes on. It draws
            // in the Background queue and occludes nothing; building it here is
            // only so the order in this method reads the way the light arrives.
            _sky = Sky.Build(transform);

            // THE HOUSING FIRST, so everything after it is mounted IN something.
            // Order 5, behind the HUD's 10 and the screens' 20, and it is twelve
            // pieces around the edge rather than a plate — see Chassis.
            Chassis.Build();

            Hud = Hud.Build(this);

            // THE PANEL'S OWN ROWS AT 24 AND THE PANE ACROSS THE FRONT AT 25,
            // both in front of the menus as well — see Scanlines and Glass. Built
            // after the HUD only because it is easier to read in the order the
            // light arrives; the sorting order is what actually decides, not
            // where these two lines sit.
            Scanlines.Build();
            Glass.Build();

            // The sound's words go to the readout. Sfx does not know what a Hud
            // is and must not: it raises the caption and something else decides
            // where words go, exactly as Session raises events and this decides
            // what they look like.
            Sfx.Caption = Hud.Caption;

            // ORDER MATTERS: bloom first, so the brightness control raises the
            // finished picture rather than the picture the glow was computed from.
            Glow = Bloom.Attach(Rig.cam);
            Filter = ScreenFilter.Attach(Rig.cam);

            Wire();
            Screens.Build(this);
            Screens.ShowTitle();   // which starts the attract cube

            // and the panel comes up: one band down the glass, once, because a
            // machine that is switched on does something and a picture does not
            Scanlines.WarmUp();
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

        /// <summary>
        /// IF THE SAVE DID NOT LOAD, SAY SO.
        ///
        /// Starting somebody over without a word is the worst thing this code can
        /// do — and it is also what a player will ASSUME happened any time a
        /// vault list looks wrong, so the silence costs trust even on the runs
        /// where nothing went wrong. Store knows which of the three copies it is
        /// running on; this is the one place that asks.
        ///
        /// A toast rather than a modal. There is nothing to decide: the bytes are
        /// already quarantined and there is no undo to offer, so an OK button is
        /// a speed bump in front of a fact. RECOVERED is the good outcome and
        /// reads like one; LOST is the bad one and says the word.
        /// </summary>
        void ReportSave()
        {
            if (Store.LoadedFrom == Store.Origin.Recovered)
                Hud.Toast("SAVE RESTORED FROM BACKUP");
            else if (Store.LoadedFrom == Store.Origin.Lost)
                Hud.Toast("SAVE UNREADABLE · STARTING OVER");
        }

        void Wire()
        {
            ReportSave();
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
                // WHAT RETIRES THE LESSON IS THE PLAYER DOING IT. Not the prompt
                // being shown, not a card being dismissed — see Coach.
                Coach.Learned(Coach.LearnedStep);
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
                // taking one is the lesson; see Coach
                Coach.Learned(Coach.LearnedNode);
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
                Coach.Learned(Coach.LearnedFold);
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
                // AND THE SAME RULE FOR THE THREE THE BOARD DOES TO YOU. The bit
                // says which, so one handler retires all three and nothing here
                // has to know what chapter it is. Plate A is absent on purpose —
                // it has a card of its own, above.
                if (bit == 2) Coach.Learned(Coach.LearnedSubstrate);
                else if (bit == Level.Everted) Coach.Learned(Coach.LearnedEverter);
                else if (bit == Level.Swapped || bit == Level.Swapped2) Coach.Learned(Coach.LearnedTrigger);

                // AN EVERTER IS NOT A PLATE AND MUST NOT FEEL LIKE ONE.
                //
                // A plate changes the BOARD — every trace goes dead, every dead
                // cell lights — and it earns the loudest moment in the game. An
                // everter changes nothing about any cell; it changes which face
                // of the solid you are standing on. So it gets its own weight: a
                // long, symmetrical turn rather than a bang, violet rather than
                // rust, and a stop at the CROSSING rather than at the strike,
                // because the moment the solid is edge-on is the moment the two
                // boards trade places.
                if (bit == Level.Everted)
                {
                    View.Rebuild(true);
                    Rig.Punch(0.055f); Rig.Squash(false, 0.11f);
                    Hud.Flash(Palette.Evert, 0.30f, 0.62f);
                    _sfx.Evert();
                    Scanlines.WarmUp();
                    _fx.PlateFired(S.N, At(cell));
                    _orb.SetMood("wide", 900f);
                    // the hold lands halfway through the turn, where the solid is
                    // edge-on — CubeView.EvertSeconds is the whole flip, so half
                    // of it is the crossing
                    TimeBend.Hitstop(90f);
                    TimeBend.Slowmo(0.42f, CubeView.EvertSeconds * 1000f);
                    View.RestartReveal(0.26f * 26f);
                    Hud.Toast(S.Everted ? "EVERTED — THE FAR SIDE" : "RESTORED — THE NEAR SIDE");
                    return;
                }

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
        /// <summary>
        /// Root two is the worst silhouette a bounded three-quarter pose presents;
        /// the rest is air, so a corner never touches the rule above it or the
        /// primary below. TitleChecks walks the whole cycle against this number.
        /// </summary>
        public const float AttractMargin = 1.53f;

        public void ShowAttract()
        {
            if (View.attract) return;      // already turning; do not reload under it
            StartCoroutine(Attract());
        }

        IEnumerator Attract()
        {
            yield return null;
            // VAULT I ON PURPOSE. The attract loop is the title screen playing
            // itself, so it wants a small, legible, authored cube — not whatever
            // the player has reached, and not one of vault IX's arc cubes, whose
            // whole point is a mechanic nobody has been taught yet.
            int level = Mathf.Clamp(Store.Data.reached, 1, Baked.Levels.Length);
            S.Load(LevelSupply.Get(level), level, LoadKind.Practice);
            SolveClock.Stop();
            View.attract = true;
            View.Whole();
            Rig.Pull = 0f;
            View.Rebuild(true);
            View.SkipWave();
            // INTO THE BAND THE TITLE ACTUALLY LEAVES, AND FILLING IT.
            //
            // This was Fit(N, 2.30) — the play window, with enough margin that a
            // freely tumbling cube never showed its diagonal past the gradient's
            // clear stripe. Two things were wrong with that and both were stale.
            // The window it framed into is the HUD's, whose two bands are
            // different heights and are not drawn on this screen at all, so the
            // subject of the title sat seventy units low. And the margin was
            // paying for a full tumble AND for a paragraph of pitch that has since
            // been deleted from the screen.
            //
            // The band is Layout's now, the pose is bounded, and the margin is the
            // root two a bounded pose actually needs plus air. See CubeView's
            // attract constants and TitleChecks.
            Rig.FitTo(Layout.TitleRect(), S.N, AttractMargin);
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
            int band = Vaults.VaultOf(how == LoadKind.Vault || how == LoadKind.Practice
                                      ? level : Daily.SpecLevel);
            _sfx.Ambience(band);

            // and the board wears the same vault the room is pitched to. The
            // lattice corrodes as the ladder climbs — see Palette — so a cube
            // that borrows vault five's difficulty borrows its metal too, which
            // is the same rule the ambience above is already following.
            View.PushPalette(band);

            S.Load(src, level, how, madeKey);
            View.attract = false;
            // the last cube ended by being thrown across the room; this one is
            // whole, square to the camera, and the camera is back where it lives
            View.Whole();
            Rig.Pull = 0f;
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
                // place rather than being half here and half in whatever reads it
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
                // and the par it was scored against, which the vault grid rates it by
                Store.SetPar(S.levelNo, S.lv.par);
                if (!Store.TryTimeBest(S.levelNo, out long tb) || ms < tb) Store.SetTimeBest(S.levelNo, ms);
                if (S.levelNo + 1 > Store.Data.reached) { Store.Data.reached = S.levelNo + 1; Store.Save(); }
                // and there is no cube after the last one to cut. Asking for it
                // would put the generator to work on a five hundred and first that
                // is not in the ladder and is not supposed to exist.
                if (S.levelNo < Vaults.LastCube) LevelSupply.Prebuild(S.levelNo + 1);
            }

            Rig.Kick(11, 0, 1); Rig.Shake(1f); Rig.Punch(0.09f);
            Hud.Flash(Palette.Core, 0.22f);
            Haptics.Buzz(Haptics.Collapse);
            _fx.Collapse(S.N, AtPlayer());
            // through to the card. The exit is a second and a half now, and a mood
            // that lapses two thirds of the way through it puts the orb back to
            // idle over an empty room.
            _orb.SetMood("win", 1700f);
            // THE ONE PLACE THE CLOCK REALLY BENDS. A long stop, then a third of
            // speed, so the core taking you lands at the pace of something ending
            // rather than something being dismissed.
            //
            // IT HAS TO BE OVER BEFORE THE MACHINE BREAKS. This was 900ms, which
            // ran straight through the exit — and the exit's own geometry is on
            // the unscaled clock while Fx is on the bent one, so the debris would
            // have crawled while the cells it came off flew. 470 puts the clock
            // back at 1.0 a frame or two before the break, which is also the right
            // shape: slow for the collapse, real time for the failure.
            TimeBend.Hitstop(150f);
            TimeBend.Slowmo(0.30f, 470f);

            // THE LAST CUBE DOES NOT END LIKE THE OTHER FOUR HUNDRED AND NINETY
            // NINE. Everything below — the throw, the card, the vault chime — is
            // for a cube you are going to leave and come back from.
            if (Vaults.EndsTheMachine(S.levelNo, S.IsDaily, S.IsMade))
            {
                StartCoroutine(Finale());
                return;
            }

            // THE CARD DOES NOT APPEAR OVER A STILL IMAGE OF A SOLVED PUZZLE. The
            // machine turns to show you it is a solid, and then it is thrown.
            StartCoroutine(WinExit());

            // A PERFECT LINE IS A DIFFERENT EVENT. Clearing at par with the same
            // exit as clearing four over says the game does not care, and the card
            // saying PERFECT COLLAPSE forty frames later is a receipt, not a
            // celebration. It goes arc, which is the one step past gold that
            // anybody reads without being told.
            if (folds <= S.lv.par) StartCoroutine(PerfectLine());

            // TWO HUNDRED MILLISECONDS OF NOTHING, AND THEN ONE TONE. The bed is
            // ducked the instant the core takes you, the room goes quiet under a
            // board that is still whole and about to stop being, and the tone
            // arrives into that gap. It is the only silence in the game — and it
            // is now also the gap the machine breaks into, three hundred
            // milliseconds later, with the tone still ringing under it.
            _sfx.Duck(0.20f);
            StartCoroutine(WinTone());

            bool crossing = !S.IsDaily && !S.IsMade
                            && Vaults.VaultOf(S.levelNo + 1) != Vaults.VaultOf(S.levelNo);
            if (crossing) _sfx.Vault();
            // and the card is the last beat of WinExit, not a timer racing it
        }

        /// <summary>
        /// THE END OF THE MACHINE.
        ///
        /// Five hundred cubes and this is the last one, so it is the only place in
        /// the game that gets to be an ENDING rather than a transition. Everything
        /// the ordinary exit does is wrong here: the throw says "and now the next
        /// one", the card asks a question, and the vault chime congratulates you
        /// on a boundary you are not crossing.
        ///
        /// SO NOTHING IS THROWN. The board is not broken open — it is left exactly
        /// as it was solved, and the picture closes in on the one square you
        /// reached instead. What goes away is everything AROUND it: the readout,
        /// the housing, the glass, the whole instrument the game has been played
        /// through, until what is left on screen is a cell and the dark.
        ///
        /// Then it grows. The singularity has spent five hundred cubes being the
        /// smallest thing on the board and it stops being small — white, because
        /// every other colour in this palette means something and none of them
        /// mean this, and bright enough that the bloom takes it. The shockwave is
        /// the ring the ordinary exits gave up: it was competing with the debris
        /// there and here there is nothing for it to compete with.
        ///
        /// And then black, and three seconds of it, and the title. A game that has
        /// an ending should be allowed to stop for a moment before offering you
        /// anything.
        /// </summary>
        bool _ending;

        /// <summary>
        /// THE DIRECTOR, SEEN THROUGH THE ENDING'S EYES.
        ///
        /// Ending decides what happens when; this decides what each of those
        /// things is made of. Every method is one line of forwarding on purpose —
        /// the moment a decision creeps in here it is a decision the harness
        /// cannot run, which is the whole reason the split exists.
        /// </summary>
        sealed class Stage : IStage
        {
            readonly GameDirector _d;
            readonly Vector3 _at;
            public Stage(GameDirector d, Vector3 at) { _d = d; _at = at; }

            public float Dt => Time.unscaledDeltaTime;
            public float HalfHeight => _d.Rig.cam.orthographicSize;
            public float Aspect => Screen.width / Mathf.Max(1f, (float)Screen.height);

            public void Ui(float a) => Singularity.UI.UiKit.Dim(a);
            public void Board(float a) => _d.View.Dim(a);
            public void Close(float k) { _d.Rig.Close = k; _d.Rig.At = _at; }
            public void Rumble(float amt) => _d.Rig.Rumble(amt);

            public void Blob(float size, float alpha)
                => _d._fx.Blob(_at, size, new Color(1f, 1f, 1f, alpha));

            public void Ring(float r0, float r1, float dur, float width, bool core)
                => _d._fx.Ring2(_at, r0, r1, dur, core ? Palette.Core : Color.white, width, false);

            public void Sparks(float speed, float size)
                => _d._fx.Burst(_at, 60, new Fx.BurstOpts
                {
                    kind = Fx.Kind.Spark, speed = speed, life = 1.4f,
                    size = size, gravity = 0f, col = Color.white
                });

            public void Bang()
            {
                _d.Rig.Kick(22, 0, 1);
                _d.Rig.Shake(1f);
                _d.Rig.Punch(-0.10f);
                Haptics.Buzz(Haptics.Collapse);
                Haptics.Buzz(Haptics.Shatter);
                _d._sfx.Shatter();
                TimeBend.Hitstop(120f);
            }

            public void Chime() => _d._sfx.Vault();
            public void Hush(float seconds) => _d._sfx.Duck(seconds);

            public void Input(bool on) => _d._input.Enabled = on;

            public void Clear()
            {
                _d._fx.Clear();
                _d._orb.Reset();
            }

            // ShowTitle starts the attract cube itself — asking twice is how the
            // second call ends up being the one that matters.
            public void Title() => Screens.ShowTitle();
        }

        /// <summary>
        /// PUMP THE ENDING, AND SAY SO IF IT DIES.
        ///
        /// A coroutine that throws is logged once by Unity and then simply stops,
        /// leaving whatever it had already set exactly where it was — which for
        /// this sequence means a screen that is half faded, or not faded at all,
        /// with no card, no throw and no way to tell that anything went wrong.
        /// Driving the enumerator by hand costs one try block and turns that into
        /// a message that names the ending; the catch also PUTS THE SCREEN BACK,
        /// because a player who hits this should land on the title rather than on
        /// a dead board.
        /// </summary>
        IEnumerator Finale()
        {
            _ending = true;
            IEnumerator seq = Ending.Run(new Stage(this, At(S.lv.goal)));

            for (int guard = 0; guard < Ending.FrameCap; guard++)
            {
                bool more;
                try { more = seq.MoveNext(); }
                catch (System.Exception e)
                {
                    Debug.LogError("the ending stopped: " + e);
                    break;
                }
                if (!more) { _ending = false; yield break; }
                yield return null;
            }

            // it either threw or ran away with itself; either way, do not strand
            // the player on a board they cannot see and cannot leave
            _ending = false;
            View.Dim(1f);
            Rig.Close = 0f;
            Rig.Pull = 0f;
            Singularity.UI.UiKit.Dim(1f);
            _input.Enabled = true;
            Screens.ShowTitle();
        }

        /// <summary>
        /// THE EXIT, IN THREE MOVES.
        ///
        /// It used to be one: the cells shrank to points, running out from the
        /// core. That is a board being switched off, and switching a board off is
        /// what you do to make room for a card — which is exactly how it read.
        ///
        /// LEAN. The solid turns into three quarters and the camera eases back off
        /// it. Nothing breaks yet, and that is the point: an explosion only lands
        /// if the eye has just been shown that the thing is three-dimensional and
        /// made of parts. This is the one moment in the game where the board is
        /// looked AT rather than played.
        ///
        /// BREAK. One frame with everything on it — the kick, the shake, the
        /// counter-punch that snaps the frame outward instead of in, the rumble,
        /// the crack. The hit that starts the throw is a separate event from the
        /// collapse that started the sequence, because they are separate things:
        /// the core took you, and then the machine failed.
        ///
        /// THROW. Every cell out from the core on its own schedule, tumbling, and
        /// the camera holding back while they go. The card arrives into an empty
        /// room afterwards.
        ///
        /// All of it runs on the unscaled clock. The bent clock is still running
        /// underneath from the collapse and this is choreography, not physics — a
        /// sequence that slows down because the slow-motion it fired is still going
        /// is a sequence that fights itself.
        /// </summary>
        IEnumerator WinExit()
        {
            // let the collapse land first — the hitstop is still holding
            yield return new WaitForSecondsRealtime(0.17f);

            for (float t = 0f; t < CubeView.LeanSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / CubeView.LeanSeconds);
                View.winLean = k * k * (3f - 2f * k);
                Rig.Pull = View.winLean * 0.20f;
                yield return null;
            }
            View.winLean = 1f;
            Rig.Pull = 0.20f;

            Rig.Kick(16, 0, 1);
            Rig.Shake(1f);
            // NEGATIVE punch: orthographicSize goes UP, so the frame jumps outward.
            // Every other punch in this game pulls in, because every other event is
            // an impact. This one is the board getting away from you.
            Rig.Punch(-0.055f);
            Rig.Squash(false, 0.10f);
            Haptics.Buzz(Haptics.Shatter);
            Hud.Flash(Palette.RustHi, 0.26f, 0.75f);
            // the whole machine goes, so this is centred on the board and not on
            // the cell the player happened to finish in
            _fx.Shatter(S.N, Vector3.zero);
            _sfx.Shatter();
            // AND THE PICTURE HOLDS FOR EIGHTY MILLISECONDS. Everything is frozen
            // together — the solid still whole, the ring and the grit stopped
            // where they were thrown — and then the whole picture moves at once.
            // Starting the throw inside the freeze would move the cells while the
            // sparks off them stood still, which is the same clock mismatch the
            // shortened slowmo above exists to avoid.
            TimeBend.Hitstop(80f);
            yield return new WaitForSecondsRealtime(0.08f);

            for (float t = 0f; t < CubeView.BurstSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / CubeView.BurstSeconds);
                View.burstAmt = k;
                // out with the debris and staying out, so the card lands on a room
                // that is bigger than the one the puzzle was played in
                Rig.Pull = 0.20f + k * 0.26f;
                yield return null;
            }
            View.burstAmt = 1f;

            // AFTER THE LAST PIECE, NOT OVER IT. The card used to go up on a fixed
            // 0.88s timer, which was measured against an exit that no longer
            // exists; it lives here now so that there is exactly one description of
            // how long the ending takes, and moving any dial above moves the card
            // with it. A beat of empty room first — nothing is asked of the player
            // until the machine has finished leaving.
            yield return new WaitForSecondsRealtime(0.16f);
            Screens.ShowWin(S, this);
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

            // ONE READING OF THE DEVICE PER FRAME, whoever comes to ask. The sky
            // is the only thing that asks today.
            Tilt.Sample(dt);
            _sky.Tick(dt);

            // A ROTATION IS A RE-LAYOUT, NOT A RESIZE. The board is centred in the
            // space the HUD is not using, and that space is a different shape the
            // instant the device turns — so the fit is redone rather than stretched.
            if (Screen.width != _lastW || Screen.height != _lastH)
            {
                _lastW = Screen.width;
                _lastH = Screen.height;
                // the window is cut to the board across and to the whole band
                // down, so it is a different rectangle in a different aspect —
                // the stroke and the four fold marks that hang on it are re-cut
                // before the camera is re-fitted to them
                Hud.Relayout();
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
                Hud.TickCoach(S, Rig.cam, View, dt);
                // AND THE SINGULARITY STOPS BEING A DOT. The finale grows a white
                // giant out of the cell the orb is standing on; leaving the orb
                // drawn puts a black disc in the middle of the star.
                _orb.Tick(ms, !_screenUp && !_ending);
                _orb.EmitTrail();
                _fx.Tick(ms / 1000f);
            }

            // the attract cube spins free behind the title and has no window to
            // stay inside, so it is the one board the camera does not frame
            Rig.Tick(dt, View.cube, !View.attract);
            Filter.Refresh();
            Glow.Refresh();
            // the schematic's halo. The board is wire whenever the material is out
            // of the way — a held MATRIX, or the middle of an eversion — and a
            // wire with no bloom is a hairline. See Bloom.Lift.
            Glow.Lift = View.GlassAmount;

            // THE BACK GESTURE, AND IT REACHES EVERY SCREEN NOW. This was
            // `Escape && !_screenUp && S.lv != null` — so back opened the pause
            // card while playing and did nothing at all on the manual, the vault
            // rack, calibrate, access, the Forge or its editor. See
            // Screens.BackPressed for what each of them does with it, and why the
            // title asks before it takes you at your word.
            //
            // Not during the ending: the machine is coming apart on its own clock
            // and there is nothing to go back to until it has.
            if (Input.GetKeyDown(KeyCode.Escape) && !_ending)
            {
                if (!_screenUp && S.lv != null) Screens.ShowPause(this);
                else if (Screens.BackPressed()) Application.Quit();
            }
        }

        void OnApplicationPause(bool paused)
        {
            // A phone call in the middle of a cube is not part of the solve.
            if (paused) { SolveClock.Hold(); Store.Flush(); }
            else if (!_screenUp) SolveClock.Go();

            // AND THE SKY FORGETS HOW THE PHONE WAS BEING HELD. Whatever happened
            // while this game was not on screen, it was very likely a pocket —
            // and coming back to a sky jammed against one corner, with no travel
            // in that direction, is worse than coming back to it centred.
            if (!paused) Tilt.Recentred();
        }

        void OnApplicationQuit() => Store.Flush();
    }
}
