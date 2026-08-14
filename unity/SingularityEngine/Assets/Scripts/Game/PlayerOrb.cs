using System.Collections.Generic;
using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// You: A HOLE, AND SOMETHING ON THE OTHER SIDE OF IT.
    ///
    /// This had a face for a while — a glowing ball with two eyes, on the sound
    /// argument that a circle with eyes is a character and a glowing ball is an
    /// effect. It is gone, and the reason is worth keeping next to the code that
    /// replaced it: a black hole does not have a face, and hanging accretion
    /// rings off one made it a planet. The ring ended up doing the work of being
    /// found, which left the hole itself as decoration on its own character.
    ///
    /// What is left has to earn its legibility the way the real thing does: with
    /// an edge, and with what can be seen through it.
    ///
    ///   IT IS A HOLE, NOT A DISC. The horizon is drawn opaque and black, so it
    ///   REMOVES the deck under it rather than covering it — an absence of board,
    ///   which is exactly what a void column is. Stand beside a gap in the cube
    ///   and the two are made of the same nothing.
    ///
    ///   THE EDGE DOES THE FINDING. A photon ring — one thin, very bright circle
    ///   right on the silhouette — with a soft lensing halo outside it and a
    ///   brighter arc travelling round. On a pale deck that ring is the whole
    ///   difference between "a hole in the world" and "a hole in the floor", and
    ///   it is the only reason a black object is allowed to be the player marker
    ///   in a game where dark already means impassable.
    ///
    ///   IT REACTS WITHOUT A FACE. Everything the character used to say with eyes
    ///   now happens as gravity: the rim flares and the halo swells on a node or
    ///   a gate, and it flinches on a refusal. A thing with no face that answers
    ///   what you did is not less expressive than one with eyes — it is
    ///   expressive in the only language it has.
    ///
    ///   AND IT STILL GETS BORED. Every five and a half seconds of a player
    ///   thinking it bounces once on the spot: no sound, no particles, nothing
    ///   that could be mistaken for the game asking for something. A character
    ///   that does something when you do nothing.
    ///
    /// INFALL, NOT EMISSION. The debris around it is spawned out on a ring and
    /// thrown INWARD, so what you see is light being pulled in and going out at
    /// the horizon. Same particle system, same cost — the velocity is simply aimed
    /// the other way. A lamp sheds; this is the opposite of a lamp.
    /// </summary>
    public class PlayerOrb : MonoBehaviour
    {
        Session _s;
        CubeView _view;
        Camera _cam;
        Fx _fx;

        Transform _root;
        SpriteRenderer _hole, _ring, _arc, _halo;

        /// <summary>The marker's radius in cells, before breathing. The original's sz*0.325.</summary>
        const float Rad = 0.325f;

        // ---- what it has to say ---------------------------------------------
        string _mood = "";
        float _moodT, _moodDur;
        float _idle;

        /// <summary>The bounce. 1 means at rest; 0 restarts it.</summary>
        float _hop = 1f;

        readonly List<(Vector3 at, float t)> _trail = new List<(Vector3, float)>();
        float _emit;

        const float TrailMs = 300f;

        public static PlayerOrb Build(Transform under, Session s, CubeView view, Camera cam, Fx fx)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(under, false);
            var p = go.AddComponent<PlayerOrb>();
            p._s = s; p._view = view; p._cam = cam; p._fx = fx;
            p.Compose();
            return p;
        }

        void Compose()
        {
            _root = new GameObject("orb").transform;
            _root.SetParent(transform, false);

            // THE STACK, AND WHY IT IS THIS ORDER.
            //   96  the hole      opaque black; takes the deck away
            //   98  the antipode  drawn in CubeView, INSIDE the hole it shows through
            //   99  the halo      light bending round the outside
            //  100  the ring      the silhouette
            //  101  the arc       the bright part of the ring, travelling
            _hole = Sprite("hole", "hole", Palette.Void, 96, additive: false);
            _halo = Sprite("halo", "halo", Color.white, 99, additive: true);
            _ring = Sprite("ring", "ring", Palette.Arc, 100, additive: true);
            _arc  = Sprite("arc",  "arc",  Palette.Hex("#a5f3fc"), 101, additive: true);
        }

        SpriteRenderer Sprite(string name, string glyph, Color col, int order, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Glyphs.For(glyph);
            sr.sharedMaterial = additive ? Glyphs.Additive : Glyphs.Material;
            sr.color = col;
            sr.sortingOrder = order;
            return sr;
        }

        // ---- events ---------------------------------------------------------

        public void SetMood(string mood, float durMs = 420f)
        {
            _mood = mood;
            _moodT = 0f;
            _moodDur = durMs;
        }

        /// <summary>Restart the bounce. Landing does this; so does boredom.</summary>
        public void Hop() => _hop = 0f;

        public void Reset()
        {
            _trail.Clear();
            _mood = ""; _moodDur = 0f;
            _idle = 0f; _hop = 1f;
        }

        // ---- the frame ------------------------------------------------------

        public void Tick(float dtMs, bool visible)
        {
            if (_s?.lv == null || !visible) { _root.gameObject.SetActive(false); return; }
            _root.gameObject.SetActive(true);

            StepMood(dtMs);
            StepTrail(dtMs);
            Place();
        }

        void StepMood(float dt)
        {
            if (_moodDur > 0f)
            {
                _moodT += dt;
                if (_moodT >= _moodDur) { _mood = ""; _moodDur = 0f; }
            }

            if (_s.walking != null || _s.anim != null || _s.drag != null) _idle = 0f;
            else _idle += dt;

            // and it gets bored
            if (_idle > 5500f && _hop >= 1f && !_s.won)
            {
                _idle = 4000f;
                Hop();
            }

            _hop = Mathf.Min(1f, _hop + dt / 300f);
        }

        /// <summary>
        /// How hard it is reacting, 0..1. This is the whole emotional range of a
        /// thing with no face: the rim brightens and the halo swells, and that is
        /// the entire vocabulary. A node or the core gets the full flare; a refused
        /// fold gets a smaller one, because a flinch is not a cheer.
        /// </summary>
        float Flare()
        {
            if (_moodDur <= 0f) return 0f;
            float left = 1f - _moodT / _moodDur;
            return _mood == "happy" || _mood == "win" ? left
                 : _mood == "wide" ? 0.6f * left
                 : 0f;
        }

        void StepTrail(float dt)
        {
            for (int i = _trail.Count - 1; i >= 0; i--)
            {
                var q = _trail[i];
                q.t += dt;
                if (q.t > TrailMs) _trail.RemoveAt(i); else _trail[i] = q;
            }

            if (_s.won) return;

            Vector3 at = WorldPos();

            // the trail is POSITIONS rather than a stretched sprite, because the
            // board is a grid and the path it took should read as the path it took
            if (_s.walking != null && _trail.Count < 40) _trail.Add((at, 0f));

            _emit -= dt;
            if (_emit <= 0f)
            {
                _emit = _s.walking != null ? 42f : 150f;
                InfallOne(at);
            }
        }

        /// <summary>One mote, spawned out on a ring and aimed back in on a spiral.</summary>
        void InfallOne(Vector3 at)
        {
            if (_fx == null || Store.Data.fx == 0) return;
            float a = Random.value * Mathf.PI * 2f;
            float r = 0.38f + Random.value * 0.14f;
            var from = new Vector3(at.x + Mathf.Cos(a) * r, at.y + Mathf.Sin(a) * r, 0f);

            _fx.Infall(from, new Vector2(at.x, at.y), a, new Color(0.59f, 0.88f, 1f));
        }

        Vector3 WorldPos()
        {
            Int3 v = Projection.ViewOf(_s.N, _s.M, _s.pos);
            float c = (_s.N - 1) * 0.5f;
            return new Vector3(v.x - c, v.y - c, 0f);
        }

        void Place()
        {
            float t = Time.time;

            // IT BREATHES. Two beats at unrelated rates, so the size never settles
            // into a pulse you can count — the difference between something alive
            // and something animating.
            float br = 0.90f + 0.10f * Mathf.Sin(t * 3.1f) + 0.045f * Mathf.Sin(t * 7.3f);
            float rr = Rad * br;

            float flare = Flare();
            float rim = 1f + flare * 0.9f;

            // SQUASH ON THE WAY DOWN, STRETCH ON THE WAY UP — applied to the whole
            // marker rather than to each piece, so the ring and the halo deform with
            // the hole. A squashed hole with a round ring on it is a sticker.
            float sqx = 1f, sqy = 1f, lift = 0f;
            if (_s.walking != null)
            {
                float vv = Mathf.Cos(_s.walking.t * Mathf.PI);      // +1 rising, -1 falling
                sqy = 1f + vv * 0.10f;
                sqx = 1f - vv * 0.09f;
            }
            else if (_hop < 1f)
            {
                float hb = Mathf.Sin(_hop * Mathf.PI * 1.6f) * Mathf.Pow(1f - _hop, 2.2f);
                sqy = 1f - hb * 0.30f;
                sqx = 1f + hb * 0.24f;
                lift = Mathf.Abs(Mathf.Sin(_hop * Mathf.PI * 1.6f)) * Mathf.Pow(1f - _hop, 2f) * 0.10f;
            }

            // The marker rides the cube's own projection while it folds, so it stays
            // on the cell it is standing on rather than hanging in the air. At rest
            // that is exactly the flat grid position.
            Vector3 at = _view.cube.TransformPoint(CubeMesh.CellToObject(_s.N, _s.pos))
                       - _cam.transform.forward * 0.62f;

            Vector3 up = _cam.transform.up;
            // the slow bob, and the squash pivoting near the bottom rather than the
            // centre, so a compressed hole sits down instead of shrinking in place
            at += up * (0.022f * Mathf.Sin(t * 2.2f) + lift - rr * (1f - sqy));

            _root.position = at;
            _root.rotation = _cam.transform.rotation;
            _root.localScale = new Vector3(sqx, sqy, 1f);

            float d = rr * 2f;
            _hole.transform.localScale = Vector3.one * d;
            _ring.transform.localScale = Vector3.one * d;
            _arc.transform.localScale  = Vector3.one * d;

            // the halo swells with the flare; its sprite is baked out to 2.1 radii
            _halo.transform.localScale = Vector3.one * (d * 2.1f * (1f + flare * 0.333f));

            _ring.color = new Color(Palette.Arc.r, Palette.Arc.g, Palette.Arc.b, Mathf.Min(1f, 0.95f * rim));
            _halo.color = new Color(1f, 1f, 1f, Mathf.Min(1f, rim));

            // brighter over one arc, travelling: the whole of "it is turning"
            _arc.transform.localRotation = Quaternion.Euler(0f, 0f, t * 0.7f * Mathf.Rad2Deg);
            _arc.color = new Color(0.647f, 0.953f, 0.988f, Mathf.Min(1f, 0.55f * rim));
        }

        /// <summary>The trail, handed to Fx so it draws in the same additive pass as everything else.</summary>
        public void EmitTrail()
        {
            if (_fx == null) return;
            foreach (var q in _trail)
            {
                float a = 1f - q.t / TrailMs;
                _fx.Blob(q.at, 0.32f * a, new Color(0.22f, 0.84f, 1f, a * a * 0.34f));
            }
        }
    }
}
