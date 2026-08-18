using UnityEngine;
using UnityEngine.UI;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// FIRST CONTACT — the two verbs, taught on the cube that needs them.
    ///
    /// THE HOLE THIS FILLS. The shipped web build showed the manual once, on the
    /// first press of PLAY: `if(!store.taught){ store.taught = 1; show('scManual'); }`.
    /// The port dropped it and kept the field — `taught` is declared in SaveData
    /// and read by nothing — so a player opening this game for the first time was
    /// handed a black square, four dim arrows and no sentence in the entire
    /// product telling them that a swipe folds the world. Every OTHER mechanic in
    /// here is taught: the plate has a card, the matrix has its one nudge, the
    /// four eversion cubes teach eversion by being unsolvable without it. The two
    /// things the game is MADE of were the only two nobody taught.
    ///
    /// AND IT IS NOT THE MANUAL. Restoring the web build's answer would put a
    /// wall of type between a player and the thing they opened; nobody reads a
    /// manual in a puzzle game, which is what the manual's own header in Screens
    /// says. The doctrine this project already follows is written on the plate
    /// card in GameDirector: *teach it when it shows up, once, on the cube that
    /// has one, with the thing itself waiting behind the card.* This is that
    /// doctrine applied to the verbs.
    ///
    /// THE ORDER IS MEASURED, NOT ASSUMED. The obvious script opens with SWIPE TO
    /// FOLD, because folding is the idea. It would be wrong. Solve the authored
    /// cubes and read the first act of the optimal line:
    ///
    ///     cube 1  FOOTING     par 1   step-up   step-up  step-up  FOLD-left
    ///     cube 2  THE TURN    par 1   step-down FOLD-up  step-down
    ///     cube 3  BURIED      par 2   step-left step-up  FOLD-left ...
    ///     cube 4  TWO FACES   par 2   step-left step-down FOLD-right ...
    ///
    /// On all four the first thing a player has to do is WALK. A coach that opens
    /// by teaching the fold is teaching the second verb first and demonstrating a
    /// move the cube does not want yet. So the walk is taught at the start, and
    /// the fold is taught at the moment the fold becomes the answer — which is a
    /// question the game can already ask itself, because the solver is right
    /// there. <see cref="Session.Advice"/> is the hint without the charge.
    ///
    /// WHAT RETIRES IT IS EVIDENCE, NOT EXPOSURE. The bits are written when the
    /// player DOES the verb, not when the prompt is shown — so a player who folds
    /// before being asked has already proved they can, and is never asked; and one
    /// who backs out of the game mid-lesson gets the lesson again rather than
    /// having spent it on a screen they left. A save that has been past cube one
    /// is taught by definition, which is the migration for everybody who was
    /// playing before this existed.
    ///
    /// IT NEVER TAKES THE INPUT. Every graphic here is raycastTarget = false and
    /// there is no dismiss button, because there is nothing to dismiss: the way
    /// out of the lesson is to do the thing, and a player who already knows the
    /// game does it inside a second and never reads a word.
    /// </summary>
    public class Coach
    {
        /// <summary>What the coach has proof the player can do. Bits of SaveData.taught.</summary>
        public const int LearnedStep = 1;
        public const int LearnedFold = 2;
        public const int LearnedAll = LearnedStep | LearnedFold;

        /// <summary>
        /// The last cube the coach will speak on. Vault I is authored and cube two
        /// is the second of the two that "show you a way out you cannot walk to";
        /// past that the game is teaching itself and a voice over the top of it is
        /// noise. A player who has reached cube three without folding does not
        /// exist — the first two cannot be finished any other way.
        /// </summary>
        public const int LastCube = 2;

        public enum Lesson { None, Step, Fold }

        /// <summary>
        /// THE WHOLE DECISION, WITH NOTHING IN IT THAT NEEDS A SCENE — so the
        /// harness can play the state machine out against a real Session without
        /// a canvas, a camera or a frame. See LayoutChecks' neighbour, CoachChecks.
        /// </summary>
        public static Lesson Next(int taught, LoadKind kind, int levelNo, bool won, bool adviceIsFold)
        {
            // THE LADDER ONLY. A daily is somebody's second week, a made cube is
            // the Forge's output and practice is a cube visited by number; none of
            // the three is anybody's first minute, and the coach speaking there
            // would be the game explaining a swipe to somebody who typed a level
            // code in.
            if (kind != LoadKind.Vault) return Lesson.None;
            if (won || levelNo > LastCube || (taught & LearnedAll) == LearnedAll) return Lesson.None;
            if ((taught & LearnedStep) == 0) return Lesson.Step;
            if ((taught & LearnedFold) == 0 && adviceIsFold) return Lesson.Fold;
            return Lesson.None;
        }

        /// <summary>
        /// A SAVE THAT HAS BEEN PAST THE FIRST CUBE IS TAUGHT.
        ///
        /// Everyone playing before this file existed has a save with no bits set
        /// in it, and handing them a tutorial on their four-hundredth cube is a
        /// worse bug than the one this fixes. `reached` is the honest witness: it
        /// only ever moves when a cube is cleared.
        /// </summary>
        public static int Migrate(int taught, int reached)
            => reached > 1 ? LearnedAll : taught;

        // ---- the marks --------------------------------------------------------

        readonly RectTransform _root;
        Image _ring, _arrow;
        Text _line;

        Lesson _showing = Lesson.None;
        float _t;

        /// <summary>Where the ring goes: the far end of the walk, in board squares.</summary>
        int _cellU = -1, _cellV = -1;

        /// <summary>Which way the coached fold goes, in InputRouter's turn indices.</summary>
        int _foldDir = -1;

        /// <summary>Restated only when the state changes, because Advice runs a solver.</summary>
        long _stamp = long.MinValue;

        public Coach(RectTransform aperture)
        {
            _root = UiKit.Rect(aperture, "coach", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _root.gameObject.SetActive(false);

            // THE RING IS THE ONE THE PLAYER ALREADY OWNS. It is the mark drawn
            // around the singularity itself in the manual's TAP TO MOVE diagram,
            // so the coach is pointing with the game's own vocabulary rather than
            // introducing a finger, a hand or a dotted line that appears nowhere
            // else in the product.
            _ring = UiKit.Icon(_root, "ring", Palette.Arc, 64f, new Vector2(0.5f, 0.5f), Vector2.zero);
            _arrow = UiKit.Icon(_root, "arrow", Palette.RustHi, 44f, new Vector2(0.5f, 0.5f), Vector2.zero);

            // ONE LINE, UNDER THE WINDOW. Not a card, not a panel and not over the
            // board: the thing being talked about is the board, and a coach that
            // covers its own subject is a worse teacher than no coach. It sits in
            // the gap the square window opened up between the aperture and the
            // bottom band — see Layout.
            _line = UiKit.Label(_root, "line", "", 22, Palette.Ink, TextAnchor.UpperCenter,
                                new Vector2(0, 0), new Vector2(1, 0),
                                new Vector2(0, -64), new Vector2(0, -26));
        }

        /// <summary>
        /// The far end of the walk, and it must be PLAIN TRACE.
        ///
        /// One tap walks the singularity to any trace connected to the one under
        /// it, so the honest demonstration of the verb is its full reach rather
        /// than the next square along — the lesson is "you go where you point",
        /// not "you shuffle". But stepping onto a glyph FIRES it: a plate turns
        /// the whole board inside out for five seconds, an everter turns the solid
        /// over. Teaching the walk by detonating the biggest event in the game is
        /// not teaching the walk, so the ring only ever lands on ordinary trace,
        /// and the same rule keeps it off a node and a lock — both of which are
        /// state, and neither of which is what the sentence under it says.
        /// </summary>
        public static bool FarEnd(Session s, out int bu, out int bv)
        {
            bu = bv = -1;
            if (s?.lv == null || s.surf == null || s.reach == null) return false;

            int n = s.N, best = -1;
            Int3 here = Projection.ViewOf(n, s.M, s.pos);

            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                {
                    int k = u * n + v;
                    if (!s.reach[k]) continue;
                    if (u == here.x && v == here.y) continue;
                    Surf c = s.surf[k];
                    if (!c.has || Level.IsGlyph(c.t)) continue;
                    if (s.lv.KeyIndexAt(c.w) >= 0 || s.lv.DoorIndexAt(c.w) >= 0) continue;

                    int d = s.reachDist[k];
                    if (d <= best) continue;          // ties keep the first, so it is stable
                    best = d; bu = u; bv = v;
                }

            return best > 0;
        }

        /// <summary>
        /// Cheap enough to ask every time the board settles, and never asked more
        /// often than that. A five-cube with a par of one is a search of a few
        /// hundred states; the budget is small for the same reason.
        /// </summary>
        static bool AdviceIsFold(Session s, out int dir)
        {
            dir = -1;
            if (!s.Advice(out Act a, 8)) return false;
            if (!a.isTurn) return false;
            dir = a.dir;
            return true;
        }

        /// <summary>
        /// Pumped from the HUD, which is where the readout's own clock already is.
        /// The state is only recomputed when the board has actually changed —
        /// position, orientation, keys, doors and world — because the recompute
        /// runs a solver and a frame is not a state change.
        /// </summary>
        public void Tick(Session s, Camera cam, CubeView view, float dt)
        {
            if (s?.lv == null) { Hide(); return; }

            long stamp = Stamp(s);
            if (stamp != _stamp)
            {
                _stamp = stamp;
                Restate(s);
            }

            if (_showing == Lesson.None) return;

            _t += dt;
            Paint(s, cam, view);
        }

        static long Stamp(Session s)
            => ((long)Turns.OriIndex(s.M) << 40) ^ ((long)s.pos.x << 34) ^ ((long)s.pos.y << 28)
             ^ ((long)s.pos.z << 22) ^ ((long)s.kmask << 12) ^ ((long)s.doors << 4) ^ s.world
             ^ ((long)s.levelNo << 46) ^ (s.won ? 1L << 60 : 0L);

        void Restate(Session s)
        {
            int taught = Store.Data.taught;

            // The fold's direction is the only thing here that costs a search, so
            // it is asked for ONLY when there is a fold lesson left to give.
            int dir = -1;
            bool fold = (taught & LearnedFold) == 0
                     && (taught & LearnedStep) != 0
                     && s.kind == LoadKind.Vault && s.levelNo <= LastCube && !s.won
                     && AdviceIsFold(s, out dir);

            Lesson want = Next(taught, s.kind, s.levelNo, s.won, fold);

            if (want == Lesson.Step && !FarEnd(s, out _cellU, out _cellV)) want = Lesson.None;
            _foldDir = dir;

            if (want != _showing)
            {
                _showing = want;
                _t = 0f;
                _root.gameObject.SetActive(want != Lesson.None);
                _line.text = want == Lesson.Step ? "TAP TO MOVE"
                           : want == Lesson.Fold ? "SWIPE TO FOLD" : "";
            }

            _ring.gameObject.SetActive(_showing == Lesson.Step);
            _arrow.gameObject.SetActive(_showing == Lesson.Fold);
        }

        void Hide()
        {
            if (_showing == Lesson.None) return;
            _showing = Lesson.None;
            _stamp = long.MinValue;
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// THE PULSE HAS A FLOOR, AND THE FLOOR IS THE ACCESSIBILITY ANSWER.
        ///
        /// Motion has an OFF in this game and it means off — camera shake, the
        /// bent clock, all of it. A prompt is not decoration though: it is the
        /// only thing on screen saying what to do, so it may not switch off with
        /// the sparks. It stops TRAVELLING instead. At MotionAmount zero the
        /// arrow sits still at the edge it is pointing to and the ring holds its
        /// size, and both are still perfectly legible; at full they move.
        /// </summary>
        void Paint(Session s, Camera cam, CubeView view)
        {
            float m = Access.MotionAmount;
            float beat = Mathf.Repeat(_t, 1.4f) / 1.4f;

            if (_showing == Lesson.Step)
            {
                if (cam == null || view == null || view.cube == null) return;
                if (_cellU < 0 || s.surf == null) return;
                Surf c = s.surf[_cellU * s.N + _cellV];
                if (!c.has) return;

                Vector3 world = view.cube.TransformPoint(CubeMesh.CellToObject(s.N, c.w));
                _ring.rectTransform.position = cam.WorldToScreenPoint(world);

                // a ring closing on the square, then starting again — the shape a
                // tap makes, drawn at the place a tap is wanted
                float g = 1f + (1f - beat) * 0.55f * m;
                _ring.rectTransform.sizeDelta = new Vector2(64f * g, 64f * g);
                _ring.color = new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b,
                                        0.35f + 0.55f * Mathf.Sin(beat * Mathf.PI));
                return;
            }

            if (_showing == Lesson.Fold && _foldDir >= 0)
            {
                // the swipe, drawn as the arrow the four fold marks already use,
                // travelling the way the finger goes
                Vector2 way = FoldWay[_foldDir];
                float reach = Mathf.Min(_root.rect.width, _root.rect.height) * 0.30f;
                float along = (-0.5f + beat * m) * 2f * reach * (m > 0.001f ? 1f : 0f);

                _arrow.rectTransform.anchoredPosition = way * (along + reach * 0.55f);
                _arrow.rectTransform.localEulerAngles = new Vector3(0, 0, FoldSpin[_foldDir]);
                _arrow.color = new Color(Palette.RustHi.r, Palette.RustHi.g, Palette.RustHi.b,
                                         0.30f + 0.60f * Mathf.Sin(beat * Mathf.PI));
            }
        }

        /// <summary>InputRouter's turn indices: 0 left, 1 right, 2 up, 3 down.</summary>
        static readonly Vector2[] FoldWay =
            { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, -1) };

        /// <summary>The arrow glyph is drawn pointing up; the rest are that one turned.</summary>
        static readonly float[] FoldSpin = { 90f, -90f, 0f, 180f };

        // ---- what the player proved -------------------------------------------

        /// <summary>
        /// Called from the events the player's own hands raise. The write is
        /// debounced by Store, so four of these in a second cost one save.
        /// </summary>
        public static void Learned(int bit)
        {
            if ((Store.Data.taught & bit) == bit) return;
            Store.Data.taught |= bit;
            Store.Save();
        }
    }
}
