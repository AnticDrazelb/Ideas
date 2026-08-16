using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// THE BOARD IS LIT, NOT PRINTED.
    ///
    /// The web original draws every trace into a second canvas and composites that
    /// over the frame at the end, and this port did not have it — which is most of
    /// why the two builds look like different games from across a room. It is not
    /// decoration. A trace is not merely a brighter blue than the lattice; it is a
    /// brighter blue that SPILLS onto what is beside it, and the spill is what
    /// makes a grid of squares read as something powered.
    ///
    /// It also does a specific job for the marker. The photon ring is one thin
    /// bright circle a couple of pixels wide, and a couple of bright pixels on a
    /// phone is a hairline — until it blooms, at which point it is an edge with
    /// light coming off it, which is the entire claim that object makes.
    ///
    /// QUARTER RESOLUTION, AND THAT IS NOT A COMPROMISE. A bloom is a
    /// low-frequency signal by construction: resolving it finely spends samples on
    /// detail the effect exists to destroy. A sixteenth of the pixels, four blur
    /// taps, and on a mid-range phone it is comfortably inside a frame.
    ///
    /// It is attached BEFORE the brightness filter so the two compose in the order
    /// the player expects — the glow is part of the picture, and turning the
    /// brightness up raises the picture including its glow.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Bloom : MonoBehaviour
    {
        Material _mat;

        /// <summary>
        /// Where light starts, and how much of it comes back.
        ///
        /// These were 0.62 and 1.15, which put the knee below the trace colour's own
        /// brightness and then added more than the original back on top — so every
        /// trace, every cage edge and every particle bloomed at nearly full strength
        /// and the board came out as overlapping halos with the cells lost inside
        /// them. A glow that swallows the thing it is glowing off is not a glow.
        ///
        /// The knee sits above the lattice and inside the trace, so the two
        /// materials are still separated by the one property the whole board is read
        /// off — and the amount is well under one, because this is a rim on the
        /// picture rather than a second copy of it.
        /// </summary>
        const float Threshold = 0.78f;
        const float Intensity = 0.45f;

        /// <summary>
        /// HOW FAR THE KNEE COMES DOWN WHILE THE MACHINE IS BEING LOOKED THROUGH.
        ///
        /// 0 is the settled board, where a glow that swallows the thing it is
        /// glowing off is the failure mode and the numbers above are tuned against
        /// it. 1 is a held MATRIX, which is the opposite picture: the fill is gone,
        /// what is left is wire, and a wire with no bloom is a hairline. The knee
        /// drops under the wire colour and the amount goes past one, so the lines
        /// blow out where they cross — which is not decoration, it is what makes a
        /// lattice of them read as depth rather than as a flat tangle.
        ///
        /// Driven by the hold, scaled by the light setting, and it is the ONLY part
        /// of the schematic that is: the x-ray itself is the readout MATRIX exists
        /// to give, so a player who has turned light down loses the halo and keeps
        /// the answer.
        /// </summary>
        public float Lift { get; set; }

        const float LiftThreshold = 0.40f, LiftIntensity = 1.10f;

        /// <summary>How many halvings before blurring. Two is quarter res on each axis.</summary>
        const int Down = 2;

        public static Bloom Attach(Camera cam)
        {
            var b = cam.gameObject.AddComponent<Bloom>();
            Shader sh = Shader.Find("Singularity/Bloom");
            if (sh == null)
            {
                Debug.LogWarning("[Singularity] Singularity/Bloom shader missing — board will draw flat");
                b.enabled = false;
                return b;
            }
            b._mat = new Material(sh) { name = "bloom", hideFlags = HideFlags.HideAndDontSave };
            b._mat.SetFloat("_Threshold", Threshold);
            b._mat.SetFloat("_Intensity", Intensity);
            b.Refresh();
            return b;
        }

        /// <summary>
        /// EFFECTS OFF MEANS OFF. A player who has turned effects down has asked
        /// for less, and a full-screen pass they cannot see the benefit of is
        /// exactly the kind of thing that setting is for.
        /// </summary>
        public void Refresh() => enabled = _mat != null && Access.Bloom;

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null) { Graphics.Blit(src, dst); return; }

            float lift = Mathf.Clamp01(Lift) * Access.LightAmount;
            _mat.SetFloat("_Threshold", Mathf.Lerp(Threshold, LiftThreshold, lift));
            _mat.SetFloat("_Intensity", Mathf.Lerp(Intensity, LiftIntensity, lift));

            int w = Mathf.Max(1, src.width >> Down);
            int h = Mathf.Max(1, src.height >> Down);

            RenderTexture a = RenderTexture.GetTemporary(w, h, 0, src.format);
            RenderTexture b = RenderTexture.GetTemporary(w, h, 0, src.format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;

            Graphics.Blit(src, a, _mat, 0);            // knee

            _mat.SetVector("_Dir", new Vector4(1, 0, 0, 0));
            Graphics.Blit(a, b, _mat, 1);              // across
            _mat.SetVector("_Dir", new Vector4(0, 1, 0, 0));
            Graphics.Blit(b, a, _mat, 1);              // and down

            _mat.SetTexture("_BloomTex", a);
            Graphics.Blit(src, dst, _mat, 2);          // add it back

            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
    }
}
