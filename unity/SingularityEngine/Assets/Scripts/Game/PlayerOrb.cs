using System.Collections.Generic;
using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// You.
    ///
    /// A black hole with a face, and the face is not decoration. It is the only
    /// thing in the game that reacts to the player rather than to the rules, and
    /// it is doing three jobs at once:
    ///
    ///   IT LOOKS WHERE YOU ARE GOING. While walking, ahead; while folding, down;
    ///   otherwise back to centre. That is a free readout of the thing the player
    ///   just asked for, delivered by something they are already watching.
    ///
    ///   IT BLINKS. Ninety milliseconds closed, then a fresh irregular wait, so it
    ///   never falls into a rhythm you can hear ticking.
    ///
    ///   AND IT GETS BORED. Every five and a half seconds of a player thinking, it
    ///   bounces once on the spot. No sound, no particles, nothing that could be
    ///   mistaken for the game asking for something — just a small sign of life in
    ///   the corner of the eye of somebody staring at a puzzle. That is the whole
    ///   of "surprise and delight" in about eight lines: a character that does
    ///   something when you do nothing.
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
        SpriteRenderer _body;
        SpriteRenderer _eyeL, _eyeR;

        // ---- the face -------------------------------------------------------
        string _mood = "";
        float _moodT, _moodDur;
        float _blink = 1800f, _nextBlink = 1800f;
        float _lookX, _lookY, _tgtX, _tgtY, _idle;

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

            _body = Sprite("body", "player", Palette.Arc, 0.70f, 100);
            _eyeL = Sprite("eyeL", "eye", Palette.Void, 0.16f, 102);
            _eyeR = Sprite("eyeR", "eye", Palette.Void, 0.16f, 102);
        }

        SpriteRenderer Sprite(string name, string glyph, Color col, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Glyphs.For(glyph);
            sr.sharedMaterial = Glyphs.Material;
            sr.color = col;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * size;
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

            StepFace(dtMs);
            StepTrail(dtMs);
            Place();
        }

        void StepFace(float dt)
        {
            if (_moodDur > 0f)
            {
                _moodT += dt;
                if (_moodT >= _moodDur) { _mood = ""; _moodDur = 0f; }
            }

            // the blink: closed for 90ms, then a fresh irregular wait
            _blink -= dt;
            if (_blink < -90f)
            {
                _blink = _nextBlink;
                _nextBlink = 1500f + Random.value * 3400f;
            }

            if (_s.walking != null)
            {
                Int3 from = Projection.ViewOf(_s.N, _s.M, _s.walking.from);
                Int3 now = Projection.ViewOf(_s.N, _s.M, _s.pos);
                _tgtX = Mathf.Clamp(now.x - from.x, -1f, 1f);
                _tgtY = Mathf.Clamp(now.y - from.y, -1f, 1f);
                _idle = 0f;
            }
            else if (_s.anim != null || _s.drag != null)
            {
                _tgtX = 0f; _tgtY = -0.25f; _idle = 0f;
            }
            else
            {
                _idle += dt;
                if (_idle > 4000f)
                {
                    // after four seconds of thinking, the eyes wander
                    float ph = (_idle - 4000f) / 2600f;
                    _tgtX = Mathf.Sin(ph * 1.9f) * 0.85f;
                    _tgtY = Mathf.Sin(ph * 0.7f) * 0.30f;
                }
                else { _tgtX = 0f; _tgtY = 0f; }
            }

            float k = Mathf.Min(1f, dt / 90f);
            _lookX += (_tgtX - _lookX) * k;
            _lookY += (_tgtY - _lookY) * k;

            // and it gets bored
            if (_idle > 5500f && _hop >= 1f && !_s.won)
            {
                _idle = 4000f;
                Hop();
            }

            _hop = Mathf.Min(1f, _hop + dt / 260f);
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
            // The orb rides the cube's own projection while it folds, so it stays
            // on the cell it is standing on rather than hanging in the air. At rest
            // that is exactly the flat grid position.
            Vector3 at = _view.cube.TransformPoint(CubeMesh.CellToObject(_s.N, _s.pos))
                       - _cam.transform.forward * 0.62f;

            // the bounce: a single squashed arc, not a loop
            float h = 1f - _hop;
            float lift = Mathf.Sin(h * Mathf.PI) * 0.20f;
            float squash = 1f + Mathf.Sin(h * Mathf.PI) * 0.10f;

            _root.position = at + _cam.transform.up * lift;
            _root.rotation = _cam.transform.rotation;
            _root.localScale = new Vector3(1f / Mathf.Max(0.4f, squash), squash, 1f);

            // the eyes
            float open = _blink < 0f ? 0.12f : 1f;
            float wide = _mood == "wide" ? 1.45f : 1f;
            float happy = _mood == "happy" || _mood == "win" ? 0.45f : 1f;
            float ey = _mood == "happy" || _mood == "win" ? 0.06f : 0f;

            float sx = 0.16f * wide;
            float sy = 0.16f * wide * open * happy;

            var offL = new Vector3(-0.15f + _lookX * 0.06f, 0.05f + _lookY * 0.05f + ey, 0f);
            var offR = new Vector3(0.15f + _lookX * 0.06f, 0.05f + _lookY * 0.05f + ey, 0f);

            _eyeL.transform.localPosition = offL;
            _eyeR.transform.localPosition = offR;
            _eyeL.transform.localScale = new Vector3(sx, sy, 1f);
            _eyeR.transform.localScale = new Vector3(sx, sy, 1f);

            // the horizon brightens on a mood and while walking
            float lit = _mood == "" ? 1f : 1.25f;
            _body.color = Palette.Arc * lit;
            _body.transform.localScale = Vector3.one * (0.70f * (_mood == "wide" ? 1.08f : 1f));
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
