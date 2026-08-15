using System.Collections.Generic;
using System.Text;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// THE FORGE — the editor, as a model.
    ///
    /// A CUBE IS EDITED ONE DECK AT A TIME. Direct manipulation of a projected
    /// solid is a lovely demo and a miserable tool: the thing you want to click
    /// is behind the thing you can see, and on a phone your thumb covers both. A
    /// deck is an n-by-n grid you can hit accurately, and the deck stepper is the
    /// only spatial reasoning the editor asks for.
    ///
    /// CUSTOM CUBES ARE NOT RANKED, and that is not an oversight. Every board in
    /// this game is defended by par being the solver's proven minimum on a cube
    /// whose identity can be checked. A cube the player wrote themselves has
    /// neither property — you could author a one-fold cube and post a perfect
    /// score in ten seconds. They keep their own bests locally and touch nothing
    /// else.
    ///
    /// No UnityEngine here on purpose: the whole editor can be driven from a test.
    /// </summary>
    public class Forge
    {
        public struct Tool
        {
            public char k;
            public string n;
            public Tool(char k, string n) { this.k = k; this.n = n; }
        }

        public static readonly Tool[] Tools =
        {
            new Tool('.', "VOID"),    new Tool('#', "LATTICE"), new Tool('+', "TRACE"),
            new Tool('A', "PLATE A"), new Tool('B', "PLATE B"),
            new Tool('S', "START"),   new Tool('G', "EXIT"),    new Tool('K', "NODE"),
            new Tool('D', "LOCK"),    new Tool('C', "WIPE DECK"),
        };

        /// <summary>The generator reaches nine, so the Forge does.</summary>
        public static readonly int[] Sizes = { 5, 6, 7, 8, 9 };

        public int n;
        public char[] vox;
        public Int3? start, goal;
        public List<Int3> keys = new List<Int3>();
        public List<Int3> doors = new List<Int3>();
        public int layer;
        public char tool = '+';
        public string name = "";

        /// <summary>The id of the saved cube being edited, or null for a new one.</summary>
        public string from;

        // ---- construction ----------------------------------------------------

        public static Forge New(int n = 5)
        {
            var f = new Forge { n = n, vox = new char[n * n * n] };
            for (int i = 0; i < f.vox.Length; i++) f.vox[i] = '.';
            return f;
        }

        /// <summary>
        /// Start and goal are optional here and nowhere else. A SAVED cube always
        /// has both — nothing without them passes validation — but a DRAFT is a
        /// cube in the middle of being built, and the first thing a player does is
        /// place voxels, not a start.
        /// </summary>
        public static Forge From(MadeCube rec)
        {
            Level lv = ShareCode.Decode(rec.code);
            if (lv == null) return New(rec.n <= 0 ? 5 : rec.n);
            var f = new Forge
            {
                n = lv.n,
                vox = (char[])lv.vox.Clone(),
                start = lv.start,
                goal = lv.goal,
                keys = new List<Int3>(lv.keys),
                doors = new List<Int3>(lv.doors),
                name = rec.name ?? "",
                from = rec.key
            };
            return f;
        }

        /// <summary>
        /// Growing keeps everything and adds empty space; shrinking keeps what
        /// still fits and drops what does not, marks included — silently keeping a
        /// start that is now outside the cube would fail validation with no way to
        /// see why.
        /// </summary>
        public void Resize(int to)
        {
            var v = new char[to * to * to];
            for (int i = 0; i < v.Length; i++) v[i] = '.';
            int m = to < n ? to : n;
            for (int y = 0; y < m; y++)
                for (int z = 0; z < m; z++)
                    for (int x = 0; x < m; x++)
                        v[Level.Vidx(to, x, y, z)] = vox[Level.Vidx(n, x, y, z)];

            bool Fits(Int3? w) => w.HasValue && w.Value.x < to && w.Value.y < to && w.Value.z < to;

            var k2 = new List<Int3>();
            var d2 = new List<Int3>();
            for (int i = 0; i < keys.Count && i < doors.Count; i++)
                if (Fits(keys[i]) && Fits(doors[i])) { k2.Add(keys[i]); d2.Add(doors[i]); }

            proven = false;
            n = to; vox = v; keys = k2; doors = d2;
            if (!Fits(start)) start = null;
            if (!Fits(goal)) goal = null;
            if (layer >= to) layer = to - 1;
            SaveDraft();
        }

        // ---- marks -----------------------------------------------------------

        public char MarkAt(Int3 w)
        {
            if (start.HasValue && start.Value == w) return 'S';
            if (goal.HasValue && goal.Value == w) return 'E';
            if (keys.IndexOf(w) >= 0) return 'N';
            if (doors.IndexOf(w) >= 0) return 'L';
            return '\0';
        }

        /// <summary>
        /// A mark can only sit on something you can stand on, so emptying a cell
        /// has to take its role with it rather than leave a start floating in a void.
        /// </summary>
        void Unmark(Int3 w)
        {
            if (start.HasValue && start.Value == w) start = null;
            if (goal.HasValue && goal.Value == w) goal = null;
            int i = keys.IndexOf(w);
            if (i >= 0) { keys.RemoveAt(i); if (i < doors.Count) doors.RemoveAt(i); }
            int j = doors.IndexOf(w);
            if (j >= 0) { doors.RemoveAt(j); if (j < keys.Count) keys.RemoveAt(j); }
        }

        public void Apply(int x, int z)
        {
            // ANY EDIT UNPROVES IT. A par is the solver's answer about one exact
            // cube, so the moment a cell changes the old answer is about a cube
            // that no longer exists — and a stale VERIFIED is worse than none.
            proven = false;

            var w = new Int3(x, layer, z);
            int i = Level.Vidx(n, x, layer, z);

            if (tool == 'S' || tool == 'G')
            {
                if (!Level.IsWalkType(vox[i])) vox[i] = '+';
                if (tool == 'S') { if (goal.HasValue && goal.Value == w) goal = null; start = w; }
                else { if (start.HasValue && start.Value == w) start = null; goal = w; }
            }
            else if (tool == 'K' || tool == 'D')
            {
                // Nodes and locks are added and removed IN PAIRS, because a node
                // with no lock is decoration and a lock with no node is a wall.
                int at = tool == 'K' ? keys.IndexOf(w) : doors.IndexOf(w);
                if (at >= 0)
                {
                    if (at < keys.Count) keys.RemoveAt(at);
                    if (at < doors.Count) doors.RemoveAt(at);
                }
                else
                {
                    if (!Level.IsWalkType(vox[i])) vox[i] = '+';
                    if (tool == 'K') keys.Add(w); else doors.Add(w);
                }
            }
            else if (tool == 'C')
            {
                ClearDeck();
            }
            else
            {
                vox[i] = tool;
                if (!Level.IsWalkType(tool)) Unmark(w);
            }

            SaveDraft();
        }

        public void ClearDeck()
        {
            proven = false;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    vox[Level.Vidx(n, x, layer, z)] = '.';
                    Unmark(new Int3(x, layer, z));
                }
            SaveDraft();
        }


        // ---- the draft -------------------------------------------------------
        //
        // AN UNSAVED CUBE SURVIVES THE APP BEING KILLED.
        //
        // Android reclaims a backgrounded Activity whenever it likes, and half an
        // hour of placing voxels is exactly the kind of work a player does not get
        // to do twice. The draft mirrors the editor after every change; it is not
        // a save, it is the editor's own memory of where it was — an unverified,
        // unnamed, half-built cube is not a thing the shelf can hold, and it is
        // the thing most worth not losing.
        //
        // Store already had the field. Nothing ever wrote it, which is the worst
        // of both: the save file carried a promise the editor never kept.
        //
        // Its own format rather than a share code, because a share code needs a
        // start and an exit and the whole point of a draft is the cube that does
        // not have them yet. Every field is either a number, a base64url id or a
        // name already folded to capitals, digits, spaces and dashes — so no
        // separator can ever appear inside a value.

        const char DField = '|', DList = ',', DAxis = '.';

        static string Pt(Int3? w) => w.HasValue ? w.Value.x + "" + DAxis + w.Value.y + DAxis + w.Value.z : "";

        static Int3? UnPt(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            string[] a = s.Split(DAxis);
            if (a.Length != 3) return null;
            if (!int.TryParse(a[0], out int x) || !int.TryParse(a[1], out int y) || !int.TryParse(a[2], out int z))
                return null;
            return new Int3(x, y, z);
        }

        static string PtList(List<Int3> l)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(DList); sb.Append(Pt(l[i])); }
            return sb.ToString();
        }

        static List<Int3> UnPtList(string s)
        {
            var l = new List<Int3>();
            if (string.IsNullOrEmpty(s)) return l;
            foreach (string part in s.Split(DList))
            {
                Int3? w = UnPt(part);
                if (w.HasValue) l.Add(w.Value);
            }
            return l;
        }

        public void SaveDraft()
        {
            var sb = new StringBuilder();
            sb.Append('1').Append(DField).Append(n).Append(DField).Append(layer).Append(DField)
              .Append(from ?? "").Append(DField).Append(name ?? "").Append(DField)
              .Append(new string(vox)).Append(DField)
              .Append(Pt(start)).Append(DField).Append(Pt(goal)).Append(DField)
              .Append(PtList(keys)).Append(DField).Append(PtList(doors));
            Store.Data.draft = sb.ToString();
            Store.Save();
        }

        public static void DropDraft()
        {
            Store.Data.draft = "";
            Store.Save();
        }

        /// <summary>
        /// The editor the player left, or a fresh one. A draft that does not parse
        /// is a draft from an older build or a corrupted save, and the answer to
        /// both is the same as the answer to no draft at all.
        /// </summary>
        public static Forge Resume()
        {
            string d = Store.Data.draft;
            if (string.IsNullOrEmpty(d)) return New();

            string[] f = d.Split(DField);
            if (f.Length < 10 || f[0] != "1") return New();
            if (!int.TryParse(f[1], out int n) || n < 2 || n > 9) return New();
            if (f[5].Length != n * n * n) return New();

            int.TryParse(f[2], out int layer);
            var fg = new Forge
            {
                n = n,
                vox = f[5].ToCharArray(),
                from = string.IsNullOrEmpty(f[3]) ? null : f[3],
                name = f[4],
                start = UnPt(f[6]),
                goal = UnPt(f[7]),
                keys = UnPtList(f[8]),
                doors = UnPtList(f[9])
            };
            fg.layer = layer < 0 || layer >= n ? 0 : layer;
            return fg;
        }

        // ---- what comes out --------------------------------------------------

        /// <summary>
        /// A NAME IS THE ONLY FREE TEXT IN THIS GAME, so it is folded to the
        /// character set the interface already speaks — capitals, digits, spaces
        /// and a dash. That is a house-style decision and, usefully, it also means
        /// no authored string can ever be markup: a cube called
        /// <c>&lt;img onerror=...&gt;</c> imported from a stranger's share code is
        /// simply not representable.
        /// </summary>
        public static string CleanName(string s)
        {
            var sb = new StringBuilder();
            bool lastSpace = false;
            foreach (char c0 in (s ?? "").ToUpperInvariant())
            {
                char c = c0;
                bool ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '-';
                if (!ok) continue;
                if (c == ' ')
                {
                    if (lastSpace || sb.Length == 0) continue;
                    lastSpace = true;
                }
                else lastSpace = false;
                sb.Append(c);
                if (sb.Length >= 18) break;
            }
            return sb.ToString().Trim();
        }

        public Level ToLevel()
        {
            return new Level
            {
                n = n,
                vox = (char[])vox.Clone(),
                start = start ?? default,
                goal = goal ?? default,
                keys = new List<Int3>(keys),
                doors = new List<Int3>(doors),
                par = 0,
                name = string.IsNullOrEmpty(name) ? "UNTITLED" : name
            };
        }

        public struct VerifyResult
        {
            public bool ok;
            public string message;
            public Level level;      // carries the proven par when ok
        }

        /// <summary>
        /// WHAT TO DO NEXT, IN ONE SENTENCE.
        ///
        /// An empty grid and ten unlabelled tools is not an editor, it is a puzzle
        /// about an editor. This reads the cube as it stands and names the single
        /// next thing standing between it and a saveable cube — always one thing,
        /// never a checklist, because a checklist is read once and a next step is
        /// read every time it changes.
        ///
        /// It lives here rather than in the screen for the same reason the rest of
        /// this class does: it is a statement about the cube, it needs no engine,
        /// and a test can ask it what it would say.
        /// </summary>
        public (int step, string say) Advice()
        {
            int trace = 0;
            foreach (char c in vox) if (Level.IsWalkType(c)) trace++;

            // enough to be a route rather than a spot — the smallest cube the
            // generator will cut has eight, so the editor asks for eight
            const int Floor = 8;
            if (trace < Floor)
                return (1, "TRACE IS WHAT YOU STAND ON. TAP CELLS TO LAY "
                         + (Floor - trace) + " MORE OF IT.");

            // THE TWO STEPS THAT WERE MISSING, AND WHY THE EDITOR LOOKED BROKEN.
            //
            // This went straight from "lay some trace" to "place a start", and a
            // player who did exactly what it said built one flat deck — which the
            // validator then refused, correctly, with NO FOLD NEEDED. Following
            // the coach to the letter produced a cube that could not be saved,
            // and nothing anywhere said the word DECK.
            //
            // A cube that folds needs height and it needs the exit somewhere a
            // walk will not reach. Those are the two ideas this whole editor is
            // about, so they are steps rather than something to find out from a
            // refusal.
            if (layer == 0 && Decks() < 2)
                return (2, "A CUBE HAS DECKS. STEP UP ONE WITH THE ARROW ABOVE.");

            if (Decks() < 2 || trace < 12)
                return (3, "LAY TRACE UP HERE TOO. A FLAT CUBE FOLDS INTO NOTHING.");

            if (!start.HasValue) return (4, "PICK START, THEN TAP THE CELL YOU BEGIN ON.");

            // THE COMMONEST REFUSAL IN THE EDITOR, SAID BEFORE YOU PRESS VERIFY.
            //
            // Across four hundred plausible cubes, more than half came back with
            // START IS BURIED — and it is a rule the deck grid cannot show you,
            // because the deck grid is one horizontal slice and burying is
            // vertical. You are looking at the start; the thing that makes it
            // illegal is on the deck above, which is not on screen.
            //
            // It got worse the moment the coach started doing its job: the step
            // before this one asks for a second deck, and a second deck is
            // exactly what buries a start. So the same test the validator runs is
            // run here, on the same projection, and the answer arrives while the
            // player is still looking at the cell rather than three taps later.
            if (!StartShows())
                return (4, "THAT CELL IS BEHIND ANOTHER ONE. THE START HAS TO BE A FACE THE CUBE SHOWS — THE RINGED CELLS.");

            if (!goal.HasValue)
                return (5, "NOW EXIT — AND PUT IT WHERE A WALK WILL NOT REACH, SO GETTING THERE TAKES A FOLD.");

            if (!proven)
                return (6, missed
                    ? "AN UNSOLVABLE CUBE IS NOT A HARD ONE, IT IS A BROKEN ONE. GIVE THE EXIT A ROUTE AND VERIFY AGAIN."
                    : "HIT VERIFY. THE SOLVER HAS TO PROVE THIS CAN BE DONE BEFORE IT COUNTS.");

            return (7, string.IsNullOrEmpty(name)
                ? "PROVEN. GIVE IT A NAME AND SAVE IT."
                : "PROVEN. SAVE IT — AND YOU GET A CODE, WHICH IS HOW SOMEBODY ELSE PLAYS IT.");
        }


        /// <summary>
        /// Is the start on a face of the unfolded cube? The validator's own test,
        /// on the validator's own projection — a second opinion here would be a
        /// second answer, and the player would get whichever one they asked last.
        /// </summary>
        public bool StartShows()
        {
            if (!start.HasValue) return true;
            if (!Level.IsWalkType(vox[Level.Vidx(n, start.Value)])) return false;

            Level lv = ToLevel();
            lv.ClearEff();
            Surf[] s0 = Projection.Project(n, lv.Eff(0), Ori.Id);
            return Projection.SurfaceAt(n, s0, Ori.Id, start.Value);
        }

        /// <summary>
        /// Is this cell one of the faces the cube shows, or is it behind another?
        ///
        /// THE DECK GRID IS A CROSS-SECTION, NOT A FLOOR PLAN, and that single
        /// misreading is behind most of what goes wrong in this editor. The cube
        /// is viewed along z, so the cell that hides another is not on the deck
        /// above — it is the one further up THIS deck, in plain sight, on the
        /// same screen. Anything solid in front of a cell hides it, gap or no
        /// gap, lattice as much as trace.
        ///
        /// The grid can therefore say so, which is the whole reason this is here:
        /// the frontmost solid of every column gets a lit ring and everything
        /// behind it does not. A player who can see which cells are faces stops
        /// putting starts on the ones that are not.
        /// </summary>
        public bool OnFace(int x, int z)
        {
            for (int zz = n - 1; zz > z; zz--)
                if (vox[Level.Vidx(n, x, layer, zz)] != '.') return false;
            return true;
        }

        /// <summary>How many decks have anything to stand on. A flat cube cannot fold.</summary>
        public int Decks()
        {
            int d = 0;
            for (int y = 0; y < n; y++)
            {
                bool any = false;
                for (int z = 0; z < n && !any; z++)
                    for (int x = 0; x < n && !any; x++)
                        if (Level.IsWalkType(vox[Level.Vidx(n, x, y, z)])) any = true;
                if (any) d++;
            }
            return d;
        }

        /// <summary>
        /// A VERIFY THAT FAILED, so the coach can say the other thing.
        ///
        /// The step that asks for a verify has to survive being wrong: a first
        /// cube very often will not solve, and "hit verify" repeated at somebody
        /// who just did is the least useful sentence available. Failure is part of
        /// the lesson, so the step stays open and changes what it says.
        /// </summary>
        public bool missed;

        /// <summary>Set by a passing Verify and cleared by any edit — see Mark/Unmark.</summary>
        public bool proven;

        /// <summary>
        /// THE ONE ANSWER THE EDITOR OWES YOU: solvable, and new.
        ///
        /// Both are refusals rather than warnings. An unsolvable cube has no par
        /// and nothing in this game can score it; a duplicate is a cube that
        /// already exists, and being told so before you name it is kinder than
        /// being told after.
        /// </summary>
        public VerifyResult Verify()
        {
            if (!start.HasValue || !goal.HasValue)
            { missed = true; return new VerifyResult { ok = false, message = "NEEDS A START AND AN EXIT" }; }

            Level lv = ToLevel();
            Verdict v = Validation.Validate(lv);
            if (!v.ok) { missed = true; return new VerifyResult { ok = false, message = v.why }; }

            lv.par = v.par;
            lv.steps = v.steps;

            var mine = new Dictionary<string, string>();
            foreach (MadeCube m in Store.Data.made) mine[m.name ?? m.key] = m.id;

            Collision? col = Identity.CollisionOf(lv, from, mine);
            if (col.HasValue)
                return new VerifyResult { ok = false, message = "THIS CUBE ALREADY EXISTS — " + Describe(col.Value) };

            proven = true;
            missed = false;
            return new VerifyResult
            {
                ok = true,
                level = lv,
                message = "VERIFIED · PAR " + v.par + " · " + v.steps + " STEPS"
            };
        }

        static string Describe(Collision c)
        {
            if (c.kind == "baked") return "IT IS " + c.name + ", CUBE " + c.level;
            if (c.kind == "minted") return "IT IS CUBE " + c.level + " — " + c.name;
            return "YOU ALREADY BUILT IT: " + c.name;
        }

        public MadeCube Record(Level lv, string src = "built")
        {
            string id = Identity.CanonId(lv);
            return new MadeCube
            {
                key = id,
                id = id,
                name = lv.name,
                n = lv.n,
                par = lv.par,
                code = ShareCode.Encode(lv),
                best = -1,
                tbest = -1
            };
        }

        /// <summary>
        /// Editing a cube and saving it REPLACES it rather than forking it — the
        /// old identity is gone the moment the geometry changed.
        /// </summary>
        public bool Save(out string message)
        {
            name = CleanName(name);
            VerifyResult v = Verify();
            if (!v.ok) { message = v.message; return false; }

            v.level.name = string.IsNullOrEmpty(name) ? "UNTITLED" : name;
            MadeCube rec = Record(v.level);

            if (from != null && from != rec.key)
            {
                MadeCube old = Store.Made(from);
                if (old != null)
                {
                    // a rebuild keeps the records the player earned on the cube it
                    // grew out of, because it is the same author's same attempt
                    rec.best = old.best;
                    rec.tbest = old.tbest;
                    Store.Data.made.Remove(old);
                }
            }

            MadeCube existing = Store.Made(rec.key);
            if (existing != null) Store.Data.made.Remove(existing);
            Store.Data.made.Add(rec);
            from = rec.key;
            Store.Save();

            message = "SAVED · PAR " + rec.par;
            return true;
        }

        public void Delete()
        {
            if (from == null) return;
            MadeCube m = Store.Made(from);
            if (m != null) Store.Data.made.Remove(m);
            from = null;
            // the draft is the editor's memory of THIS cube; leaving it behind
            // would resurrect what was just deleted on the next visit
            Store.Data.draft = "";
            Store.Save();
        }

        /// <summary>Take a cube in from a share code somebody else pasted.</summary>
        public static bool Import(string code, out string message)
        {
            Level lv = ShareCode.Decode(code);
            if (lv == null) { message = "THAT IS NOT A CUBE CODE"; return false; }

            Verdict v = Validation.Validate(lv);
            if (!v.ok) { message = "THAT CUBE IS BROKEN — " + v.why; return false; }
            lv.par = v.par;
            lv.steps = v.steps;
            lv.name = CleanName(lv.name);
            if (string.IsNullOrEmpty(lv.name)) lv.name = "SHARED";

            string id = Identity.CanonId(lv);
            if (Store.Made(id) != null) { message = "YOU ALREADY HAVE THAT CUBE"; return false; }

            var f = New(lv.n);
            MadeCube rec = f.Record(lv, "imported");
            Store.Data.made.Add(rec);
            Store.Save();

            message = "LOADED · " + lv.name + " · PAR " + v.par;
            return true;
        }
    }
}
