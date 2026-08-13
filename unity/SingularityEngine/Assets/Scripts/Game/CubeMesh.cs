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
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetTriangles(tri[s], s, false);
            mesh.RecalculateBounds();
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
