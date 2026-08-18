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

        // how far out the camera has had to pull to keep the solid in its window,
        // and how big the solid is when it is square to us
        float _room = 1f;
        int _boardN;

        /// <summary>
        /// EVERY IMPULSE IN THIS FILE GOES THROUGH ONE NUMBER, and at STILL that
        /// number is zero — not forty per cent, which is what it used to bottom
        /// out at. Forty per cent of a plate fire is still a plate fire to the
        /// player who cannot take one.
        /// </summary>
        static float Amount => Access.MotionAmount;

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

            // THE EAR. Unity plays nothing at all without one of these, and it says
            // so as a warning rather than an error — so a build with no listener is
            // a silent game that looks like it is working. Every scene here is built
            // in code from Bootstrap, which means there is no scene-authored camera
            // carrying the default one, which means it has to be put here.
            go.AddComponent<AudioListener>();
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
        public void Fit(int n, float margin = 1.28f) => FitTo(Layout.BoardRect(), n, margin);

        /// <summary>
        /// FIT INTO A NAMED RECTANGLE, because the game has two of them.
        ///
        /// The play window is the square the HUD's stroke is drawn around. The
        /// title's is the gap between the masthead and the controls, which is a
        /// different shape in a different place — and the attract cube was being
        /// framed with the play window's centre, so it sat low on the one screen
        /// where it is the subject rather than the board.
        /// </summary>
        public void FitTo(Rect board, int n, float margin)
        {
            float screenH = Mathf.Max(1f, Screen.height);

            // the cube needs this many world units across, allowing for the fact
            // that a cube mid-fold is wider than its face
            _boardN = n;

            // THE MARGIN'S JOB CHANGED. It was here to leave room for a cube
            // mid-fold, and it never had enough — a fold is root two wide and this
            // is 1.28, so the solid left the aperture on every horizontal swipe.
            // Room() contains it exactly now, which means this number no longer
            // decides whether the cube CLIPS. What it decides is how often the
            // camera has to move: at 1.28 a forty-five degree fold costs a twenty
            // per cent pull-out and everything else costs nothing. Tighter is a
            // bigger board and a busier camera; that is the whole trade.
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

        /// <summary>
        /// STAND BACK AND WATCH IT GO. An extra, held pull on the orthographic
        /// size that whoever set it is responsible for clearing — unlike Room,
        /// which is the framing and eases itself home, and unlike Punch, which is
        /// an impulse and decays. The exit is the only thing that uses it, and it
        /// wants the camera to stay out for the whole of the throw.
        ///
        /// Scaled by the motion amount at the point of use, so STILL loses the
        /// pull and keeps the containment.
        /// </summary>
        public float Pull { get; set; }

        /// <summary>
        /// AIM AT ONE CELL AND CLOSE ON IT. Zero is the framing the game is played
        /// in; one is that cell filling the aperture with everything else outside
        /// it. Only the finale uses it — the board is a whole object every other
        /// second of the game and the camera has no business preferring part of it.
        /// </summary>
        public float Close { get; set; }
        public Vector3 At { get; set; }

        public void Shake(float amt) => _trauma = Mathf.Min(1f, _trauma + amt * Amount);

        /// <summary>
        /// HOLD THE FRAME AT A LEVEL, rather than shoving it once.
        ///
        /// Shake ADDS, because every caller of it is an event: a fold lands, a
        /// plate fires, the machine breaks. Trauma decays at 2.4 a second, so an
        /// event's shove is gone in well under half a second and the next one
        /// stacks on whatever is left — which is exactly right for events and
        /// exactly wrong for a rumble that is supposed to RISE over two seconds.
        /// Called every frame, Shake wins the race against the decay by about ten
        /// to one and pins trauma at maximum from a fifth of the way in; what you
        /// get is not a star waking up, it is the picture coming off its mount.
        ///
        /// This sets the floor instead. An event on top of it still reads, because
        /// the max is taken rather than the assignment — a rumble cannot quieten a
        /// hit that happens during it.
        /// </summary>
        public void Rumble(float amt) => _trauma = Mathf.Max(_trauma, Mathf.Clamp01(amt) * Amount);
        public void Punch(float amt) => _punch = Mathf.Clamp(_punch + amt * Amount, -0.12f, 0.12f);

        public void Squash(bool axisX, float amt)
        {
            amt *= Amount;
            _sqX = axisX ? 1f + amt : 1f - amt * 0.6f;
            _sqY = axisX ? 1f - amt * 0.6f : 1f + amt;
            _sqT = 0f;
        }

        /// <summary>
        /// KEEP THE SOLID INSIDE ITS OWN WINDOW.
        ///
        /// A cube square to the camera is n across. The same cube turned forty-five
        /// degrees is n times root two, and corner-on it is n times root three — so
        /// a board fitted to the aperture at rest is half again too big for it the
        /// moment a fold starts, and the thing runs out through the frame that is
        /// supposed to be the edge of the window. It did.
        ///
        /// The fix is not a bigger margin. Reserving the worst case permanently
        /// costs thirty per cent of the board at rest, every second of play, to
        /// pay for a shape that exists for half of one animation — and it would
        /// still be wrong for the matrix, which the player can hold at any angle
        /// they like.
        ///
        /// So the camera measures. The cube's whole transform rotates, so its
        /// on-screen extent is the rotated axis-aligned box of a known solid: three
        /// dot products, exact, no bounds query and no guessing. The size needed to
        /// contain that inside the aperture is arithmetic, and the camera takes
        /// whichever is larger, that or the resting fit.
        ///
        /// OUT AT ONCE AND BACK SLOWLY. Easing outward would let the corner clip
        /// for the two frames the ease takes, which is the whole bug; the extent it
        /// is tracking is already a smooth function of the fold, so snapping to it
        /// IS smooth. Coming back is eased, because a camera that returns as fast
        /// as it left reads as a bounce.
        ///
        /// It is not gated on the motion setting, and that is deliberate. This is
        /// not an impulse on top of the picture, it is the framing of the picture:
        /// STILL turns off shake, kick, punch and the bent clock, and a player who
        /// asked for those to stop did not ask for the cube to leave the screen.
        /// </summary>
        float Room(float dt, Transform cube, bool contain)
        {
            float want = 1f;

            if (contain && cube != null && _boardN > 1 && _baseSize > 0.0001f)
            {
                Quaternion r = cube.rotation;
                Vector3 rx = r * Vector3.right, ry = r * Vector3.up, rz = r * Vector3.forward;

                // half the solid, plus a hair so it never sits ON the stroke
                float e = _boardN * 0.5f * 1.04f;
                float ex = e * (Mathf.Abs(rx.x) + Mathf.Abs(ry.x) + Mathf.Abs(rz.x));
                float ey = e * (Mathf.Abs(rx.y) + Mathf.Abs(ry.y) + Mathf.Abs(rz.y));

                Rect ap = Layout.ApertureRect();
                float screenH = Mathf.Max(1f, Screen.height);

                // orthographicSize is half the world height of the WHOLE screen, so
                // the aperture only sees the fraction of it that it covers
                float need = Mathf.Max(ex * screenH / ap.width, ey * screenH / ap.height);
                want = Mathf.Max(1f, need / _baseSize);
            }

            _room = want > _room ? want : Mathf.MoveTowards(_room, want, dt * 1.1f);
            return _room;
        }

        public void Tick(float dt, Transform cube, bool contain = true)
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

            float cl = Mathf.Clamp01(Close);
            Vector2 aim = Vector2.Lerp(_baseOffset, new Vector2(At.x, At.y), cl * cl * (3f - 2f * cl));

            // AND THE CLOSE IS NOT GATED ON MOTION. It is not an impulse on top of
            // the picture, it is the framing — the same argument Room makes. A
            // player who asked the board to hold still did not ask to be shown the
            // wrong part of it.
            float zoom = Mathf.Lerp(1f, 0.16f, cl);

            // SHAKE IS A SCREEN GESTURE MEASURED IN WORLD UNITS, and those two
            // agree only at one zoom. Trauma of one is half a world unit against a
            // four-unit frame — a fifth of the height, which reads as a hit; the
            // same half unit against the closed frame's 0.64 is most of the screen,
            // and the shockwave that is supposed to rattle the star would instead
            // fling it out of shot and shake an empty picture. So every offset
            // travels with the aperture and the AMPLITUDE ON SCREEN is what stays
            // constant, which is the only part of it anybody can see.
            cam.transform.localPosition = new Vector3(
                aim.x + (sx + _kickX * _kickDecay) * zoom,
                aim.y + (sy + _kickY * _kickDecay) * zoom,
                -40f);
            cam.orthographicSize = _baseSize * Room(dt, cube, contain)
                                 * (1f + Pull * Amount) * (1f - _punch)
                                 * zoom;

            if (cube != null)
            {
                float k = 1f - _sqT * _sqT;
                cube.localScale = new Vector3(Mathf.Lerp(1f, _sqX, k), Mathf.Lerp(1f, _sqY, k), 1f);
            }
        }
    }
}
