using System.Collections.Generic;
using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// The cube on screen: one mesh, three materials, and a rotation.
    ///
    /// Everything the web build had to draw by hand — which cell wins a column,
    /// how bright it is for its depth, what a fold looks like halfway through —
    /// falls out of putting the real solid in front of an orthographic camera
    /// that is looking straight down an axis. The depth buffer performs the
    /// collapse, the shader performs the ramp, and the transform performs the fold.
    /// </summary>
    public class CubeView : MonoBehaviour
    {
        public Transform cube;
        public MeshFilter filter;
        public MeshRenderer renderer3d;

        Mesh _mesh;
        Material _matTrace, _matLattice, _matPlate;

        Session _s;
        int _builtWorld = -1;
        int _builtCells = -1;

        int[] _cellOfVertex = System.Array.Empty<int>();
        Color32[] _vertexCols = System.Array.Empty<Color32>();

        // THE REVEAL FRONT, in BFS steps. It runs out from the player after every
        // settled change, so the lit reachable set is DRAWN rather than switched on
        // — and the sweep is the connectivity graph being traced one cell at a time.
        float _reveal = 99f;
        const float RevealPerSecond = 26f;

        // The transition front, in cells, and which way it runs.
        float _wave = 99f, _waveDir = 1f;
        Int3 _waveFocus;
        const float WavePerSecond = 22f;

        // the cage, and the two reads that change nothing
        Transform _cage;
        MeshRenderer _cageR;
        readonly List<Transform> _nearPips = new List<Transform>();
        Transform _through;
        SpriteRenderer _throughSr;
        readonly List<(int u, int v, Session.Special what)> _near = new List<(int, int, Session.Special)>();

        // markers
        Transform _markerRoot;
        readonly List<Marker> _markers = new List<Marker>();

        class Marker
        {
            public Transform t;
            public SpriteRenderer sr;
            public Int3 cell;
            public string role;   // "core" | "node" | "lock" | "player"
            public int index;
        }

        // ---- live view state (driven by the session and the input router) ----
        public float peekYaw, peekPitch, peekAmt;

        /// <summary>
        /// THE ATTRACT CUBE. The title screen is a composition with a cube in the
        /// middle of it, and a still one reads as a screenshot. It turns slowly on
        /// two axes at rates that do not divide into each other, so it never
        /// repeats a pose while anybody is looking at it — and it is the same
        /// renderer and the same rules, just with nobody playing.
        /// </summary>
        public bool attract;
        float _attractT;

        public const float PeekYaw = 0.62f, PeekPitch = 0.42f, PeekMaxPitch = 1.15f;

        public void Init(Session s)
        {
            _s = s;

            cube = new GameObject("Cube").transform;
            cube.SetParent(transform, false);

            filter = cube.gameObject.AddComponent<MeshFilter>();
            renderer3d = cube.gameObject.AddComponent<MeshRenderer>();
            renderer3d.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer3d.receiveShadows = false;
            renderer3d.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            _mesh = new Mesh { name = "cube" };
            filter.sharedMesh = _mesh;

            Shader sh = Shader.Find("Singularity/Cell");
            if (sh == null) { Debug.LogError("Singularity/Cell shader missing"); sh = Shader.Find("Unlit/Color"); }

            _matTrace = new Material(sh) { name = "trace" };
            _matLattice = new Material(sh) { name = "lattice" };
            _matPlate = new Material(sh) { name = "plate" };

            PushPalette();
            _matPlate.SetFloat("_EdgeLift", 1.4f);
            // It is cut clean through the lattice and legible from every face, so it
            // draws over whatever is in front of it. That is the rule, not a hack:
            // it is what makes every fold legal while you stand on one, and what
            // makes planning with them possible at all.
            _matPlate.renderQueue = 2100;
            _matPlate.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            renderer3d.sharedMaterials = new[] { _matTrace, _matLattice, _matPlate };

            _markerRoot = new GameObject("Markers").transform;
            _markerRoot.SetParent(transform, false);

            BuildCage();
            BuildReads();
        }

        // ---- the cage -------------------------------------------------------
        //
        // Under a held MATRIX the lattice goes to glass, and glass with no edges is
        // just a dimmer board. The cage is the twelve edges of the solid you are
        // holding — the one piece of the picture that says the thing has a SHAPE
        // rather than being a grid that happens to be lit from behind. It rotates
        // with the cube because it belongs to the cube, and it fades out entirely
        // at rest because at rest the cube is square to the camera and its own
        // silhouette says the same thing for free.

        void BuildCage()
        {
            var go = new GameObject("Cage");
            _cage = go.transform;
            _cage.SetParent(cube, false);

            var mf = go.AddComponent<MeshFilter>();
            _cageR = go.AddComponent<MeshRenderer>();
            _cageR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cageR.receiveShadows = false;
            _cageR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            Shader sh = Shader.Find("Singularity/Fx");
            _cageR.sharedMaterial = new Material(sh) { name = "cage" };
            mf.sharedMesh = new Mesh { name = "cage" };
            _cageR.enabled = false;
        }

        static readonly int[,] CageEdge =
        {
            {0,1},{1,3},{3,2},{2,0}, {4,5},{5,7},{7,6},{6,4}, {0,4},{1,5},{2,6},{3,7}
        };

        void RebuildCage(float half)
        {
            var corner = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corner[i] = new Vector3((i & 1) == 0 ? -half : half,
                                        (i & 2) == 0 ? -half : half,
                                        (i & 4) == 0 ? -half : half);

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var kind = new List<Vector2>();
            var col = new List<Color>();
            var tri = new List<int>();
            const float w = 0.035f;

            for (int e = 0; e < 12; e++)
            {
                Vector3 a = corner[CageEdge[e, 0]], b = corner[CageEdge[e, 1]];
                Vector3 dir = (b - a).normalized;
                // two crossed slabs per edge, so it never vanishes edge-on
                Vector3 p1 = Vector3.Cross(dir, Vector3.up);
                if (p1.sqrMagnitude < 0.01f) p1 = Vector3.Cross(dir, Vector3.right);
                p1 = p1.normalized;
                Vector3 p2 = Vector3.Cross(dir, p1).normalized;
                Slab(v, uv, kind, col, tri, a, b, p1 * w);
                Slab(v, uv, kind, col, tri, a, b, p2 * w);
            }

            Mesh m = _cage.GetComponent<MeshFilter>().sharedMesh;
            m.Clear();
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetUVs(1, kind);
            m.SetColors(col);
            m.SetTriangles(tri, 0, false);
            m.RecalculateBounds();
        }

        static void Slab(List<Vector3> v, List<Vector2> uv, List<Vector2> kind, List<Color> col, List<int> tri,
                         Vector3 a, Vector3 b, Vector3 across)
        {
            int i = v.Count;
            v.Add(a - across); v.Add(b - across); v.Add(b + across); v.Add(a + across);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
            uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            var k = new Vector2(1, 0);
            for (int j = 0; j < 4; j++) { kind.Add(k); col.Add(Color.white); }
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        // ---- the two reads that change nothing ------------------------------

        void BuildReads()
        {
            // Four pips, one per neighbour, telling the eye where the next useful
            // step is before the hand has to work it out.
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("pip" + i);
                go.transform.SetParent(_markerRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Glyphs.For("pip");
                sr.sharedMaterial = Glyphs.Material;
                sr.sortingOrder = 95;
                go.transform.localScale = Vector3.one * 0.22f;
                go.SetActive(false);
                _nearPips.Add(go.transform);
            }

            // And the antipode, drawn faintly THROUGH the player: a look at the far
            // side of the world down the column you are standing in.
            var t = new GameObject("through");
            t.transform.SetParent(_markerRoot, false);
            _throughSr = t.AddComponent<SpriteRenderer>();
            _throughSr.sharedMaterial = Glyphs.Material;
            // ABOVE THE HORIZON, NOT BEHIND IT. This is the far side of the world
            // seen THROUGH the hole, so it has to draw after the hole has removed
            // the deck — at 94 it was being covered by the very thing it is meant
            // to be visible through. See PlayerOrb for the rest of the stack.
            _throughSr.sortingOrder = 98;
            t.transform.localScale = Vector3.one * 0.34f;
            _through = t.transform;
            t.SetActive(false);
        }

        void PlaceReads(Camera cam)
        {
            Quaternion face = cam.transform.rotation;
            float c = (_s.N - 1) * 0.5f;
            bool quiet = _s.walking == null && _s.anim == null && !_s.won;

            _s.NearSpecials(_near);
            for (int i = 0; i < _nearPips.Count; i++)
            {
                bool on = quiet && i < _near.Count;
                if (_nearPips[i].gameObject.activeSelf != on) _nearPips[i].gameObject.SetActive(on);
                if (!on) continue;
                var (u, v, what) = _near[i];
                _nearPips[i].position = new Vector3(u - c, v - c, -(_s.N * 0.5f + 0.4f));
                _nearPips[i].rotation = face;
                var sr = _nearPips[i].GetComponent<SpriteRenderer>();
                sr.color = Colour(what) * 0.75f;
            }

            Session.Special through = quiet ? _s.ThroughLook() : Session.Special.None;
            bool showThrough = through != Session.Special.None;
            if (_through.gameObject.activeSelf != showThrough) _through.gameObject.SetActive(showThrough);
            if (showThrough)
            {
                _throughSr.sprite = Glyphs.For(through == Session.Special.Core ? "core"
                                             : through == Session.Special.Node ? "node" : "lock");
                // faint, and deliberately so: it is a look through a hole, and the
                // only reward is noticing
                _throughSr.color = new Color(Colour(through).r, Colour(through).g, Colour(through).b, 0.30f);
                _through.position = cube.TransformPoint(CubeMesh.CellToObject(_s.N, _s.pos))
                                  - cam.transform.forward * 0.40f;
                // rocking very slightly, because the point is that it is GLIMPSED
                _through.rotation = face * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.42f) * 4f);
            }
        }

        static Color Colour(Session.Special s)
            => s == Session.Special.Core ? Palette.Core
             : s == Session.Special.Node ? Palette.Node
             : Palette.Lock;

        /// <summary>
        /// The two depth ramps, onto the materials that draw them.
        ///
        /// Separated out because legibility moves the near lattice, and a
        /// setting that changes the board has to be able to say so to the board
        /// after it has already been built. These are the only colours the cube
        /// has: everything else about a cell is arithmetic in the shader.
        /// </summary>
        /// <summary>Which vault's weathering the lattice is wearing. See Palette.</summary>
        int _band;

        public void PushPalette(int band = -1)
        {
            if (band >= 0) _band = band;
            if (_matTrace == null) return;
            _matTrace.SetColor("_ColFar", Palette.TraceFar);
            _matTrace.SetColor("_ColNear", Palette.TraceNear);
            _matLattice.SetColor("_ColFar", Palette.LatticeFarOf(_band));
            _matLattice.SetColor("_ColNear", Palette.LatticeNearOf(_band));

            // A plate is the one thing that is the same in every world, so it does
            // not take the depth ramp: it is rust at full strength wherever it is.
            _matPlate.SetColor("_ColFar", Palette.Rust * 0.62f);
            _matPlate.SetColor("_ColNear", Palette.RustHi);
        }

        public void Rebuild(bool force = false)
        {
            if (_s?.lv == null) return;
            if (!force && _builtWorld == _s.world && _builtCells == _s.lv.vox.Length) return;
            _builtWorld = _s.world;
            _builtCells = _s.lv.vox.Length;

            CubeMesh.Build(_mesh, _s.lv, _s.world);
            _cellOfVertex = CubeMesh.LastCellOfVertex;
            _vertexCols = new Color32[_cellOfVertex.Length];

            RebuildCage(_s.N * 0.5f);

            float halfSpan = (_s.N - 1) * 0.5f;
            _matTrace.SetFloat("_HalfSpan", halfSpan);
            _matLattice.SetFloat("_HalfSpan", halfSpan);
            _matPlate.SetFloat("_HalfSpan", halfSpan);

            BuildMarkers();
            UpdateReach();
        }

        /// <summary>
        /// Rewrite the colour stream with what is reachable from where the player
        /// stands. No re-triangulation: the geometry only changes when the WORLD
        /// does, and this changes on every step.
        ///
        /// Reachability is a property of a view SQUARE, not of a cell — so a cell
        /// is lit when it is the surface of a reachable column, which is exactly
        /// the set the player may walk to.
        /// </summary>
        public void UpdateReach()
        {
            if (_s?.lv == null || _cellOfVertex.Length == 0 || _s.reach == null) return;

            int n = _s.N;
            var lit = new byte[n * n * n];
            var dist = new byte[n * n * n];
            var wave = new byte[n * n * n];

            // The transition front is measured in VIEW cells from its focus, not in
            // world cells: the wave is something the screen does, and two cells that
            // land on the same square have to arrive together or the front tears.
            Int3 focus = Projection.ViewOf(n, _s.M, _waveFocus);
            for (int i = 0; i < n * n * n; i++)
            {
                int y = i / (n * n), rr = i - y * n * n, z = rr / n, x = rr - z * n;
                Int3 v3 = Projection.ViewOf(n, _s.M, new Int3(x, y, z));
                float d = Mathf.Sqrt((v3.x - focus.x) * (v3.x - focus.x) + (v3.y - focus.y) * (v3.y - focus.y));
                wave[i] = (byte)Mathf.Min(255, Mathf.RoundToInt(d));
            }

            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                {
                    int k = u * n + v;
                    if (!_s.reach[k]) continue;
                    Surf sc = _s.surf[k];
                    if (!sc.has) continue;
                    int idx = Level.Vidx(n, sc.w);
                    lit[idx] = 255;
                    dist[idx] = _s.reachDist[k];
                }

            for (int i = 0; i < _cellOfVertex.Length; i++)
            {
                int cell = _cellOfVertex[i];
                _vertexCols[i] = new Color32(lit[cell], dist[cell], wave[cell], 255);
            }

            _mesh.SetColors(_vertexCols);
        }

        /// <summary>Send the front back to the player and let it run out again.</summary>
        public void RestartReveal(float headStart = 0f) => _reveal = -headStart;

        /// <summary>
        /// Run the board in from a cell, or out of it. In on a cube arriving, out
        /// of the core on a cube ending — the win card does not appear over a still
        /// image of a solved puzzle, it appears after the puzzle has left.
        /// </summary>
        public void StartWave(Int3 focus, bool inward)
        {
            _waveFocus = focus;
            _waveDir = inward ? 1f : -1f;
            _wave = inward ? -2f : 0f;
            UpdateReach();
        }

        public void SkipWave() { _wave = 999f; _waveDir = 1f; }

        // ---- markers --------------------------------------------------------
        //
        // An object — node, lock, core, and the player's own footing — is attached
        // to a cell, and exists in a projection only while that cell IS the surface
        // of its column. Bury it behind lattice and it is gone until you fold. So
        // the markers are not children of the cube: they are placed each frame at
        // the cube's current world position for that cell, pushed a little toward
        // the camera, and hidden outright when the projection says the cell they
        // belong to is not what you are looking at.

        void BuildMarkers()
        {
            foreach (var m in _markers) if (m.t) Destroy(m.t.gameObject);
            _markers.Clear();

            Add(_s.lv.goal, "core", 0, Palette.Core, 0.86f);
            for (int i = 0; i < _s.lv.keys.Count; i++) Add(_s.lv.keys[i], "node", i, Palette.Node, 0.62f);
            for (int i = 0; i < _s.lv.doors.Count; i++) Add(_s.lv.doors[i], "lock", i, Palette.Lock, 0.74f);

        }

        Marker Add(Int3 cell, string role, int index, Color col, float size)
        {
            Marker m = Make(role + index, col, size);
            m.cell = cell; m.role = role; m.index = index;
            _markers.Add(m);
            return m;
        }

        Marker Make(string name, Color col, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_markerRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Glyphs.For(name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'));
            sr.sharedMaterial = Glyphs.Material;
            sr.color = col;
            sr.sortingOrder = 100;
            go.transform.localScale = Vector3.one * size;
            return new Marker { t = go.transform, sr = sr };
        }

        // ---- per frame ------------------------------------------------------

        public void Apply(Camera cam)
        {
            if (_s?.lv == null) return;

            // base orientation, then the live fold on top of it in world space
            Quaternion baseRot = CubeMesh.RotationFor(_s.M);
            float ang = _s.anim?.ang ?? (_s.drag != null && _s.drag.hasAxis ? _s.drag.ang : 0f);
            bool axisX = _s.anim?.axisX ?? (_s.drag?.axisX ?? true);

            Quaternion live = Quaternion.identity;
            if (Mathf.Abs(ang) > 1e-5f)
            {
                // A horizontal drag yaws the cube; dragging RIGHT pushes the near
                // face right, which in Unity's left-handed frame is a NEGATIVE
                // rotation about up. A vertical drag pitches it, and dragging up
                // lifts the near face, which is a positive rotation about right.
                live = axisX
                    ? Quaternion.AngleAxis(-ang * Mathf.Rad2Deg, Vector3.up)
                    : Quaternion.AngleAxis(ang * Mathf.Rad2Deg, Vector3.right);
            }

            // MATRIX — the third verb. The lattice goes to glass and the whole
            // solid can be orbited, which costs the other two verbs nothing
            // because it is reached by holding still rather than by moving.
            float e = peekAmt * peekAmt * (3f - 2f * peekAmt);
            if (e > 1e-4f)
            {
                live = Quaternion.AngleAxis(-peekYaw * e * Mathf.Rad2Deg, Vector3.up)
                     * Quaternion.AngleAxis(peekPitch * e * Mathf.Rad2Deg, Vector3.right)
                     * live;
            }

            if (attract)
            {
                _attractT += Time.unscaledDeltaTime;
                live = Quaternion.AngleAxis(_attractT * 17f, Vector3.up)
                     * Quaternion.AngleAxis(Mathf.Sin(_attractT * 0.41f) * 22f, Vector3.right)
                     * live;
            }

            cube.rotation = live * baseRot;

            float dim = Mathf.Lerp(1f, 0.55f, e);
            _matLattice.SetFloat("_Dim", dim);
            _matTrace.SetFloat("_Dim", Mathf.Lerp(1f, 0.85f, e));

            _wave += Time.unscaledDeltaTime * WavePerSecond;
            foreach (Material m in new[] { _matTrace, _matLattice, _matPlate })
            {
                m.SetFloat("_Peek", peekAmt * peekAmt * (3f - 2f * peekAmt));
                m.SetFloat("_Wave", _wave);
                m.SetFloat("_WaveDir", _waveDir);
            }

            _reveal += Time.unscaledDeltaTime * RevealPerSecond;
            _matTrace.SetFloat("_Reveal", _reveal);
            _matLattice.SetFloat("_Reveal", _reveal);
            _matPlate.SetFloat("_Reveal", _reveal);

            // the cage only earns its place while the solid is being looked at
            bool cageOn = e > 0.01f;
            if (_cageR.enabled != cageOn) _cageR.enabled = cageOn;
            if (cageOn)
            {
                // 0.46 at rest and 0.40 more under a full lean, which is the
                // original's drawCage alpha. The cage is the twelve edges of the
                // thing you are HOLDING, so it has to come up as the material goes
                // away — glass with no edges is just a dimmer board.
                float peekE = peekAmt * peekAmt * (3f - 2f * peekAmt);
                _cageR.sharedMaterial.SetColor("_Tint",
                    new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, e * (0.46f + peekE * 0.40f)));
            }

            PlaceMarkers(cam);
            PlaceReads(cam);
        }

        void PlaceMarkers(Camera cam)
        {
            Vector3 toward = -cam.transform.forward * 0.52f;
            Quaternion face = cam.transform.rotation;

            foreach (var m in _markers)
            {
                bool shown = Shown(m);
                if (m.t.gameObject.activeSelf != shown) m.t.gameObject.SetActive(shown);
                if (!shown) continue;
                m.t.position = cube.TransformPoint(CubeMesh.CellToObject(_s.N, m.cell)) + toward;
                m.t.rotation = face;
            }

        }

        bool Shown(Marker m)
        {
            if (_s.surf == null) return false;
            if (m.role == "node" && (_s.kmask & (1 << m.index)) != 0) return false;   // collected
            if (m.role == "lock" && (_s.doors & (1 << m.index)) != 0) return false;   // opened
            // in this world at all?
            if (!Level.IsWalkType(Level.EffType(_s.lv.vox[_s.lv.Vidx(m.cell)], _s.world))) return false;
            return Projection.SurfaceAt(_s.N, _s.surf, _s.M, m.cell);
        }
    }
}
