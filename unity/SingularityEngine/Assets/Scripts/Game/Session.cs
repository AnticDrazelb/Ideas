using System;
using System.Collections.Generic;
using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    public enum LoadKind { Vault, Daily, Made, Practice }

    /// <summary>Things the session did, for the presentation layer to react to. The rules never draw.</summary>
    public class SessionEvents
    {
        public Action<string> Toast;
        public Action<Int3, Surf> Stepped;
        public Action<Int3> Landed;          // the last step of a walk, which lands rather than steps
        public Action<Int3, int> DoorOpened;
        public Action<Int3, int> KeyTaken;
        public Action<Int3, int> PlateFired;      // cell, bit
        public Action Reverted;
        public Action<Int3> ThrownToPlate;
        public Action<int> FoldLanded;            // turn index
        public Action<int> FoldRefused;           // turn index
        public Action<int, int, Color> Denied;    // u, v, colour
        public Action Won;
        public Action StuckChanged;
        public Action Settled;
        public Action<int> PlateTick;             // seconds remaining, 3..1
    }

    /// <summary>
    /// The cube in play: where you stand, how the engine is folded, what you
    /// carry, and which world the plates have put you in.
    ///
    /// Everything here is rules. It never touches a mesh, a material or an
    /// AudioSource — it raises an event and lets the presentation layer decide
    /// what that looks like. That split is what lets the whole of this class be
    /// exercised by an edit-mode test with no scene loaded.
    /// </summary>
    public class Session
    {
        public const int PlateMs = 5000;
        public const int HintsPerCube = 3;

        public readonly SessionEvents On = new SessionEvents();

        // ---- state ----------------------------------------------------------
        public Level lv;
        public int N;
        public Ori M = Ori.Id;
        public Int3 pos;
        public int kmask, doors, turns, world;

        /// <summary>
        /// WHICH WAY THE ENGINE IS BEING LOOKED THROUGH.
        ///
        /// Bits 1 and 2 of <see cref="world"/> are the plates, and they change
        /// what a cell IS. Bit 4 changes nothing about any cell — it flips the
        /// depth test, so every column shows its FAR side. One accessor rather
        /// than the mask spelled out at each site, because the three places that
        /// project are the three places that must never disagree about it.
        /// </summary>
        public bool Everted => (world & Level.Everted) != 0;
        public Surf[] surf;
        public bool[] reach;
        public byte[] reachDist;

        public int levelNo = 1;
        public LoadKind kind = LoadKind.Vault;
        public string madeKey;
        public bool won, stuck;
        public int hintsLeft = HintsPerCube;
        public float plateT;

        public bool IsDaily => kind == LoadKind.Daily;
        public bool IsMade => kind == LoadKind.Made;
        public bool IsPractice => kind == LoadKind.Practice;

        // ---- motion ---------------------------------------------------------
        public class Walk { public List<int> queue; public Int3 from; public float t; }
        public class Anim
        {
            public bool axisX;
            public float ang, from, to, t, dur;
            public int turn;      // -1 for a spring-back or a refusal
            public bool seat;     // the detent curve
            public bool hard;     // the refusal curve
        }
        public class Drag { public bool hasAxis; public bool axisX; public float ang; }

        public Walk walking;
        public Anim anim;
        public Drag drag;

        readonly List<Snapshot> undoStack = new List<Snapshot>();
        public int UndoDepth => undoStack.Count;

        struct Snapshot
        {
            public Int3 pos; public int ori, kmask, doors, turns, world;
        }

        // ---- loading --------------------------------------------------------

        public void Load(Level source, int number, LoadKind how, string madeKeyIn = null)
        {
            kind = how;
            madeKey = madeKeyIn;
            levelNo = (how == LoadKind.Daily || how == LoadKind.Made) ? Daily.SpecLevel : Mathf.Max(1, number);

            lv = source.Clone();
            lv.name = source.name;
            lv.par = source.par;
            N = lv.n;

            M = Ori.Id;
            pos = lv.start;
            kmask = 0; doors = 0; turns = 0; world = 0;
            lv.ClearEff();
            plateT = 0;

            int k0 = lv.KeyIndexAt(pos);
            if (k0 >= 0) kmask |= 1 << k0;

            undoStack.Clear();
            walking = null; anim = null; drag = null;
            won = false; stuck = false;
            hintsLeft = HintsPerCube;
            Remain.Reset();

            Settle();

            // The clock starts with the cube — including the intro wave, which
            // every player and every device pays identically.
            SolveClock.Reset();

            if (how == LoadKind.Vault && levelNo > Store.Data.reached)
            {
                Store.Data.reached = levelNo;
                Store.Save();
            }
        }

        // ---- projection bookkeeping ----------------------------------------

        /// <summary>Recompute everything the projection decides, after any settled change.</summary>
        public void Settle()
        {
            surf = Projection.Project(N, lv.Eff(world), M, Everted);
            ComputeReach();
            On.Settled?.Invoke();
        }

        public void ComputeReach()
        {
            reach = new bool[N * N];
            reachDist = new byte[N * N];
            Int3 v = Projection.ViewOf(N, M, pos);
            var q = new List<int> { v.x * N + v.y };
            reach[q[0]] = true;
            for (int i = 0; i < q.Count; i++)
            {
                int u = q[i] / N, w = q[i] % N;
                for (int t = 0; t < 4; t++)
                {
                    int u2 = u + Turns.All[t].dx, w2 = w + Turns.All[t].dy;
                    if (u2 < 0 || w2 < 0 || u2 >= N || w2 >= N) continue;
                    int k = u2 * N + w2;
                    if (reach[k]) continue;
                    if (!Projection.Walkable(lv, surf, u2, w2, doors)) continue;
                    reach[k] = true;
                    reachDist[k] = (byte)Mathf.Min(255, reachDist[q[i]] + 1);
                    q.Add(k);
                }
            }

            // ONE CHOKE POINT. Everything that can change what you may reach — a
            // landed fold, a step, a plate firing, an undo, a cube loading — comes
            // through here, so the live par is asked for here and nowhere else.
            Remain.Schedule(this);
        }

        public int KeysHeld() => Popcount(kmask) - Popcount(doors);
        static int Popcount(int x) { int c = 0; while (x != 0) { x &= x - 1; c++; } return c; }

        // ---- undo -----------------------------------------------------------

        void PushUndo()
        {
            undoStack.Add(new Snapshot { pos = pos, ori = Turns.OriIndex(M), kmask = kmask, doors = doors, turns = turns, world = world });
            if (undoStack.Count > 200) undoStack.RemoveAt(0);
        }

        public bool Undo()
        {
            if (undoStack.Count == 0 || won) return false;
            Snapshot s = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            pos = s.pos; M = Turns.Oris[s.ori];
            kmask = s.kmask; doors = s.doors; turns = s.turns; world = s.world;

            // Undoing INTO a flipped world hands back a full five seconds rather
            // than whatever was left when the snapshot was taken. Storing the
            // remainder would be the more literal restore and the worse one: it
            // would drop the player back in with a fifth of a second on the clock,
            // which is not a position anybody can play out of, and undo would be a
            // button that re-served the same death. This is a retry, so it is a
            // whole run at it.
            plateT = (world & ~Level.Everted) != 0 ? PlateMs : 0;

            walking = null; anim = null; drag = null;
            won = false; stuck = false;
            Settle();
            return true;
        }

        // ---- pathing --------------------------------------------------------

        /// <summary>
        /// Pathing treats a shut lock as a wall, EXCEPT as the final square: you
        /// open a lock by walking into it deliberately, one at a time, never by
        /// having a route swallow two of them and your last node on the way past.
        /// </summary>
        public List<int> PathTo(int tu, int tv)
        {
            Int3 start = Projection.ViewOf(N, M, pos);
            var from = new int[N * N];
            for (int i = 0; i < from.Length; i++) from[i] = -1;
            var seen = new bool[N * N];
            var q = new List<int> { start.x * N + start.y };
            seen[q[0]] = true;

            for (int i = 0; i < q.Count; i++)
            {
                int u = q[i] / N, v = q[i] % N;
                if (u == tu && v == tv) break;
                for (int t = 0; t < 4; t++)
                {
                    int u2 = u + Turns.All[t].dx, v2 = v + Turns.All[t].dy;
                    if (u2 < 0 || v2 < 0 || u2 >= N || v2 >= N) continue;
                    int k = u2 * N + v2;
                    if (seen[k]) continue;

                    bool ok = Projection.Walkable(lv, surf, u2, v2, doors, out Surf cell);

                    // A PLATE MAY ONLY BE THE DESTINATION. Stepping on one rewrites
                    // the board, so every step planned after it was planned against
                    // a world that no longer exists. Routing stops there and you
                    // plan again — which also makes standing on a plate a decision
                    // rather than something that happens to you on the way past.
                    if (ok && Level.IsGlyph(cell.t) && !(u2 == tu && v2 == tv)) continue;

                    if (!ok)
                    {
                        if (!(u2 == tu && v2 == tv)) continue;
                        Surf raw = surf[k];
                        if (!raw.has || !Level.IsWalkType(raw.t)) continue;
                        int di = lv.DoorIndexAt(raw.w);
                        if (di < 0 || (doors & (1 << di)) != 0 || KeysHeld() < 1) continue;
                    }

                    seen[k] = true;
                    from[k] = q[i];
                    q.Add(k);
                }
            }

            int end = tu * N + tv;
            if (!seen[end]) return null;
            var outp = new List<int>();
            int cur = end, guard = 0;
            int startK = start.x * N + start.y;
            while (cur != startK)
            {
                outp.Insert(0, cur);
                cur = from[cur];
                if (cur < 0 || guard++ > N * N + 4) return null;
            }
            return outp;
        }

        // ---- the three verbs ------------------------------------------------

        public void TapCell(int u, int v)
        {
            if (walking != null || anim != null || won) return;

            Surf s = surf[u * N + v];
            if (!s.has) { On.Toast?.Invoke("VOID — UNRENDERED"); On.Denied?.Invoke(u, v, Palette.Fault); return; }
            if (!Level.IsWalkType(s.t)) { On.Toast?.Invoke("GRID — NO FOOTING ON THIS FACE"); On.Denied?.Invoke(u, v, Palette.Fault); return; }

            int di = lv.DoorIndexAt(s.w);
            bool shut = di >= 0 && (doors & (1 << di)) == 0;
            if (shut && KeysHeld() < 1)
            {
                On.Toast?.Invoke("LOCKED — COLLECT EVERY NODE");
                Haptics.Buzz(Haptics.Fault);
                On.Denied?.Invoke(u, v, Palette.Lock);
                return;
            }

            List<int> path = PathTo(u, v);
            if (path == null)
            {
                On.Toast?.Invoke(shut ? "NO WAY TO THAT LOCK" : "NOT CONNECTED — FOLD THE ENGINE");
                On.Denied?.Invoke(u, v, Palette.Fault);
                return;
            }

            if (path.Count == 0)
            {
                // Tapping the cell you already stand on. On a plate that PRESSES IT
                // AGAIN — the spring-back puts you back on the plate, and without
                // this the only way to fire it again would be to step off and step
                // back on, which needs a walkable neighbour. A plate cut through a
                // wall of rock does not always have one, and that is a soft lock:
                // standing on the one thing that can change the board, unable to
                // use it.
                if (Level.IsGlyph(s.t))
                {
                    PushUndo();
                    FirePlate(Level.GlyphBit(s.t));
                    AfterAction();
                }
                return;
            }

            PushUndo();
            walking = new Walk { queue = path, from = pos, t = 0 };
        }

        public void AdvanceWalk(float dtMs)
        {
            if (walking == null) return;
            walking.t += dtMs / 105f;
            if (walking.t < 1f) return;

            if (walking.queue.Count == 0) { walking = null; Settle(); return; }
            int k = walking.queue[0];
            walking.queue.RemoveAt(0);
            Surf cell = surf[k];
            if (!cell.has) { walking = null; Settle(); return; }   // the board moved under a queued step

            walking.from = pos;
            pos = cell.w;

            int di = lv.DoorIndexAt(pos);
            if (di >= 0 && (doors & (1 << di)) == 0)
            {
                doors |= 1 << di;
                On.DoorOpened?.Invoke(pos, di);
                On.Toast?.Invoke("LOCK OPEN");
                Haptics.Buzz(Haptics.Move);
            }
            else
            {
                On.Stepped?.Invoke(pos, cell);
            }

            int ki = lv.KeyIndexAt(pos);
            if (ki >= 0 && (kmask & (1 << ki)) == 0)
            {
                kmask |= 1 << ki;
                On.KeyTaken?.Invoke(pos, ki);
                On.Toast?.Invoke("NODE");
                Haptics.Buzz(Haptics.Move);
            }

            int gb = lv.GlyphAt(pos);
            if (gb != 0) FirePlate(gb);

            walking.t = 0;
            if (walking.queue.Count == 0)
            {
                walking = null;
                On.Landed?.Invoke(pos);
                Settle();
                AfterAction();
            }
            else
            {
                ComputeReach();
            }
        }

        public bool CanTurn(int t)
        {
            if (walking != null || anim != null || won || lv == null) return false;
            return Projection.Landing(lv, Turns.All[t].f(M), pos, doors, world, out _);
        }

        public bool TryTurn(int t)
        {
            if (walking != null || anim != null || won) return false;
            Ori m2 = Turns.All[t].f(M);
            if (!Projection.Landing(lv, m2, pos, doors, world, out _)) return false;

            bool axisX = t == 0 || t == 1;
            float to = (t == 1 || t == 2) ? Mathf.PI / 2f : -Mathf.PI / 2f;
            float from = (drag != null && drag.hasAxis && drag.axisX == axisX) ? drag.ang : 0f;
            drag = null;
            PushUndo();
            anim = new Anim
            {
                axisX = axisX, ang = from, from = from, to = to, t = 0,
                dur = 180f + 150f * (Mathf.Abs(to - from) / (Mathf.PI / 2f)),
                turn = t, seat = true
            };
            return true;
        }

        /// <summary>
        /// A REFUSED FOLD IS A PHYSICAL EVENT, not an error message. The cube
        /// commits a few degrees, hits the stop, and slams back — so the answer
        /// arrives in the hand before the toast arrives in the eye.
        /// </summary>
        public void RefuseTurn(int t)
        {
            bool axisX = t == 0 || t == 1;
            float sgn = (t == 1 || t == 2) ? 1f : -1f;
            float from = (drag != null && drag.hasAxis && drag.axisX == axisX) ? drag.ang : sgn * 0.14f;
            drag = null;
            anim = new Anim { axisX = axisX, ang = from, from = from, to = 0, t = 0, dur = 260, turn = -1, hard = true };
            On.FoldRefused?.Invoke(t);
            On.Toast?.Invoke("NO FOOTING THAT WAY");
            Haptics.Buzz(Haptics.Fault);
        }

        public void SpringBack()
        {
            if (drag == null || !drag.hasAxis) { drag = null; return; }
            anim = new Anim { axisX = drag.axisX, ang = drag.ang, from = drag.ang, to = 0, t = 0, dur = 190, turn = -1 };
            drag = null;
        }

        /// <summary>
        /// The detent, as a curve. A plain ease-out arrives at ninety degrees and
        /// stops, which is what a slideshow does. A real latch goes slightly past
        /// its seat and is pulled back into it, so this overshoots by a few degrees
        /// and settles — and that half-frame of coming BACK is most of what makes
        /// the fold feel like a mechanism instead of a transition.
        /// </summary>
        public static float EaseSeat(float p)
        {
            const float c = 1.9f;
            return 1f + (c + 1f) * Mathf.Pow(p - 1f, 3f) + c * Mathf.Pow(p - 1f, 2f);
        }

        /// <summary>A refusal gets no overshoot: arrive early, stop dead.</summary>
        public static float EaseSlam(float p) => 1f - Mathf.Pow(1f - p, 5f);

        public void AdvanceAnim(float dtMs)
        {
            if (anim == null) return;
            anim.t += dtMs;
            float p = Mathf.Min(1f, anim.t / anim.dur);
            float e = anim.hard ? EaseSlam(p) : anim.seat ? EaseSeat(p) : 1f - Mathf.Pow(1f - p, 3f);
            anim.ang = anim.from + (anim.to - anim.from) * e;
            if (p < 1f) return;

            int t = anim.turn;
            anim = null;
            if (t < 0) return;

            M = Turns.All[t].f(M);
            Projection.Landing(lv, M, pos, doors, world, out Surf land);
            pos = land.w;

            int ki = lv.KeyIndexAt(pos);
            if (ki >= 0 && (kmask & (1 << ki)) == 0)
            {
                kmask |= 1 << ki;
                On.KeyTaken?.Invoke(pos, ki);
                On.Toast?.Invoke("NODE");
            }

            turns++;
            On.FoldLanded?.Invoke(t);
            Haptics.Buzz(Haptics.Fold);
            Settle();
            AfterAction();
        }

        // ---- plates ---------------------------------------------------------

        public void FirePlate(int bit)
        {
            world ^= bit;
            // THE CLOCK IS FOR THE PLATE BITS ONLY. An everted world with no plate
            // in it is a standing state, not a countdown, so the clock is armed
            // by bits 1 and 2 and is blind to bit 4 — otherwise stepping on an
            // everter would start a five-second timer that reverts a polarity
            // nobody asked to be temporary.
            plateT = (world & ~Level.Everted) != 0 ? PlateMs : 0;
            lv.ClearEff();
            surf = Projection.Project(N, lv.Eff(world), M, Everted);
            On.PlateFired?.Invoke(pos, bit);
            ComputeReach();
            On.Settled?.Invoke();
            Haptics.Buzz(Haptics.Move);
        }

        int _lastPlateTick = -1;

        public void StepPlateClock(float dtMs, bool screenUp)
        {
            if (world == 0 || lv == null || won || screenUp) return;

            // AN EXPIRY WAITS OUT A FOLD IN FLIGHT. A fold is a third of a second
            // of the board rotating toward a landing that was checked against the
            // world it started in; reverting the material half way through would
            // resolve it against a cube that no longer exists. So the clock is
            // allowed to sit at zero for the tail of a rotation, which costs the
            // player nothing they could have spent and costs the state machine one
            // whole class of bug.
            if (plateT <= 0f)
            {
                if (anim == null && drag == null) RevertWorld();
                return;
            }

            float was = plateT;
            plateT = Mathf.Max(0f, plateT - dtMs);
            int lo = Mathf.CeilToInt(plateT / 1000f);
            if (lo != Mathf.CeilToInt(was / 1000f) && lo > 0 && lo <= 3 && lo != _lastPlateTick)
            {
                _lastPlateTick = lo;
                On.PlateTick?.Invoke(lo);
            }
        }

        /// <summary>
        /// THE SPRING BACK. Everything FirePlate does, aimed the other way — plus
        /// the one thing a plate never has to do: deal with a player who is now
        /// inside the rock.
        ///
        /// It takes an undo point FIRST. The revert is the only thing in the game
        /// that moves you without you having asked, so the state you were in when
        /// it landed has to be recoverable, or a five-second limit would be a
        /// five-second trap.
        /// </summary>
        public void RevertWorld()
        {
            PushUndo();
            // EVERSION SURVIVES THE CLOCK, AND THAT IS THE WHOLE DIFFERENCE.
            //
            // A plate is a five-second window: it changes the board and takes it
            // back, and the pressure is the countdown. An everter changes which
            // FACE of the solid is the surface, and nothing about that is
            // temporary — you are standing on the far side until you stand on
            // another everter.
            //
            // It is not a preference. The solver carries `world` through its
            // search and has no notion of the clock at all, because the clock is
            // real time and the search is not. A polarity that expired would make
            // every cube in Baked.Arc solvable on paper and impossible in the
            // hand, and nothing in the harness could have seen it.
            world &= Level.Everted;
            plateT = 0;
            _lastPlateTick = -1;
            walking = null;      // any queued route dies with the world it was planned in
            lv.ClearEff();
            surf = Projection.Project(N, lv.Eff(world), M, Everted);
            On.Reverted?.Invoke();
            ThrowToPlate();
            ComputeReach();
            On.Settled?.Invoke();
            AfterAction();
        }

        /// <summary>
        /// Put the player back on the nearest plate if the world they have just
        /// been returned to has no footing where they stand.
        ///
        /// LANDING ON A PLATE DOES NOT FIRE IT. Same rule as folding onto one: you
        /// press a plate with a foot, and being put on one is not pressing it.
        /// Otherwise the spring-back would flip the world straight back and start a
        /// clock nobody asked for.
        /// </summary>
        public bool ThrowToPlate()
        {
            Int3 v = Projection.ViewOf(N, M, pos);
            bool here = Projection.SurfaceAt(N, surf, M, pos, out Int3 hv);
            bool onPlate = here && Projection.Walkable(lv, surf, hv.x, hv.y, doors)
                                && Level.IsGlyph(surf[hv.x * N + hv.y].t);
            if (onPlate) return false;

            bool found = false;
            Surf best = default;
            int bd = int.MaxValue;

            for (int u = 0; u < N; u++)
                for (int w = 0; w < N; w++)
                {
                    if (!Projection.Walkable(lv, surf, u, w, doors, out Surf cell)) continue;
                    if (!Level.IsGlyph(cell.t)) continue;
                    int d = (u - v.x) * (u - v.x) + (w - v.y) * (w - v.y);
                    if (d < bd) { bd = d; best = cell; found = true; }
                }

            // A cube can only be in a flipped world if it has a plate in it, so the
            // search above cannot come up empty in practice. The fallback is the
            // old behaviour — nearest square of any kind — because a marker with
            // nowhere to stand would be a crash, and a crash is worse than a bad
            // landing.
            if (!found)
            {
                if (here && Projection.Walkable(lv, surf, hv.x, hv.y, doors)) return false;
                for (int u = 0; u < N; u++)
                    for (int w = 0; w < N; w++)
                    {
                        if (!Projection.Walkable(lv, surf, u, w, doors, out Surf cell)) continue;
                        int d = (u - v.x) * (u - v.x) + (w - v.y) * (w - v.y);
                        if (d < bd) { bd = d; best = cell; found = true; }
                    }
            }

            if (!found) return false;
            pos = best.w;
            On.ThrownToPlate?.Invoke(pos);
            On.Toast?.Invoke("RETURNED TO PLATE");
            Haptics.Buzz(Haptics.Fault);
            return true;
        }

        // ---- two reads that change nothing ----------------------------------

        /// <summary>
        /// THE ANTIPODE: the cell diametrically opposite the one you are standing
        /// in — through the centre, not around the outside.
        ///
        /// STRAIGHT THROUGH, NOT ACROSS. Only the DEPTH of the view coordinates is
        /// flipped: same column, opposite end. The world-space diagonal opposite
        /// reads wrong — a hole you are looking INTO shows you what is directly
        /// behind it, not something off to one side. This one is behind you by
        /// definition from wherever you are reading, and it re-aims itself every
        /// time the engine folds.
        /// </summary>
        public Int3 Antipode()
        {
            Int3 v = Projection.ViewOf(N, M, pos);
            return Projection.WorldOf(N, M, new Int3(v.x, v.y, N - 1 - v.z));
        }

        public enum Special { None, Node, Lock, Core }

        /// <summary>
        /// What can be seen through the horizon at the far side of the world, and
        /// that is ALL it does: no reach, no route, no bearing on any fold. It is a
        /// look through a hole, and the only reward is noticing.
        ///
        /// Which is worth having precisely because it costs nothing. A player who
        /// never sees it has lost nothing; a player who does has found out what the
        /// thing they are moving actually is.
        /// </summary>
        public Special ThroughLook()
        {
            if (lv == null) return Special.None;
            Int3 a = Antipode();
            // a thing that is rock in this world is not in this world, and the far
            // side of a hole is no exception — the same rule the board draws by
            if (!Level.IsWalkType(Level.EffType(lv.vox[lv.Vidx(a)], world))) return Special.None;
            if (a == lv.goal) return Special.Core;
            for (int i = 0; i < lv.keys.Count; i++)
                if ((kmask & (1 << i)) == 0 && lv.keys[i] == a) return Special.Node;
            for (int i = 0; i < lv.doors.Count; i++)
                if (lv.doors[i] == a) return Special.Lock;
            return Special.None;
        }

        /// <summary>
        /// ONE STEP AWAY, NOT THROUGH THE CENTRE.
        ///
        /// The opposite of the antipode: real, walkable neighbours — the four cells
        /// a single step from where you stand — checked for whether one of them is
        /// the core, an uncollected node, or a lock. Nothing here changes what you
        /// may do; it only tells the eye where the next useful step is before the
        /// hand has to work it out.
        /// </summary>
        public void NearSpecials(List<(int u, int v, Special what)> into)
        {
            into.Clear();
            if (lv == null || surf == null) return;
            Int3 p = Projection.ViewOf(N, M, pos);
            for (int t = 0; t < 4; t++)
            {
                int u2 = p.x + Turns.All[t].dx, v2 = p.y + Turns.All[t].dy;
                if (!Projection.Walkable(lv, surf, u2, v2, doors, out Surf cell)) continue;
                if (cell.w == lv.goal) { into.Add((u2, v2, Special.Core)); continue; }
                int ki = lv.KeyIndexAt(cell.w);
                if (ki >= 0 && (kmask & (1 << ki)) == 0) { into.Add((u2, v2, Special.Node)); continue; }
                int di = lv.DoorIndexAt(cell.w);
                if (di >= 0) { into.Add((u2, v2, Special.Lock)); }
            }
        }

        // ---- the hint -------------------------------------------------------

        /// <summary>The first move of an optimal line from here, and nothing more.</summary>
        public bool Hint(out Act act)
        {
            act = Act.None;
            if (lv == null || won || hintsLeft <= 0) return false;
            SolveResult r = Solver.Solve(lv, 40, new SolveState
            {
                pos = pos, ori = Turns.OriIndex(M), kmask = kmask, doors = doors, world = world
            });
            if (!r.ok || !r.hasFirst) return false;
            hintsLeft--;
            act = r.first;
            return true;
        }

        // ---- after every action ---------------------------------------------

        public event Action Finished;

        void AfterAction()
        {
            if (pos == lv.goal) { Finish(); return; }
            // The dead-end check is the same question the live readout asks every
            // time the state changes, so there is one run and this is the trigger.
            Remain.Schedule(this);
        }

        void Finish()
        {
            won = true;
            SolveClock.Stop();
            Finished?.Invoke();
            On.Won?.Invoke();
        }

        // ---- the live readout ----------------------------------------------

        public readonly RemainSolver Remain = new RemainSolver();
    }
}
