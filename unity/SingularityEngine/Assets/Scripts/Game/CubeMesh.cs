using System.Collections.Generic;
using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// The cube, as geometry.
    ///
    /// Only faces with nothing next to them are ever built — the inside of the
    /// cube is not visible from anywhere, and meshing it away is the difference
    /// between thirteen hundred quads and three hundred on the cubes that need it.
    ///
    /// Three submeshes, because three materials: trace, lattice, and plate. The
    /// plate gets its own because it is CUT CLEAN THROUGH the lattice and has to
    /// draw over whatever is in front of it, in every orientation, in every world.
    /// That is not a rendering trick — it is the rule that makes plates legible
    /// enough to plan with, and it is why every fold is legal while you stand on
    /// one.
    /// </summary>
    public static class CubeMesh
    {
        public const int SubTrace = 0, SubLattice = 1, SubPlate = 2;

        static readonly Int3[] FaceDir =
        {
            new Int3( 1, 0, 0), new Int3(-1, 0, 0),
            new Int3( 0, 1, 0), new Int3( 0,-1, 0),
            new Int3( 0, 0, 1), new Int3( 0, 0,-1),
        };

        /// <summary>
        /// Core cell space to Unity object space. The rules use a right-handed
        /// basis and Unity is left-handed, so the cube's own Z is flipped here —
        /// see the note on <see cref="RotationFor"/> for why this flip has to
        /// exist rather than being folded into the rotation.
        /// </summary>
        static Vector3 ToObject(float x, float y, float z) => new Vector3(x, y, -z);

        static Vector3 DirObject(Int3 d) => new Vector3(d.x, d.y, -d.z);

        // The four corners of each face, in object units of one cell. Built from
        // the OBJECT-space direction, so the winding stays consistent in the space
        // the mesh actually lives in.
        static readonly Vector3[][] FaceQuad = BuildQuads();

        static Vector3[][] BuildQuads()
        {
            var quads = new Vector3[6][];
            for (int f = 0; f < 6; f++)
            {
                Vector3 d = DirObject(FaceDir[f]);
                Vector3 a = Mathf.Abs(d.x) == 1 ? Vector3.up : Vector3.right;
                Vector3 b = Vector3.Cross(d, a).normalized;
                a = Vector3.Cross(b, d).normalized;
                var sg = new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
                var q = new Vector3[4];
                for (int i = 0; i < 4; i++) q[i] = d * 0.5f + (a * sg[i].x + b * sg[i].y) * 0.5f;
                quads[f] = q;
            }
            return quads;
        }

        static readonly Vector2[] QuadUv = { new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0) };

        /// <summary>
        /// Which cell each vertex belongs to, in the order Build emitted them.
        ///
        /// Kept so the reveal sweep can rewrite the colour stream on every settle
        /// without re-triangulating: the geometry only changes when the WORLD
        /// changes, but what is reachable changes on every step and every fold.
        /// </summary>
        public static int[] LastCellOfVertex { get; private set; } = System.Array.Empty<int>();

        /// <summary>
        /// Build the visible surface of <paramref name="lv"/> as seen in
        /// <paramref name="world"/>. Coordinates are centred on the cube's middle
        /// and scaled so one cell is one unit, so the object's transform is pure
        /// rotation and the camera never has to know the cube's size.
        /// </summary>
        public static void Build(Mesh mesh, Level lv, int world)
        {
            int n = lv.n;
            char[] eff = lv.Eff(world);

            var verts = new List<Vector3>(4096);
            var norms = new List<Vector3>(4096);
            var uv0 = new List<Vector2>(4096);
            var uv1 = new List<Vector3>(4096);
            var cellOf = new List<int>(4096);
            var cols = new List<Color32>(4096);
            var tri = new List<int>[3] { new List<int>(), new List<int>(), new List<int>() };

            float c = (n - 1) * 0.5f;

            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        char t = eff[Level.Vidx(n, x, y, z)];
                        if (t == '.') continue;

                        int sub = Level.IsGlyph(t) ? SubPlate : t == '+' ? SubTrace : SubLattice;
                        Vector3 centre = ToObject(x - c, y - c, z - c);

                        for (int f = 0; f < 6; f++)
                        {
                            Int3 d = FaceDir[f];
                            int nx = x + d.x, ny = y + d.y, nz = z + d.z;
                            bool outside = nx < 0 || ny < 0 || nz < 0 || nx >= n || ny >= n || nz >= n;

                            // A plate is cut through, so it always shows its own
                            // faces; otherwise a face is built only where the
                            // neighbour is nothing.
                            if (!outside && eff[Level.Vidx(n, nx, ny, nz)] != '.' && sub != SubPlate) continue;

                            int b = verts.Count;
                            Vector3 nrm = DirObject(d);
                            for (int i = 0; i < 4; i++)
                            {
                                verts.Add(centre + FaceQuad[f][i]);
                                norms.Add(nrm);
                                uv0.Add(QuadUv[i]);
                                uv1.Add(centre);
                                cellOf.Add(Level.Vidx(n, x, y, z));
                                cols.Add(new Color32(0, 0, 0, 255));
                            }
                            tri[sub].Add(b); tri[sub].Add(b + 1); tri[sub].Add(b + 2);
                            tri[sub].Add(b); tri[sub].Add(b + 2); tri[sub].Add(b + 3);
                        }
                    }

            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(cols);
            LastCellOfVertex = cellOf.ToArray();
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetTriangles(tri[s], s, false);
            mesh.RecalculateBounds();
        }

        // ---- the schematic ---------------------------------------------------

        /// <summary>Which cell each WIRE vertex belongs to, in emission order.</summary>
        public static int[] LastWireCellOfVertex = System.Array.Empty<int>();

        /// <summary>
        /// What each wire cell IS: 0 empty, 128 lattice, 255 something you can
        /// stand on. Kept because the reach pass rewrites the other three colour
        /// channels every settle and must not lose this one.
        /// </summary>
        public static byte[] LastWireClass = System.Array.Empty<byte>();

        /// <summary>
        /// Half a cell, less the gutter — the SAME inset the surface shader uses
        /// for its plate, so a wire box and the rim of the face in front of it are
        /// the same line rather than two lines a hair apart.
        /// </summary>
        const float WireHalf = 0.5f - 0.055f;

        /// <summary>
        /// THE MACHINE AS A DRAWING: one wire box per cell, for EVERY cell in the
        /// volume, including the empty ones.
        ///
        /// This is the geometry the surface mesh cannot supply and never could.
        /// Build above meshes away every face with something next to it, which is
        /// right — the inside of a solid is not visible from anywhere — and it
        /// means a cell buried in the lattice has no vertices at all, and a VOID
        /// has none by definition. So a schematic drawn from face borders can only
        /// ever outline the walls that already happened to exist. What it cannot
        /// draw is a complete cell, and what it cannot draw at all is the space.
        ///
        /// THE SPACE IS THE POINT. A void is not absence in this game, it is the
        /// route: the corridors are what you fold to line up. Drawing the whole
        /// n-cubed volume as a faint lattice and the material as brighter boxes on
        /// top says where the matter ISN'T, which is the question somebody holding
        /// MATRIX is actually asking and the one thing the board has never been
        /// able to answer — a void and the outside of the cube looked identical.
        ///
        /// Eight vertices and twelve edges a cell, as lines. A full eight-cube is
        /// four thousand vertices, which is nothing, and it carries the same
        /// attributes as the surface so it runs through the same vertex stage —
        /// the arrival wave, the eversion, the exit burst. A wire that did not
        /// travel with the cell it belongs to would come off it the moment
        /// anything moved.
        /// </summary>
        public static void BuildWire(Mesh mesh, Level lv, int world)
        {
            int n = lv.n;
            char[] eff = lv.Eff(world);
            float c = (n - 1) * 0.5f;

            int cells = n * n * n;
            var verts = new List<Vector3>(cells * 8);
            var norms = new List<Vector3>(cells * 8);
            var uv1 = new List<Vector3>(cells * 8);
            var cols = new List<Color32>(cells * 8);
            var cellOf = new List<int>(cells * 8);
            var idx = new List<int>(cells * 24);
            var cls = new List<byte>(cells * 8);

            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        int cell = Level.Vidx(n, x, y, z);
                        char t = eff[cell];
                        byte k = t == '.' ? (byte)0 : Level.IsWalkType(t) ? (byte)255 : (byte)128;

                        Vector3 centre = ToObject(x - c, y - c, z - c);
                        int b = verts.Count;
                        for (int i = 0; i < 8; i++)
                        {
                            verts.Add(centre + new Vector3((i & 1) == 0 ? -WireHalf : WireHalf,
                                                           (i & 2) == 0 ? -WireHalf : WireHalf,
                                                           (i & 4) == 0 ? -WireHalf : WireHalf));
                            // The wire frag never reads it, but the shared vertex
                            // stage normalises the normal and a zero one is a NaN
                            // in an interpolator. One constant is cheaper than
                            // finding out which driver minds.
                            norms.Add(Vector3.forward);
                            uv1.Add(centre);
                            cols.Add(new Color32(0, 0, 0, k));
                            cellOf.Add(cell);
                            cls.Add(k);
                        }

                        // the twelve edges: every pair of corners differing in
                        // exactly one bit
                        for (int i = 0; i < 8; i++)
                            for (int bit = 1; bit <= 4; bit <<= 1)
                                if ((i & bit) == 0) { idx.Add(b + i); idx.Add(b + i + bit); }
                    }

            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(cols);
            mesh.subMeshCount = 1;
            mesh.SetIndices(idx, MeshTopology.Lines, 0, false);
            mesh.RecalculateBounds();

            LastWireCellOfVertex = cellOf.ToArray();
            LastWireClass = cls.ToArray();
        }

        /// <summary>
        /// The rotation that puts the cube in the orientation <paramref name="m"/>
        /// describes.
        ///
        /// This is the one place the two coordinate conventions have to be
        /// reconciled, and it is worth being explicit about why. The rules use a
        /// RIGHT-handed view basis — R right, U up, F toward the camera — which is
        /// the convention the projection maths is written in. Unity is LEFT-handed,
        /// with +Z going away from the viewer. Negating one row of the basis to
        /// convert would give a matrix with determinant -1, which is a reflection
        /// and not a rotation: the cube would come out mirrored and no quaternion
        /// could express it.
        ///
        /// The fix is to flip the cube's own local Z as well, so the conversion is
        /// a similarity rather than a reflection: M = J A J, where A has rows R, U,
        /// F and J = diag(1, 1, -1). det(M) = (-1)(+1)(-1) = +1, a proper rotation.
        /// Concretely that negates the third row and the third column of A, leaving
        /// the corner entry alone.
        ///
        /// The mesh is built in Unity's own handedness already (cell z maps
        /// straight to object z), so the J on the right is absorbed by reading the
        /// cube's local axes back out of the columns of M below.
        /// </summary>
        /// <summary>Where a world cell sits in the cube's object space, in cell units.</summary>
        public static Vector3 CellToObject(int n, Int3 w)
        {
            float c = (n - 1) * 0.5f;
            return ToObject(w.x - c, w.y - c, w.z - c);
        }

        public static Quaternion RotationFor(Ori m)
        {
            // columns of M = images of the object-space basis vectors
            Vector3 ix = new Vector3(m.R.x, m.U.x, -m.F.x);
            Vector3 iy = new Vector3(m.R.y, m.U.y, -m.F.y);
            Vector3 iz = new Vector3(-m.R.z, -m.U.z, m.F.z);
            // A signed permutation is exactly orthonormal, so LookRotation cannot
            // be handed anything degenerate here.
            return Quaternion.LookRotation(iz, iy);
        }
    }
}
