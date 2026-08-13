using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// The camera, and the trauma.
    ///
    /// ORTHOGRAPHIC AND AXIS-ALIGNED, ALWAYS. That is not a look, it is the rule:
    /// the collapse says every cell sharing a screen column is one place, and only
    /// a parallel projection down an axis makes that true of the picture as well
    /// as of the maths. A perspective camera would show you cells that the solver
    /// says are the same square landing on different pixels.
    ///
    /// Everything else here is feel. A landed fold gets an aimed kick along its
    /// own axis, trauma on top so the whole frame is briefly unsafe, and a punch
    /// of zoom, which is what makes it read as a hit rather than a slide.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;

        float _trauma, _punch, _kickX, _kickY, _kickDecay;
        float _sqX, _sqY, _sqT = 1f;
        float _baseSize = 4f;

        /// <summary>Effects at 40% rather than nothing when the player has turned them down.</summary>
        static float Amount => Store.Data.fx != 0 ? 1f : 0.4f;

        public void Init()
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // THERE IS NOTHING BEHIND THE BOARD, AND THAT IS THE STATEMENT. A void
            // column is unrendered space: not a dark room, not deep sky, nothing.
            // Anything painted there is the display claiming to have data it does
            // not have.
            cam.backgroundColor = Palette.Void;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 200f;
            cam.transform.position = new Vector3(0, 0, -40f);
            cam.transform.rotation = Quaternion.identity;   // looks down +Z
            cam.allowHDR = false;
            cam.allowMSAA = true;
        }

        /// <summary>
        /// Fit the cube to the screen with a margin for the two HUD bands. The
        /// cube is one unit per cell, so this is the only place that needs to know
        /// how big a cube is.
        /// </summary>
        public void Fit(int n, float margin = 1.28f)
        {
            // the diagonal, because a cube mid-fold is wider than its face
            float halfFace = n * 0.5f;
            float need = halfFace * margin;
            float aspect = cam.aspect <= 0.0001f ? 1f : cam.aspect;
            _baseSize = aspect < 1f ? need / aspect : need;
            _baseSize = Mathf.Max(_baseSize, need);
        }

        public void Kick(float mag, float dx, float dy)
        {
            _kickX += dx * mag * 0.012f * Amount;
            _kickY += dy * mag * 0.012f * Amount;
            _kickDecay = 1f;
        }

        public void Shake(float amt) => _trauma = Mathf.Min(1f, _trauma + amt * Amount);
        public void Punch(float amt) => _punch = Mathf.Clamp(_punch + amt * Amount, -0.12f, 0.12f);

        public void Squash(bool axisX, float amt)
        {
            amt *= Amount;
            _sqX = axisX ? 1f + amt : 1f - amt * 0.6f;
            _sqY = axisX ? 1f - amt * 0.6f : 1f + amt;
            _sqT = 0f;
        }

        public void Tick(float dt, Transform cube)
        {
            _trauma = Mathf.Max(0f, _trauma - dt * 2.4f);
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 0.42f);
            _kickDecay = Mathf.Max(0f, _kickDecay - dt * 5.2f);
            _kickX = Mathf.MoveTowards(_kickX, 0f, dt * 2.6f);
            _kickY = Mathf.MoveTowards(_kickY, 0f, dt * 2.6f);
            _sqT = Mathf.Min(1f, _sqT + dt * 5f);

            float t2 = _trauma * _trauma;
            float sx = (Mathf.PerlinNoise(Time.time * 34f, 0f) - 0.5f) * 2f * t2 * 0.55f;
            float sy = (Mathf.PerlinNoise(0f, Time.time * 31f) - 0.5f) * 2f * t2 * 0.55f;

            cam.transform.localPosition = new Vector3(sx + _kickX * _kickDecay, sy + _kickY * _kickDecay, -40f);
            cam.orthographicSize = _baseSize * (1f - _punch);

            if (cube != null)
            {
                float k = 1f - _sqT * _sqT;
                cube.localScale = new Vector3(Mathf.Lerp(1f, _sqX, k), Mathf.Lerp(1f, _sqY, k), 1f);
            }
        }
    }
}
