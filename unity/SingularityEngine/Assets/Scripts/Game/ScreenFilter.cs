using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// The brightness and contrast controls, as a blit on the camera.
    ///
    /// The web original does this with a CSS filter over the whole document, which
    /// is free because the compositor was going to composite anyway. Unity has no
    /// equivalent, so it is a full-screen pass — and the important part is the one
    /// the original also has: AT 100/100 NOTHING HAPPENS. The component switches
    /// itself off, the blit never runs, and the cost of these two settings is zero
    /// for everyone who leaves them alone.
    ///
    /// It sits on the camera rather than on a UI overlay because it has to catch
    /// the board AND the interface. A player raising brightness to read the board
    /// in sunlight has not asked for the HUD to stay dim.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenFilter : MonoBehaviour
    {
        Material _mat;
        int _brightWas = -1, _contrastWas = -1;

        public static ScreenFilter Attach(Camera cam)
        {
            var f = cam.gameObject.AddComponent<ScreenFilter>();
            f.Refresh();
            return f;
        }

        /// <summary>Read the settings back out of the store; call after Calibrate changes one.</summary>
        public void Refresh()
        {
            int b = Store.Data.bright, c = Store.Data.contrast;
            if (b == _brightWas && c == _contrastWas) return;
            _brightWas = b; _contrastWas = c;

            bool identity = b == 100 && c == 100;
            enabled = !identity;
            if (identity) return;

            if (_mat == null)
            {
                Shader sh = Shader.Find("Singularity/Filter");
                if (sh == null) { Debug.LogWarning("Singularity/Filter shader missing"); enabled = false; return; }
                _mat = new Material(sh) { name = "filter", hideFlags = HideFlags.HideAndDontSave };
            }

            _mat.SetFloat("_Brightness", b / 100f);
            _mat.SetFloat("_Contrast", c / 100f);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null) { Graphics.Blit(src, dst); return; }
            Graphics.Blit(src, dst, _mat);
        }
    }
}
