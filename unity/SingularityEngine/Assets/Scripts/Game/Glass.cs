using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// THE SCREEN IS BEHIND SOMETHING, AND THE SOMETHING IS FILTHY.
    ///
    /// The case put the interface inside a machine. This is the pane across the
    /// front of it: thirty years of fingerprints, dust, hairline scratches, a
    /// smear where somebody wiped it with a sleeve, and the ghost of the pixel
    /// lattice underneath. It is the same photographed object the case came from
    /// and it does the other half of the same job — a display with nothing in
    /// front of it is a picture of a display, however good the bezel around it is.
    ///
    /// IT ONLY EVER ADDS LIGHT, AND THAT IS THE WHOLE DESIGN.
    ///
    /// Alpha-blending a photograph of dirty glass over this interface would be a
    /// catastrophe, and not a subtle one: the picture's median is near black, so
    /// blending it would DARKEN the display toward the dirt everywhere the dirt
    /// is dark — which is most of it — and the access audit's four dark grounds
    /// would go with it. Additive cannot do that. Dust catches light; it does not
    /// remove it. The layer can make a pixel brighter and has no way at all to
    /// make one dimmer, so nothing that was legible before it stops being.
    ///
    /// The pedestal is taken off the picture before it ever gets here — see
    /// unity/tools/chassis/glass.py — so the clean half of the glass contributes
    /// exactly zero rather than lifting the whole display off black. What is left
    /// is the dirt, at these strengths, as a fraction of full white:
    ///
    ///     half the glass   0.000      it is clean, and adds nothing
    ///     nine tenths      under 0.02
    ///     the worst speck  0.29       a grain of dust, a few units across
    ///
    /// IT IS IN FRONT OF EVERYTHING, INCLUDING THE SCREENS, because that is where
    /// glass is. The chassis is at 5 and the HUD at 10 and the menus at 20; the
    /// pane is at 25, and a menu that came forward is still behind it. This is
    /// the one layer in the project that is deliberately drawn over the board,
    /// which the chassis goes to some length to avoid — the difference is that
    /// the chassis is opaque and this is light.
    ///
    /// AND IT GOES AWAY WHEN ASKED. Legibility mode is a promise that the
    /// interface will stop performing, and a layer of grease on the screen is the
    /// most performing thing in the game.
    /// </summary>
    public static class Glass
    {
        /// <summary>
        /// How much of the picture reaches the screen.
        ///
        /// Chosen by looking, at a third and at two thirds, against a stand-in
        /// board of lit cells and rust-edged controls. Two thirds is a good
        /// picture of a filthy screen and starts to speckle the lit cells; a
        /// third is a machine somebody has been using. This is a third, plus a
        /// little, because the first choice was invisible on an OLED.
        /// </summary>
        public const float Strength = 0.34f;

        static GameObject _pane;

        /// <summary>The pane, on its own canvas in front of everything.</summary>
        public static Canvas Build()
        {
            Canvas c = Singularity.UI.UiKit.Canvas("Glass", 25);

            var tex = Resources.Load<Texture2D>("glass");
            if (tex == null)
            {
                Debug.LogWarning("Glass: Resources/glass.jpg is missing; the pane will not be drawn.");
                return c;
            }

            // Exactly the opening, because that is where the glass is — and under
            // a full-bleed child rather than directly under the canvas, because
            // UiKit.SafeArea zeroes the offsets of anything parented straight to
            // one. Put the inset there and the pane covers the metal as well.
            RectTransform safe = Singularity.UI.UiKit.Rect(c.transform, "safe",
                                                           Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform rt = Singularity.UI.UiKit.Rect(safe, "pane",
                                                         Vector2.zero, Vector2.one,
                                                         new Vector2(Chassis.InsetLeft, Chassis.InsetBottom),
                                                         new Vector2(-Chassis.InsetRight, -Chassis.InsetTop));
            _pane = rt.gameObject;

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height),
                                       new Vector2(0.5f, 0.5f), 1f);
            img.type = Image.Type.Simple;
            img.material = Additive;

            // The alpha is the strength: the shader premultiplies by it before an
            // additive blend, so the tint's alpha is a straight multiplier on how
            // much light this adds and nothing here is ever transparent.
            img.color = new Color(1f, 1f, 1f, Strength);

            // A FULL-SCREEN GRAPHIC THAT EATS EVERY TAP IS THE CLASSIC WAY TO
            // SHIP A GAME NOBODY CAN PLAY. It is in front of the buttons.
            img.raycastTarget = false;

            Refresh();
            return c;
        }

        /// <summary>Off under legibility, on otherwise. Safe to call before Build.</summary>
        public static void Refresh()
        {
            if (_pane == null) return;   // == null, not ?., see UiKit.Ensure
            bool want = !Access.Legible;
            if (_pane.activeSelf != want) _pane.SetActive(want);
        }

        static Material _add;

        /// <summary>
        /// The sprite shader the marker's halo uses, set to add rather than
        /// cover. Its own note explains why the blend modes are properties: two
        /// materials, one shader, and the sprite pipeline stays a single batch.
        /// A separate material from the glyphs' so that changing one cannot
        /// silently change the other.
        /// </summary>
        static Material Additive
        {
            get
            {
                // == null rather than ??=: a static cache survives a domain
                // reload while the native object it points at does not
                if (_add != null) return _add;

                Shader sh = Shader.Find("Singularity/Glyph");
                if (sh == null) { Debug.LogError("Singularity/Glyph shader missing"); sh = Shader.Find("Sprites/Default"); }

                _add = new Material(sh) { name = "glass" };
                _add.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _add.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                return _add;
            }
        }
    }
}
