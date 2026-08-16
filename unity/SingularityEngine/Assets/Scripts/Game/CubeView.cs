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
        bool _wasTurning;
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

        // ---- eversion ---------------------------------------------------------
        //
        // THE RENDERER AND THE RULES HAVE TO STAY THE SAME STATEMENT. That is the
        // claim at the top of Cell.shader and it is the reason the board is drawn
        // as a solid at all: an orthographic camera down one axis makes the DEPTH
        // BUFFER perform the projection, so the nearest cell in a column is the
        // one that gets drawn, which is precisely the rule the solver plays by.
        //
        // Everting reverses that rule — each column shows its FAR side — so the
        // renderer has to reverse the same way, and there is exactly one honest
        // way to do it without touching the depth test: TURN THE SOLID THROUGH
        // ITSELF. A scale of -1 along the camera axis puts the far cells nearest,
        // which is not an effect that resembles eversion, it IS eversion, and the
        // depth buffer keeps doing the one job it was given.
        //
        // It goes on THIS transform rather than on `cube`, because `cube` carries
        // the orientation and would flip along whichever axis the fold happens to
        // have pointed at the camera. This one is never rotated, so its Z is the
        // view axis at every orientation. Cull is already Off — the solid is
        // closed and the peek path draws back faces — so the inverted winding a
        // negative scale produces costs nothing.
        public float evertAmt;          // 0 upright, 1 everted; the eased position
        float _evertTarget;

        /// <summary>How long the solid takes to turn through itself, in seconds.</summary>
        public const float EvertSeconds = 0.55f;

        /// <summary>
        /// How far out of step the cells are, as a fraction of the whole turn.
        ///
        /// Zero and every cell flips together, which is a slab turning over.
        /// One and the last cell only starts as the first one lands, which is a
        /// slow ripple and loses the sense of a single event. Three quarters is a
        /// wave with a clear front that still reads as one motion — and it is
        /// keyed on DEPTH, so the turn sweeps from the face you are looking at to
        /// the face you are about to be looking at.
        /// </summary>
        public const float EvertStagger = 0.75f;

        // ---- the exit ---------------------------------------------------------
        //
        // THE MACHINE STOPS BEING ONE OBJECT.
        //
        // Winning used to shrink every cell to a point, running out from the core.
        // It was correct and it was quiet: the board did not come apart, it was
        // switched off one cell at a time, and the biggest moment in the game read
        // as a screen being cleared to make room for a card.
        //
        // So it is thrown instead. First the solid TURNS — a lean into three
        // quarters with the camera easing back — because the explosion only means
        // anything if the eye has just been told the thing is three-dimensional and
        // made of parts. Then every part goes.
        //
        // The turn is rotation only. It deliberately does not raise _Peek: MATRIX
        // takes the board to glass so you can see through it, and debris you can
        // see through is not debris.

        /// <summary>0 square to the camera, 1 fully leant into the exit's three quarters.</summary>
        public float winLean;

        /// <summary>0 whole, 1 fully thrown. Drives the per-cell burst in the shader.</summary>
        public float burstAmt;

        public const float LeanYaw = 0.50f, LeanPitch = 0.33f;   // radians, ~29 and ~19 degrees
        public const float LeanSeconds = 0.34f, BurstSeconds = 0.85f;

        /// <summary>
        /// How far out of step the cells are, keyed on DISTANCE FROM THE CORE, so
        /// the break travels outward as a front. Wider than the eversion's, because
        /// this one is allowed to lose its shape — it is ending.
        /// </summary>
        public const float BurstStagger = 0.55f;

        /// <summary>Put the solid back together. Every entry into a cube goes through here.</summary>
        public void Whole() { winLean = 0f; burstAmt = 0f; }

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

        /// <summary>
        /// The hold, eased — how far into the schematic the board is. Everything
        /// that comes on with MATRIX reads it from here rather than smoothing the
        /// raw amount again in its own way and drifting out of step with the rest.
        /// </summary>
        public float PeekEase => peekAmt * peekAmt * (3f - 2f * peekAmt);

        /// <summary>
        /// HOW FAR OUT OF THE WAY THE MATERIAL IS — which is what `_Peek` has
        /// always meant, and it is not only the hold.
        ///
        /// An eversion pushes it too: halfway through the flip the solid is
        /// edge-on and the cube goes to glass on the way in and hardens on the way
        /// out, deliberately, because that is a look MATRIX has already taught the
        /// player. So the x-ray flares for half a second in the middle of a
        /// polarity change, which is the right picture — the machine turning
        /// through itself, seen as wire.
        ///
        /// Written every frame by Apply and read by anything outside this class
        /// that has to agree with the shader. Bloom.Lift used the hold alone and
        /// so was the one part of the schematic that did not come on with the turn.
        /// </summary>
        public float GlassAmount { get; private set; }

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

            // AND THE SCHEMATIC, which is what the same three materials are drawn
            // in while somebody is holding MATRIX. Constants, so they are pushed
            // with the palette rather than every frame. The plate keeps its rust
            // for the reason it keeps everything else: it is the one thing that is
            // the same in every world, and a schematic that recoloured it would be
            // saying it was not.
            _matTrace.SetColor("_WireCol", Palette.WireTrace);
            _matLattice.SetColor("_WireCol", Palette.WireLattice);
            _matPlate.SetColor("_WireCol", Palette.RustHi);

            // The lattice is most of the solid, so its x-ray is the one that can
            // turn into a tangle — it draws at half the trace's strength and fades
            // harder into the far side.
            _matTrace.SetFloat("_WireGain", 0.40f);
            _matLattice.SetFloat("_WireGain", 0.20f);
            _matPlate.SetFloat("_WireGain", 0.34f);
            _matLattice.SetFloat("_WireFar", 0.20f);
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
        /// Run the board in from a cell, or out of it. Only the inward direction
        /// has a caller now: the exit used to be an outward wave — every cell
        /// shrinking to a point, running from the core — and it is a burst instead.
        /// The direction stays because it costs one sign and the wave is the only
        /// thing in here that can end a board quietly, which some future screen
        /// will want.
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
            // THE TURN, AND THE GLASS THAT COVERS IT.
            //
            // Halfway through the flip the solid is edge-on and a scale of zero
            // is a degenerate object — a line. That instant is not hidden, it is
            // USED: the cube goes to glass on the way in and hardens on the way
            // out, which is the same look MATRIX already taught the player, so
            // "there is something behind this" is a sentence they have heard.
            // Eversion is that sentence finished: the behind is now the front.
            _evertTarget = _s != null && _s.Everted ? 1f : 0f;
            evertAmt = Mathf.MoveTowards(evertAmt, _evertTarget,
                                         Time.unscaledDeltaTime / Mathf.Max(0.01f, EvertSeconds));
            // NO GLOBAL FLIP. The mirror is done per cell in the vertex shader,
            // which is both the better picture and the simpler seam: the mesh
            // never changes for an eversion — a polarity changes no cell's TYPE,
            // only which one wins its column — so the whole effect is three
            // uniforms and nothing on the CPU moves at all.
            float ev = evertAmt;

            // one at the middle of the turn, zero at either end — how much of the
            // transition we are IN, as opposed to which side of it we are on
            float turning = 1f - Mathf.Abs(ev * 2f - 1f);

            float e = Mathf.Max(PeekEase, turning * 0.85f);
            GlassAmount = e;
            if (e > 1e-4f)
            {
                live = Quaternion.AngleAxis(-peekYaw * e * Mathf.Rad2Deg, Vector3.up)
                     * Quaternion.AngleAxis(peekPitch * e * Mathf.Rad2Deg, Vector3.right)
                     * live;
            }

            // THE LEAN OUT. Gated on motion because it is a camera move on top of
            // the picture rather than the framing of it — a player who asked the
            // board to hold still gets the burst without the turn, and the burst
            // is still the board coming apart.
            if (winLean > 1e-4f)
            {
                float wl = winLean * Access.MotionAmount;
                live = Quaternion.AngleAxis(-LeanYaw * wl * Mathf.Rad2Deg, Vector3.up)
                     * Quaternion.AngleAxis(LeanPitch * wl * Mathf.Rad2Deg, Vector3.right)
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
                m.SetFloat("_Peek", e);
                m.SetFloat("_Wave", _wave);
                m.SetFloat("_WaveDir", _waveDir);

                // THE TWO AXES ARE TAKEN FROM THE CUBE'S OWN ROTATION, every
                // frame, because the solid is folded and the shader works in
                // object space. Forward is what gets mirrored; right is what the
                // cells spin about, so they all turn the same way on screen
                // whichever face happens to be pointing at the camera.
                m.SetFloat("_Evert", ev);
                m.SetVector("_EvertAxis", cube.InverseTransformDirection(Vector3.forward));
                m.SetVector("_EvertSpin", cube.InverseTransformDirection(Vector3.right));
                m.SetFloat("_EvertSpread", EvertStagger);

                m.SetFloat("_Burst", burstAmt);
                m.SetFloat("_BurstSpread", BurstStagger);
            }

            _reveal += Time.unscaledDeltaTime * RevealPerSecond;
            // THE COLOUR OF THE TURN. The everter's violet is pushed into the
            // near-colour of every material while the solid is passing through
            // itself, so the flip is not a silent geometric event — the whole
            // board says which control did this, in the control's own hue, and
            // the light channel scales it because it is light.
            if (turning > 1e-4f)
            {
                float k = turning * Access.LightAmount;
                _matTrace.SetColor("_ColNear", Color.Lerp(Palette.TraceNear, Palette.Evert, k * 0.8f));
                _matLattice.SetColor("_ColNear", Color.Lerp(Palette.LatticeNear, Palette.Evert, k * 0.55f));
            }
            else if (_wasTurning)
            {
                _matTrace.SetColor("_ColNear", Palette.TraceNear);
                _matLattice.SetColor("_ColNear", Palette.LatticeNear);
            }
            _wasTurning = turning > 1e-4f;

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
                //
                // AND IT IS THE BRIGHTEST LINE IN THE SCHEMATIC. Under a full hold
                // the whole solid has become wire, and a rust cage at 0.86 sitting
                // inside a lattice of lit wire is the outline of the object reading
                // as less present than its contents. It goes to the trace's wire
                // colour at full strength, which is the one thing on screen that
                // should be unambiguous: this is where the machine ends.
                float peekE = PeekEase;
                Color cage = Color.Lerp(Palette.Rust, Palette.WireTrace, peekE * 0.85f);
                _cageR.sharedMaterial.SetColor("_Tint",
                    new Color(cage.r, cage.g, cage.b, e * (0.46f + peekE * 0.54f)));
            }

            // A MARKER BELONGS TO A CELL, AND THE CELLS HAVE LEFT. Every glyph on
            // the board is placed each frame at the cube's world position for its
            // cell, and the burst happens in the vertex shader — so the CPU still
            // thinks every cell is exactly where it was authored. Leaving them on
            // would hang the core, the nodes and the player in mid-air over an
            // empty screen. They go with the board.
            bool intact = burstAmt <= 1e-4f;
            if (_markerRoot.gameObject.activeSelf != intact) _markerRoot.gameObject.SetActive(intact);
            if (!intact) return;

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
