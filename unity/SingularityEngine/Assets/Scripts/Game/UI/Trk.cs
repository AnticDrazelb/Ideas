using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Singularity.UI
{
    /// <summary>
    /// LETTER-SPACING, WHICH uGUI DOES NOT HAVE AND THIS INTERFACE IS MADE OF.
    ///
    /// Every rule in the shipped sheet carries a tracking value — `.20em` on a
    /// button, `.26em` on the wordmark, `.24em` on an eyebrow, down to `.055em` on
    /// a cube's name — and the body sets `.1em` as the floor for everything that
    /// does not say otherwise. That is not decoration. The interface is monospace
    /// capitals, and monospace capitals set solid read as a serial number: the
    /// tracking is most of what makes a line of them read as an INSTRUMENT'S
    /// LABEL. A port that drops it has changed every screen in the game, and
    /// dropping it is what this port had done.
    ///
    /// uGUI's Text has no such property, so it is applied to the generated mesh
    /// instead. A BaseMeshEffect runs after the text has been laid out and before
    /// it is drawn, which means the glyphs are already positioned and all that is
    /// needed is to push each one along by the accumulated spacing.
    ///
    /// THE ALIGNMENT HAS TO BE RE-DONE, WHICH IS THE PART THAT IS EASY TO GET
    /// WRONG. Text centred the line before this ran, on the width the line had
    /// WITHOUT tracking. Adding space to every character makes it wider, so a
    /// centred line drifts right by half of what was added and a right-aligned one
    /// by all of it. Each line therefore gets a compensating shift, which is what
    /// a browser does when it lays the same line out: the advance of a character
    /// is its width PLUS the letter-spacing, and the line box is measured from
    /// those advances.
    /// </summary>
    public class Trk : BaseMeshEffect
    {
        /// <summary>Tracking in canvas units. One unit is one CSS pixel, so this is `em * font-size`.</summary>
        public float spacing;

        static readonly List<UIVertex> Buf = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null) return;
            if (Mathf.Abs(spacing) < 0.01f) return;

            int verts = vh.currentVertCount;
            if (verts < 4) return;

            var text = graphic as Text;
            TextAnchor anchor = text != null ? text.alignment : TextAnchor.MiddleCenter;
            bool centred = anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter
                        || anchor == TextAnchor.LowerCenter;
            bool right = anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight
                      || anchor == TextAnchor.LowerRight;

            Buf.Clear();
            var v = default(UIVertex);
            for (int i = 0; i < verts; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Buf.Add(v);
            }

            // A GLYPH IS FOUR VERTICES, AND A LINE IS FOUND FROM THE GEOMETRY.
            //
            // Reading the source string to find the line breaks would be wrong the
            // moment a label wraps — the breaks the generator chose are not in the
            // string. The top edge of each quad is, though: it steps down by a line
            // height and nothing else moves it.
            int glyphs = verts / 4;
            int lineStart = 0;
            float lineTop = Buf[0].position.y;

            for (int g = 0; g <= glyphs; g++)
            {
                bool end = g == glyphs;
                float top = end ? 0f : Buf[g * 4].position.y;

                // half a line's worth of drop is a new line; anything less is the
                // ordinary wobble between a cap and a descender on the same one
                if (end || top < lineTop - 1f)
                {
                    int n = g - lineStart;
                    if (n > 0)
                    {
                        float added = spacing * n;
                        float shift = centred ? -added * 0.5f : right ? -added : 0f;

                        for (int c = lineStart; c < g; c++)
                        {
                            float dx = shift + spacing * (c - lineStart);
                            for (int k = 0; k < 4; k++)
                            {
                                int idx = c * 4 + k;
                                UIVertex u = Buf[idx];
                                Vector3 p = u.position;
                                p.x += dx;
                                u.position = p;
                                Buf[idx] = u;
                            }
                        }
                    }
                    lineStart = g;
                    lineTop = top;
                }
            }

            for (int i = 0; i < verts; i++) vh.SetUIVertex(Buf[i], i);
        }
    }
}
