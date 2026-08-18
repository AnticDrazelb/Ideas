using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// THE BLACK BEHIND THE MACHINE IS DEEP SKY NOW.
    ///
    /// CameraRig cleared to Palette.Void and carried the argument for it: a void
    /// column is unrendered space — not a dark room, not deep sky, nothing — and
    /// anything painted there is the display claiming to have data it does not
    /// have. That argument was about AREA FILLS competing with the depth ramp,
    /// and it is kept here in the only form that matters: see Sky.shader, and
    /// SkyChecks, which holds the one area fill in this file below the dimmest
    /// cell the game draws. The stars are points, and a point is not a claim.
    ///
    /// WHY IT IS WORTH DOING AT ALL. The case is a photograph of a real object
    /// with a glass front, and behind the glass was a flat black rectangle. A
    /// black rectangle behind glass reads as a screen that is off. The same
    /// rectangle with a field of stars in it that MOVES WHEN THE PHONE MOVES
    /// reads as a window — and that is the whole fiction of this game, which is
    /// about a thing trapped inside a machine somewhere out there.
    ///
    /// IT IS FIXED IN THE WORLD, NOT IN THE HAND. Tilt gives the lean; the sky
    /// offsets against it, so tipping the phone right slides the field left and
    /// the parallax reads as looking round the edge of the window rather than as
    /// a texture sliding about on the glass. Three shells at different rates do
    /// the depth.
    ///
    /// AND IT IS GOVERNED BY CALIBRATE. At STILL the parallax is exactly zero and
    /// the field stands still — a player who asked the board to stop moving did
    /// not ask for a background that does. Under LEGIBLE there is no sky at all:
    /// that setting is a promise the interface will stop performing, and a
    /// starfield is the interface performing.
    /// </summary>
    public class Sky : MonoBehaviour
    {
        /// <summary>
        /// HOW FAR THE FIELD TRAVELS AT FULL LEAN, in the shader's own uv, where
        /// the whole screen height is 2.0. So the movement on glass is HALF this
        /// as a fraction of the screen — which is the conversion nobody did.
        ///
        /// THREE PER CENT WAS INVISIBLE AND THE CHECK SAID IT WAS FINE. It
        /// asserted the number sat between 0.005 and 0.05, a band with no meaning
        /// attached to it, and never converted it to anything a person could see.
        /// Three per cent of uv is 1.5% of the screen: twenty-nine pixels on a
        /// 1920-tall phone AT FULL LEAN, and full lean is a fast twenty-two degree
        /// tilt. Ordinary handling puts Tilt.Look around a third, which is NINE
        /// PIXELS of movement on small dim points against black. The sensor was
        /// working. The effect was not.
        ///
        /// Ten per cent is five per cent of the screen — about ninety pixels at
        /// full lean and twenty to forty in the hand. Still a background, and now
        /// a background that moves.
        /// </summary>
        public const float Travel = 0.100f;

        /// <summary>
        /// The one area fill in the shader, and the number SkyChecks holds
        /// against the dimmest cell. See Sky.shader.
        /// </summary>
        public const float NebulaPeak = 0.055f;

        /// <summary>
        /// THE BRIGHTEST A STAR GETS, AND IT IS UNDER THE BLOOM'S KNEE.
        ///
        /// A star is a point, and everything above rests on it staying one. The
        /// glow does not care: Bloom's threshold is 0.78, so a star over it comes
        /// back as a soft halo several times its own size and the claim SkyChecks
        /// makes about the size of a star stops being true of what is on the
        /// screen. Under the knee it is exactly as big as it is drawn.
        ///
        /// It is still bright — a dim star is a smudge, not a subtle star — and
        /// during a lift, when the machine is being looked through and the knee
        /// comes down to 0.40, the whole picture blooms and the sky goes with it.
        /// That is the effect doing what it is for, on a frame where nothing is
        /// being read off cell brightness anyway.
        /// </summary>
        public const float StarPeak = 0.74f;

        /// <summary>How much of the field carries a star in its nearest shell.</summary>
        public const float Density = 0.10f;

        /// <summary>
        /// THE RADIUS OF THE BIGGEST STAR, IN PIXELS, and it is a pixel count
        /// rather than a fraction of anything on purpose: a star is the one thing
        /// on this screen that should be exactly as big on a 1080 phone as on a
        /// 1440 one. The two further shells take 0.78 and 0.62 of it.
        ///
        /// SkyChecks holds it against the size of a cell. That is the whole of
        /// the argument the camera's old clear-to-black comment was making — a
        /// point is not a claim about the board, an area fill is — and it is the
        /// form of it that a number can keep.
        /// </summary>
        public const float StarPx = 1.7f;

        Material _mat;
        Vector2 _drift;

        public static Sky Build(Transform under)
        {
            var go = new GameObject("Sky");
            go.transform.SetParent(under, false);
            var sky = go.AddComponent<Sky>();
            sky.Compose();
            return sky;
        }

        void Compose()
        {
            // A QUAD IN CLIP SPACE. The vertex shader writes these coordinates
            // straight out, so the mesh is the screen and the camera's size,
            // offset, punch and shake never touch it.
            var mesh = new Mesh { name = "sky" };
            mesh.vertices = new[]
            {
                new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                new Vector3(-1,  1, 0), new Vector3(1,  1, 0)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };

            // AND BOUNDS BIG ENOUGH THAT CULLING CANNOT REACH IT. The renderer is
            // at the origin and draws the whole screen; Unity would cull it the
            // moment the camera moved off, and a culled background is a black
            // screen with no error attached to it.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            Shader sh = Shader.Find("Singularity/Sky");
            if (sh == null) { Debug.LogError("Singularity/Sky shader missing"); enabled = false; return; }

            _mat = new Material(sh) { name = "sky" };
            _mat.SetFloat("_Peak", StarPeak);
            _mat.SetFloat("_Nebula", NebulaPeak);
            _mat.SetFloat("_Density", Density);
            _mat.SetFloat("_StarPx", StarPx);
            _mat.SetColor("_Tint", Tint);
            r.sharedMaterial = _mat;
        }

        /// <summary>
        /// The wash, and it is the palette's own deep void rather than a new
        /// colour: the sky is the same cold blue the board's furthest cells fade
        /// into, which is what stops it reading as a second art direction.
        /// </summary>
        public static Color Tint => new Color(Palette.Trace.r * 0.42f, Palette.Trace.g * 0.42f, Palette.Trace.b * 0.42f, 1f);

        public void Tick(float dt)
        {
            if (_mat == null) return;

            // LEGIBLE TAKES THE SKY AWAY ENTIRELY, rather than dimming it. Half a
            // starfield is still a starfield behind the type.
            float fade = Access.Legible ? 0f : 1f;
            _mat.SetFloat("_Fade", fade);
            if (fade <= 0f) return;

            // and STILL takes the movement without taking the picture
            Vector2 want = Tilt.Look * (Travel * Access.MotionAmount);

            // The reading is already filtered; this is the second, gentler one
            // that stops a single noisy accelerometer frame from being a visible
            // step on a display that is otherwise perfectly still.
            _drift = Vector2.Lerp(_drift, want, 1f - Mathf.Exp(-6f * dt));
            _mat.SetVector("_Drift", new Vector4(_drift.x, _drift.y, 0f, 0f));
        }
    }
}
