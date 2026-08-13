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

        // markers
        Transform _markerRoot;
        readonly List<Marker> _markers = new List<Marker>();
        Marker _player;

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

            _matTrace.SetColor("_ColFar", Palette.TraceFar);
            _matTrace.SetColor("_ColNear", Palette.TraceNear);
            _matLattice.SetColor("_ColFar", Palette.LatticeFar);
            _matLattice.SetColor("_ColNear", Palette.LatticeNear);

            // A plate is the one thing that is the same in every world, so it does
            // not take the depth ramp: it is rust at full strength wherever it is.
            _matPlate.SetColor("_ColFar", Palette.Rust * 0.62f);
            _matPlate.SetColor("_ColNear", Palette.RustHi);
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
        }

        public void Rebuild(bool force = false)
        {
            if (_s?.lv == null) return;
            if (!force && _builtWorld == _s.world && _builtCells == _s.lv.vox.Length) return;
            _builtWorld = _s.world;
            _builtCells = _s.lv.vox.Length;

            CubeMesh.Build(_mesh, _s.lv, _s.world);

            float halfSpan = (_s.N - 1) * 0.5f;
            _matTrace.SetFloat("_HalfSpan", halfSpan);
            _matLattice.SetFloat("_HalfSpan", halfSpan);
            _matPlate.SetFloat("_HalfSpan", halfSpan);

            BuildMarkers();
        }

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

            if (_player == null)
            {
                _player = Make("player", Palette.Arc, 0.68f);
                _player.role = "player";
            }
            _player.t.SetAsLastSibling();
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

            cube.rotation = live * baseRot;

            float dim = Mathf.Lerp(1f, 0.55f, e);
            _matLattice.SetFloat("_Dim", dim);
            _matTrace.SetFloat("_Dim", Mathf.Lerp(1f, 0.85f, e));

            PlaceMarkers(cam);
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

            if (_player != null)
            {
                bool ok = _s.lv != null;
                if (_player.t.gameObject.activeSelf != ok) _player.t.gameObject.SetActive(ok);
                if (ok)
                {
                    _player.t.position = cube.TransformPoint(CubeMesh.CellToObject(_s.N, _s.pos)) + toward * 1.15f;
                    _player.t.rotation = face;
                }
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
