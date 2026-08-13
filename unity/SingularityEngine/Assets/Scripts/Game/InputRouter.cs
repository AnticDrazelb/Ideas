using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// INPUT — one finger, three verbs, and the clash taken out.
    ///
    /// A gesture is a TAP until it has travelled far enough to be a drag, and once
    /// it is a drag it is locked to one axis for the rest of its life. No gesture
    /// ever changes its mind halfway, because a control that reinterprets itself
    /// under the thumb feels broken even when it is right.
    ///
    /// THE THIRD VERB COSTS THE OTHER TWO NOTHING. A gesture is a tap until it
    /// travels (drag) or until it stays (matrix). The timer is armed on every
    /// touch and disarmed by the first pixel of travel, so a drag can never become
    /// a peek and a peek can never become a walk — once the lattice is glass,
    /// letting go only closes it.
    /// </summary>
    public class InputRouter
    {
        public const float TapSlop = 14f;
        public const float HoldSeconds = 0.210f;

        readonly Session _s;
        readonly CubeView _view;
        readonly Camera _cam;

        Vector2 _start, _last;
        float _downAt;
        bool _down, _peeking;

        public bool Enabled = true;

        public InputRouter(Session s, CubeView view, Camera cam)
        {
            _s = s; _view = view; _cam = cam;
        }

        static float PxPerQuarter() => Mathf.Max(90f, Mathf.Min(Screen.width, Screen.height) * 0.40f);

        public void Tick(float dt)
        {
            if (!Enabled || _s.lv == null) { ReleasePeek(); return; }

            Vector2 p;
            bool downNow = false, upNow = false, held = false;

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                p = t.position;
                downNow = t.phase == TouchPhase.Began;
                upNow = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = !upNow;
            }
            else
            {
                p = Input.mousePosition;
                downNow = Input.GetMouseButtonDown(0);
                upNow = Input.GetMouseButtonUp(0);
                held = Input.GetMouseButton(0);
            }

            if (downNow) Down(p);
            else if (_down && held) Move(p);
            else if (_down && upNow) Up(p);

            if (_down && !_peeking && !_s.drag.HasAxisSafe()
                && Time.unscaledTime - _downAt > HoldSeconds
                && (p - _start).sqrMagnitude < TapSlop * TapSlop)
            {
                PeekOn();
            }

            // the matrix eases in and out rather than snapping, so a quick hold
            // that resolves as a tap does not strobe the board
            _view.peekAmt = Mathf.MoveTowards(_view.peekAmt, _peeking ? 1f : 0f, dt * 6f);

            Keyboard();
        }

        void Down(Vector2 p)
        {
            if (_s.won || _s.anim != null || _s.walking != null) return;
            _down = true;
            _start = _last = p;
            _downAt = Time.unscaledTime;
            _s.drag = new Session.Drag();
        }

        void Move(Vector2 p)
        {
            if (_s.drag == null) return;
            Vector2 d = p - _start;

            if (_peeking)
            {
                // ORBIT. The hold has already landed, so this gesture is looking
                // rather than folding: it moves the camera one-to-one with the
                // thumb, at the same pixels-per-quarter-turn the drag uses.
                float q = PxPerQuarter() * 0.66f;
                _view.peekYaw += (p.x - _last.x) / q * (Mathf.PI / 2f);
                _view.peekPitch = Mathf.Clamp(_view.peekPitch - (p.y - _last.y) / q * (Mathf.PI / 2f),
                                              -CubeView.PeekMaxPitch, CubeView.PeekMaxPitch);
                _last = p;
                return;
            }

            if (!_s.drag.hasAxis)
            {
                if (Mathf.Abs(d.x) < TapSlop && Mathf.Abs(d.y) < TapSlop) return;
                _s.drag.hasAxis = true;
                _s.drag.axisX = Mathf.Abs(d.x) > Mathf.Abs(d.y);
            }

            float amt = _s.drag.axisX ? d.x : d.y;
            float qq = Mathf.Clamp(amt / PxPerQuarter(), -1f, 1f);
            float was = Mathf.Abs(_s.drag.ang);
            _s.drag.ang = qq * Mathf.PI / 2f;

            // the detent is something you can feel coming
            if (was < Mathf.PI / 4f && Mathf.Abs(_s.drag.ang) >= Mathf.PI / 4f) Haptics.Buzz(Haptics.Fold);
            _last = p;
        }

        void Up(Vector2 p)
        {
            _down = false;
            var drag = _s.drag;
            if (drag == null) { ReleasePeek(); return; }

            if (!drag.hasAxis)
            {
                bool wasPeek = _peeking;
                _s.drag = null;
                ReleasePeek();
                if (wasPeek) return;                 // a hold is looking, never walking
                if (CellAtPoint(_start, out int u, out int v)) _s.TapCell(u, v);
                return;
            }

            float ang = drag.ang;
            bool axisX = drag.axisX;
            if (Mathf.Abs(ang) > Mathf.PI / 4f)
            {
                int t = axisX ? (ang > 0 ? 1 : 0) : (ang > 0 ? 2 : 3);
                if (!_s.TryTurn(t)) _s.RefuseTurn(t);
            }
            else _s.SpringBack();
        }

        void PeekOn()
        {
            if (_peeking) return;
            _peeking = true;
            _view.peekYaw = CubeView.PeekYaw;
            _view.peekPitch = CubeView.PeekPitch;
            if (Store.Data.sawPeek == 0) { Store.Data.sawPeek = 1; Store.Save(); }
        }

        void ReleasePeek()
        {
            if (!_peeking) return;
            _peeking = false;
            _view.peekYaw = 0f;
            _view.peekPitch = 0f;
        }

        /// <summary>
        /// WHERE DID THEY ACTUALLY TAP?
        ///
        /// The web build needed two answers to this: arithmetic while the board was
        /// flat, and a search against the drawn picture while it was leaned over,
        /// because a view mode that draws one thing and hits another is not a view
        /// mode, it is a bug with a switch on it.
        ///
        /// Here there is one answer, and it is the honest one: ask the renderer.
        /// Project every surface cell exactly as it is drawn and take the nearest
        /// centre, with a radius so a tap into empty space stays a tap into empty
        /// space rather than snapping to whatever happened to be closest. It cannot
        /// drift from what is on screen, because it reads what is on screen.
        /// </summary>
        public bool CellAtPoint(Vector2 screen, out int bu, out int bv)
        {
            bu = bv = -1;
            if (_s.surf == null) return false;

            float cell = _cam.orthographicSize * 2f / Screen.height;   // world units per pixel
            float radius = 0.62f / (cell * cell);                      // in squared pixels
            float bd = radius;
            bool found = false;

            for (int u = 0; u < _s.N; u++)
                for (int v = 0; v < _s.N; v++)
                {
                    Surf s = _s.surf[u * _s.N + v];
                    if (!s.has) continue;
                    Vector3 world = _view.cube.TransformPoint(CubeMesh.CellToObject(_s.N, s.w));
                    Vector3 sp = _cam.WorldToScreenPoint(world);
                    float dx = sp.x - screen.x, dy = sp.y - screen.y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < bd) { bd = d2; bu = u; bv = v; found = true; }
                }

            return found;
        }

        /// <summary>Desktop, for anyone who opens this on one. The same split the thumb gets.</summary>
        void Keyboard()
        {
            if (_s.won) return;

            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift)) PeekOn();
            else if (_peeking && !_down) ReleasePeek();

            int t = -1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) t = 0;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) t = 1;
            else if (Input.GetKeyDown(KeyCode.UpArrow)) t = 2;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) t = 3;

            if (t >= 0)
            {
                if (_peeking)
                {
                    // held with the matrix open, the arrows orbit instead
                    _view.peekYaw += (t == 0 ? -0.18f : t == 1 ? 0.18f : 0f);
                    _view.peekPitch = Mathf.Clamp(_view.peekPitch + (t == 2 ? 0.18f : t == 3 ? -0.18f : 0f),
                                                  -CubeView.PeekMaxPitch, CubeView.PeekMaxPitch);
                }
                else if (!_s.TryTurn(t)) _s.RefuseTurn(t);
            }

            if (Input.GetKeyDown(KeyCode.Z)) _s.Undo();
        }
    }

    static class DragExt
    {
        public static bool HasAxisSafe(this Session.Drag d) => d != null && d.hasAxis;
    }
}
