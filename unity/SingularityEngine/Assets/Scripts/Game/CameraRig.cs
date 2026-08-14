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
        Vector2 _baseOffset;

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
        /// Fit the cube into the space the HUD is not using, and CENTRE IT THERE
        /// rather than on the screen.
        ///
        /// That distinction is the whole of this method. Centring on the screen and
        /// hoping the bands do not reach is what puts the primary action of the
        /// game on top of the hero object the first time somebody adds a button or
        /// runs it on a shorter phone. The bands have real heights; the board takes
        /// exactly what is left between them and sits in the middle of that.
        ///
        /// It works the same in both orientations because it asks for a RECTANGLE
        /// and fits to whichever of its sides is smaller — landscape is not a
        /// special case, it is the same question with a different answer.
        /// </summary>
        public void Fit(int n, float margin = 1.28f)
        {
            Rect board = Layout.BoardRect();
            float screenH = Mathf.Max(1f, Screen.height);

            // the cube needs this many world units across, allowing for the fact
            // that a cube mid-fold is wider than its face
            float need = n * margin;
            float shorter = Mathf.Max(1f, Mathf.Min(board.width, board.height));

            // orthographicSize is half the world height of the WHOLE screen
            _baseSize = need * screenH / (2f * shorter);

            // and the board's centre is not the screen's centre
            Vector2 drift = board.center - new Vector2(Screen.width * 0.5f, screenH * 0.5f);
            float worldPerPixel = 2f * _baseSize / screenH;
            _baseOffset = drift * worldPerPixel;

            PerfWatch.Settle();
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

            cam.transform.localPosition = new Vector3(
                _baseOffset.x + sx + _kickX * _kickDecay,
                _baseOffset.y + sy + _kickY * _kickDecay,
                -40f);
            cam.orthographicSize = _baseSize * (1f - _punch);

            if (cube != null)
            {
                float k = 1f - _sqT * _sqT;
                cube.localScale = new Vector3(Mathf.Lerp(1f, _sqX, k), Mathf.Lerp(1f, _sqY, k), 1f);
            }
        }
    }
}
