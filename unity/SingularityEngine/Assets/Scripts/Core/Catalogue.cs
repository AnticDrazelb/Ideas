using System;
using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>
    /// THE FIVE HUNDRED, AND THEY ARE THE SAME FIVE HUNDRED FOR EVERYBODY.
    ///
    /// The ladder used to be minted on the device: cube four hundred was a pure
    /// function of the number four hundred, so it was the same puzzle everywhere,
    /// which was the right instinct and cost the game its difficulty curve. The
    /// audit measured where — mean par climbs 2.71 to 5.17 by cube 120 and then
    /// stops, going backwards over the last two tenths while the board size sits
    /// at nine. From the halfway point the game was the same difficulty forever.
    ///
    /// So the ladder is cut once, offline, out of a hundred thousand candidates,
    /// and shipped as text. What that buys is not only a curve: it is the first
    /// time this game can END, and the first time a cube can be chosen for being
    /// GOOD rather than for clearing a band.
    ///
    /// IT IS A TEXT ASSET AND NOT A C# ARRAY, deliberately, and for the same
    /// reason there is no ProjectSettings.asset in this repository: five hundred
    /// literals is a file nobody can review and two people cannot merge. One cube
    /// a line — par, steps, identity, share code — so a change to the catalogue
    /// is a diff somebody can read.
    ///
    /// THE ORDER IS FROZEN THE DAY IT SHIPS. Progress is an index into this list.
    /// Reorder or replace a cube and every player's save now points at a
    /// different puzzle, with their bests and pars attached to numbers that
    /// changed meaning underneath them. After release it is append-only, and the
    /// harness pins the whole thing by checksum so an accidental edit fails a
    /// build instead of shipping.
    /// </summary>
    public static class Catalogue
    {
        public struct Entry
        {
            public int par, steps;
            public string id, code;
        }

        static Entry[] _all;

        /// <summary>Every cube, in order. Empty if the asset is missing.</summary>
        public static Entry[] All => _all ?? (_all = Array.Empty<Entry>());

        public static int Count => All.Length;

        /// <summary>
        /// Hand the catalogue its text. The game calls this from Resources at boot
        /// and the harness calls it from the file it just cut, which is what keeps
        /// the two reading exactly the same thing.
        /// </summary>
        public static void Load(string text)
        {
            var outp = new List<Entry>();
            if (!string.IsNullOrEmpty(text))
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    // A DASH IS A HOLE AND IT KEEPS ITS PLACE. A slot the cut could
                    // not fill must still occupy its number, or every cube after it
                    // moves and every save in the world points one puzzle to the
                    // left.
                    if (line == "-") { outp.Add(default); continue; }

                    string[] p = line.Split(' ');
                    if (p.Length < 4) { outp.Add(default); continue; }
                    if (!int.TryParse(p[0], out int par) || !int.TryParse(p[1], out int steps))
                    { outp.Add(default); continue; }
                    outp.Add(new Entry { par = par, steps = steps, id = p[2], code = p[3] });
                }
            _all = outp.ToArray();
        }

        public static bool Has(int level)
            => level >= 1 && level <= All.Length && !string.IsNullOrEmpty(All[level - 1].code);

        /// <summary>
        /// The cube at a level number, decoded. Null if there is not one — the
        /// caller falls back to the generator exactly as it always has, which is
        /// what keeps a missing asset a degraded game rather than no game.
        /// </summary>
        public static Level Get(int level)
        {
            if (!Has(level)) return null;
            Entry e = All[level - 1];
            Level lv = ShareCode.Decode(e.code);
            if (lv == null) return null;
            lv.level = level;
            lv.par = e.par;
            lv.steps = e.steps;
            lv.name = Vaults.LevelName(level);
            return lv;
        }

        /// <summary>
        /// Every identity in the catalogue, for the Forge's duplicate check. Eight
        /// characters each — four kilobytes for the whole ladder — because
        /// computing them would be twenty-four canonical forms a cube and the
        /// better part of a minute on a phone.
        /// </summary>
        public static HashSet<string> Ids
        {
            get
            {
                if (_ids != null) return _ids;
                _ids = new HashSet<string>();
                foreach (Entry e in All) if (!string.IsNullOrEmpty(e.id)) _ids.Add(e.id);
                return _ids;
            }
        }
        static HashSet<string> _ids;

        /// <summary>
        /// A checksum over the whole ladder, in order. The one number that says
        /// "this is the catalogue that shipped" — see the note on the class about
        /// why the order is not allowed to move.
        /// </summary>
        public static string Stamp()
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (Entry e in All)
                {
                    string s = e.id ?? "-";
                    for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619u;
                    h = (h ^ (uint)e.par) * 16777619u;
                }
                return h.ToString("x8");
            }
        }
    }
}
