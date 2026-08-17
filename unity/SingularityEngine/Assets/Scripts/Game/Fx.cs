using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// The debris and the fronts.
    ///
    /// EVERYTHING IN HERE IS A CLAIM ABOUT MASS. A footfall knocks grit off the
    /// block and the block answers with a square front one cell wide — small, but
    /// it is the difference between a marker moving and a thing landing on stone.
    /// A landed fold rains grit off the WHOLE board rather than off the player,
    /// because the mass that arrived was the cube's and not yours. A refusal gets
    /// chips off the stop it just hit and nothing else, because light means yes in
    /// this game and no has to be told in the dark.
    ///
    /// A SQUARE FRONT, NOT A CIRCLE. The thing that just moved was a grid, and a
    /// circle would be lying about that.
    ///
    /// It is one mesh, rebuilt each frame, drawn additively with no texture. The
    /// effects live in a screen-aligned plane in front of the board and do not
    /// rotate with it — they belong to the event, not to the geometry.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx I { get; private set; }

        public enum Kind { Spark, Chip, Ember }

        struct Particle
        {
            public Vector2 pos, vel;
            public float life, maxLife, size, gravity, drag;
            public Color col;
            public Kind kind;
        }

        struct Ring
        {
            public Vector2 pos;
            public float t, dur, r0, r1, width;
            public Color col;
            public bool square;
        }

        readonly List<Particle> _parts = new List<Particle>();
        readonly List<Ring> _rings = new List<Ring>();

        /// <summary>Quads drawn for exactly one frame — the orb's trail, and nothing else so far.</summary>
        readonly List<(Vector3 at, float size, Color col)> _transient = new List<(Vector3, float, Color)>();

        Mesh _mesh;
        MeshFilter _filter;
        float _z;

        readonly List<Vector3> _v = new List<Vector3>(4096);
        readonly List<Vector2> _uv = new List<Vector2>(4096);
        readonly List<Vector2> _kind = new List<Vector2>(4096);
        readonly List<Color> _col = new List<Color>(4096);
        readonly List<int> _tri = new List<int>(6144);

        /// <summary>
        /// A hard cap, and a lower one when the light is turned down. A player
        /// who has asked for less still gets every beat — they just get it at
        /// forty per cent, because the beats are the game and the confetti is
        /// not. At NONE the debris stops entirely and the beat is carried by the
        /// sound, the haptic and the board itself, all three of which are still
        /// there.
        /// </summary>
        int Cap => Access.Lighting == Access.LightLevel.Full ? 460
                 : Access.Lighting == Access.LightLevel.Reduced ? 130 : 0;
        static float Amount => Access.LightAmount;

        public static Fx Build(Transform under)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(under, false);
            var fx = go.AddComponent<Fx>();
            fx.Init();
            I = fx;
            return fx;
        }

        void Init()
        {
            _filter = gameObject.AddComponent<MeshFilter>();
            var r = gameObject.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            Shader sh = Shader.Find("Singularity/Fx");
            if (sh == null) { Debug.LogError("Singularity/Fx shader missing"); sh = Shader.Find("Sprites/Default"); }
            r.sharedMaterial = new Material(sh) { name = "fx" };

            _mesh = new Mesh { name = "fx" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
        }

        /// <summary>The plane the debris lives in: just in front of the cube, whatever size it is.</summary>
        public void SetBoard(int n) => _z = -(n * 0.87f + 1f);

        public void Clear() { _parts.Clear(); _rings.Clear(); _transient.Clear(); }

        /// <summary>
        /// A soft round mark for this frame only. The orb's trail is POSITIONS
        /// rather than a stretched sprite, because the board is a grid and the path
        /// it took should read as the path it took.
        /// </summary>
        public void Blob(Vector3 at, float size, Color col) => _transient.Add((at, size, col));

        /// <summary>
        /// INFALL. One mote spawned out on a ring and thrown INWARD, so what you
        /// see around the character is light being pulled in and going out at the
        /// horizon. The sideways kick makes it arrive on a spiral rather than
        /// falling straight down a drain.
        /// </summary>
        public void Infall(Vector3 from, Vector2 to, float angle, Color col)
        {
            if (_parts.Count >= Cap) return;
            var at = new Vector2(from.x, from.y);
            Vector2 inward = (to - at) * 2.1f;
            var kick = new Vector2(Mathf.Cos(angle + 1.57f), Mathf.Sin(angle + 1.57f)) * 0.7f;

            _parts.Add(new Particle
            {
                pos = at,
                vel = inward + kick,
                life = 0f, maxLife = 0.52f,
                size = 0.09f,
                gravity = 0f,
                drag = 0.97f,
                col = col,
                kind = Kind.Ember
            });
        }

        // ---- spawning --------------------------------------------------------

        public struct BurstOpts
        {
            public Kind kind;
            public float speed, life, size, gravity, lift, fade;
            public Color col;
            public float angle, spread;   // spread <= 0 means all the way round
        }

        public void Burst(Vector3 world, int n, BurstOpts o)
        {
            // Scaled rather than floored at one: at NONE the answer is none, and
            // a Max(1, …) here is how "no particles" quietly becomes "one".
            n = Mathf.RoundToInt(n * Amount);
            if (n <= 0) return;
            var at = new Vector2(world.x, world.y);

            for (int i = 0; i < n && _parts.Count < Cap; i++)
            {
                float a = o.spread > 0f
                    ? o.angle + (Random.value - 0.5f) * o.spread
                    : Random.value * Mathf.PI * 2f;
                float sp = o.speed * (0.45f + Random.value * 0.85f);

                _parts.Add(new Particle
                {
                    pos = at,
                    vel = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + o.lift),
                    life = 0f,
                    maxLife = o.life * (0.7f + Random.value * 0.6f),
                    size = o.size * (0.6f + Random.value * 0.8f),
                    gravity = o.gravity,
                    drag = o.fade <= 0f ? 0.86f : o.fade,
                    col = o.col,
                    kind = o.kind
                });
            }
        }

        public void Ring2(Vector3 world, float r0, float r1, float dur, Color col, float width, bool square)
        {
            if (Access.Lighting == Access.LightLevel.None) return;
            if (Access.Lighting == Access.LightLevel.Reduced && _rings.Count > 3) return;
            _rings.Add(new Ring
            {
                pos = new Vector2(world.x, world.y),
                t = 0f, dur = dur, r0 = r0, r1 = r1, width = width,
                col = col, square = square
            });
        }

        // ---- the frame -------------------------------------------------------

        public void Tick(float dt)
        {
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                Particle p = _parts[i];
                p.life += dt;
                if (p.life >= p.maxLife) { _parts.RemoveAt(i); continue; }

                p.vel.y -= p.gravity * dt;
                p.vel *= Mathf.Pow(p.drag, dt * 60f * 0.016f);
                p.pos += p.vel * dt;
                _parts[i] = p;
            }

            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                Ring r = _rings[i];
                r.t += dt;
                if (r.t >= r.dur) { _rings.RemoveAt(i); continue; }
                _rings[i] = r;
            }

            RebuildMesh();
        }

        void RebuildMesh()
        {
            _v.Clear(); _uv.Clear(); _kind.Clear(); _col.Clear(); _tri.Clear();

            foreach (Particle p in _parts)
            {
                float k = 1f - p.life / p.maxLife;
                Color c = p.col;
                c.a = k * (p.kind == Kind.Chip ? 0.95f : 0.85f) * Amount;
                float s = p.size * (p.kind == Kind.Ember ? 0.6f + k * 0.6f : k * 0.6f + 0.4f);
                Quad(p.pos, s, s, c, p.kind == Kind.Chip ? 1f : 0f);
            }

            foreach (Ring r in _rings)
            {
                float k = r.t / r.dur;
                float rad = Mathf.Lerp(r.r0, r.r1, 1f - Mathf.Pow(1f - k, 3f));
                Color c = r.col;
                c.a = (1f - k) * 0.8f * Amount;

                // An outline out of four bars, because that is what an outline is
                // and it costs the same as a textured ring without needing one.
                float w = r.width;
                if (r.square)
                {
                    Quad(r.pos + new Vector2(0, rad), rad * 2f + w, w, c, 1f);
                    Quad(r.pos + new Vector2(0, -rad), rad * 2f + w, w, c, 1f);
                    Quad(r.pos + new Vector2(rad, 0), w, rad * 2f + w, c, 1f);
                    Quad(r.pos + new Vector2(-rad, 0), w, rad * 2f + w, c, 1f);
                }
                else
                {
                    // A RING IS AN ANNULUS, NOT A NECKLACE.
                    //
                    // This was twenty round dots spaced around the circumference,
                    // which works at a one-cell radius and disappears at any other:
                    // the collapse ring grows to several cells across, so twenty
                    // specks a tenth of a cell wide ended up more than a cell apart
                    // and the round front simply was not there. What you saw was the
                    // square front alone — which is why the biggest moment in the
                    // game came out square when the original draws both.
                    //
                    // Built from the geometry instead, so the width is the width at
                    // every radius. The uv sits at the centre of the shape mask so
                    // the fragment is solid: the triangles ARE the ring, and there
                    // is nothing for a mask to round off.
                    const int seg = 48;
                    float ri = rad - w * 0.5f, ro = rad + w * 0.5f;
                    for (int i = 0; i < seg; i++)
                    {
                        float a0 = i / (float)seg * Mathf.PI * 2f;
                        float a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                        var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                        var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                        Band(r.pos + d0 * ri, r.pos + d1 * ri, r.pos + d1 * ro, r.pos + d0 * ro, c);
                    }
                }
            }

            foreach (var t in _transient) Quad(new Vector2(t.at.x, t.at.y), t.size, t.size, t.col, 0f);
            _transient.Clear();

            _mesh.Clear();
            if (_v.Count == 0) return;
            _mesh.SetVertices(_v);
            _mesh.SetUVs(0, _uv);
            _mesh.SetUVs(1, _kind);
            _mesh.SetColors(_col);
            _mesh.SetTriangles(_tri, 0, false);
            _mesh.RecalculateBounds();
        }

        /// <summary>
        /// Four corners, filled flat. The uv is pinned to the middle of the shape
        /// mask so every fragment is solid — this is for shapes the TRIANGLES
        /// describe, where a disc or square mask would only eat the edges off it.
        /// </summary>
        void Band(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = _v.Count;
            _v.Add(new Vector3(a.x, a.y, _z));
            _v.Add(new Vector3(b.x, b.y, _z));
            _v.Add(new Vector3(c.x, c.y, _z));
            _v.Add(new Vector3(d.x, d.y, _z));

            var mid = new Vector2(0.5f, 0.5f);
            _uv.Add(mid); _uv.Add(mid); _uv.Add(mid); _uv.Add(mid);

            var k = new Vector2(0f, 0f);
            _kind.Add(k); _kind.Add(k); _kind.Add(k); _kind.Add(k);
            _col.Add(col); _col.Add(col); _col.Add(col); _col.Add(col);

            _tri.Add(i); _tri.Add(i + 1); _tri.Add(i + 2);
            _tri.Add(i); _tri.Add(i + 2); _tri.Add(i + 3);
        }

        void Quad(Vector2 at, float w, float h, Color c, float square)
        {
            int b = _v.Count;
            float hw = w * 0.5f, hh = h * 0.5f;
            _v.Add(new Vector3(at.x - hw, at.y - hh, _z));
            _v.Add(new Vector3(at.x + hw, at.y - hh, _z));
            _v.Add(new Vector3(at.x + hw, at.y + hh, _z));
            _v.Add(new Vector3(at.x - hw, at.y + hh, _z));

            _uv.Add(new Vector2(0, 0)); _uv.Add(new Vector2(1, 0));
            _uv.Add(new Vector2(1, 1)); _uv.Add(new Vector2(0, 1));

            var k = new Vector2(square, 0);
            _kind.Add(k); _kind.Add(k); _kind.Add(k); _kind.Add(k);
            _col.Add(c); _col.Add(c); _col.Add(c); _col.Add(c);

            _tri.Add(b); _tri.Add(b + 1); _tri.Add(b + 2);
            _tri.Add(b); _tri.Add(b + 2); _tri.Add(b + 3);
        }

        // ---- the named events ------------------------------------------------
        //
        // Each of these is one beat of the original's effects budget, kept as a
        // named method rather than a pile of call-site parameters so that the
        // reasons stay attached to the numbers.

        /// <summary>A footfall knocks grit off the block, and the block answers with a square front one cell wide.</summary>
        public void Footfall(Vector3 at, Color chip)
        {
            Burst(at + Vector3.up * -0.2f, 4, new BurstOpts
            { kind = Kind.Chip, speed = 0.34f, life = 0.42f, size = 0.11f, gravity = 2.6f, fade = 0.90f, col = chip });
            Ring2(at, 0.30f, 0.62f, 0.26f, Palette.Arc, 0.05f, true);
        }

        /// <summary>The arrival: a heavier knock, and the last of the walk's momentum going into the stone.</summary>
        public void Land(Vector3 at, Color chip)
        {
            Burst(at + Vector3.up * -0.24f, 7, new BurstOpts
            { kind = Kind.Chip, speed = 0.44f, life = 0.46f, size = 0.13f, gravity = 3.0f, fade = 0.80f, col = chip });
            Burst(at, 5, new BurstOpts
            { kind = Kind.Ember, speed = 0.20f, lift = 0.26f, life = 0.70f, size = 0.11f, gravity = -0.18f, col = Palette.Arc });
        }

        /// <summary>
        /// A LOCK OPENING IS THE LOUDEST THING THAT IS NOT A FOLD. It is the only
        /// moment a node stops being a number and becomes a thing that did
        /// something, so it gets the whole kit.
        /// </summary>
        public void LockOpen(Vector3 at)
        {
            Burst(at, 26, new BurstOpts { kind = Kind.Spark, speed = 1.5f, life = 0.64f, size = 0.16f, gravity = 1.1f, col = Palette.Node });
            Burst(at, 10, new BurstOpts { kind = Kind.Chip, speed = 1.1f, life = 0.76f, size = 0.13f, gravity = 3.4f, col = Palette.Node * 0.7f });
            Ring2(at, 0.2f, 2.6f, 0.52f, Palette.Node, 0.07f, false);
        }

        public void NodeTaken(Vector3 at)
        {
            Burst(at, 20, new BurstOpts { kind = Kind.Spark, speed = 1.3f, life = 0.56f, size = 0.15f, gravity = 0.2f, col = Palette.Node });
            Burst(at, 8, new BurstOpts { kind = Kind.Ember, speed = 0.26f, lift = 0.40f, life = 0.90f, size = 0.13f, gravity = -0.24f, col = Palette.Node });
            Ring2(at, 0.15f, 1.9f, 0.46f, Palette.Node, 0.06f, false);
        }

        /// <summary>
        /// The landing is the verb. A square front leaves the cube at BOARD scale,
        /// and then grit rains off the whole face — seeded across all of it rather
        /// than at the player, because the mass that arrived was the cube's.
        /// </summary>
        public void FoldLanded(int n, Vector3 playerAt)
        {
            Ring2(Vector3.zero, n * 0.18f, n * 0.92f, 0.62f, Palette.TraceNear, 0.10f, true);
            for (int i = 0; i < 9; i++)
            {
                var where = new Vector3((Random.value - 0.5f) * n, (Random.value - 0.5f) * n, 0);
                Burst(where, 2, new BurstOpts
                { kind = Kind.Chip, speed = 0.30f, life = 0.76f, size = 0.11f, gravity = 3.0f, fade = 0.65f, col = Palette.GridHi });
            }
            Burst(playerAt, 10, new BurstOpts { kind = Kind.Spark, speed = 0.78f, life = 0.44f, size = 0.14f, gravity = 0.6f, col = Palette.Arc });
        }

        /// <summary>A refusal is a collision: chips off the stop it just hit, and nothing else.</summary>
        public void FoldRefused(int n, int turn)
        {
            float sgn = (turn == 1 || turn == 2) ? 1f : -1f;
            bool axisX = turn < 2;
            var at = new Vector3(axisX ? -sgn * n * 0.42f : 0f, axisX ? 0f : sgn * n * 0.42f, 0f);
            float ang = axisX ? (sgn > 0 ? 0f : Mathf.PI) : (sgn > 0 ? -Mathf.PI / 2f : Mathf.PI / 2f);

            Burst(at, 12, new BurstOpts
            {
                kind = Kind.Chip, speed = 1.2f, life = 0.62f, size = 0.12f, gravity = 4.2f,
                fade = 0.80f, col = Palette.GridHi, angle = ang, spread = 1.6f
            });
        }

        /// <summary>
        /// THE BIGGEST EVENT IN THE GAME. Two square fronts leave the plate at
        /// different speeds and the room floods with the plate's own colour. The
        /// flash is deliberately short of a white-out: the whole point of the flip
        /// is that the old material is SEEN leaving.
        /// </summary>
        public void PlateFired(int n, Vector3 at)
        {
            Ring2(at, 0.2f, n * 1.05f, 0.76f, Palette.Rust, 0.13f, true);
            Ring2(at, 0.2f, n * 0.62f, 0.52f, Palette.Ink, 0.07f, true);
            Burst(at, 30, new BurstOpts { kind = Kind.Spark, speed = 1.7f, life = 0.78f, size = 0.18f, gravity = 0f, col = Palette.Rust });
            Burst(at, 14, new BurstOpts { kind = Kind.Spark, speed = 0.56f, life = 1.0f, size = 0.24f, gravity = -0.16f, col = Palette.Ink });
            Burst(at, 16, new BurstOpts { kind = Kind.Chip, speed = 1.5f, life = 0.90f, size = 0.14f, gravity = 3.0f, col = Palette.GridHi });
        }

        /// <summary>A refused tap points at itself: the toast says why, the ring says WHERE.</summary>
        public void Deny(Vector3 at, Color col)
        {
            Ring2(at, 0.62f, 0.30f, 0.34f, col, 0.06f, true);
        }

        /// <summary>
        /// THE EXIT, and the only place the budget is spent without restraint —
        /// you are leaving, so nothing after this has to stay legible.
        /// </summary>
        public void Collapse(int n, Vector3 at)
        {
            // NO RINGS. A square front and a circular one, expanding off the cell
            // the core took you in, on top of a board that is about to come apart
            // into its own cells — two more shapes arriving in the same half second
            // as the shape that matters. The exit is the machine breaking; anything
            // drawn over it is competing with it.
            Burst(at, 34, new BurstOpts { kind = Kind.Spark, speed = 1.9f, life = 0.80f, size = 0.18f, gravity = 0f, col = Palette.Core });
            Burst(at, 18, new BurstOpts { kind = Kind.Spark, speed = 0.70f, life = 1.0f, size = 0.24f, gravity = -0.14f, col = Palette.RustHi });
            Burst(at, 22, new BurstOpts { kind = Kind.Ember, speed = 0.14f, lift = 0.70f, life = 1.5f, size = 0.16f, gravity = -0.40f, fade = 0.8f, col = Palette.Core });
        }

        /// <summary>
        /// A PERFECT LINE IS A DIFFERENT EVENT, and it never once looked like one.
        /// Par is the whole scoring system; clearing at par with the same exit as
        /// clearing four over says the game does not care.
        /// </summary>
        /// <summary>
        /// THE ENGINE FAILING, half a second after the core took you.
        ///
        /// The cells themselves are thrown in the vertex shader — see Cell.shader —
        /// so this is not the explosion, it is the DUST OFF it: a square shock the
        /// size of the board, and grit thrown wide enough to still be in frame
        /// while the debris tumbles through it. Centred on the board rather than on
        /// the player, because it is the whole machine that goes.
        /// </summary>
        public void Shatter(int n, Vector3 at)
        {
            Burst(at, 46, new BurstOpts { kind = Kind.Chip, speed = 3.4f, life = 1.1f, size = 0.16f, gravity = 0.9f, fade = 0.85f, col = Palette.RustHi });
            Burst(at, 30, new BurstOpts { kind = Kind.Spark, speed = 2.6f, life = 0.70f, size = 0.13f, gravity = 0f, col = Palette.Core });
            Burst(at, 20, new BurstOpts { kind = Kind.Ember, speed = 0.40f, lift = 0.55f, life = 1.7f, size = 0.18f, gravity = -0.34f, fade = 0.8f, col = Palette.Rust });
        }

        public void PerfectLine(int n, Vector3 at)
        {
            Burst(at, 34, new BurstOpts { kind = Kind.Chip, speed = 2.1f, life = 1.5f, size = 0.14f, gravity = 1.8f, fade = 0.9f, col = Palette.Arc });
            Burst(at, 22, new BurstOpts { kind = Kind.Ember, speed = 0.30f, lift = 1.2f, life = 1.8f, size = 0.16f, gravity = -0.46f, fade = 0.85f, col = Palette.Arc });
        }
    }
}
