using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Singularity.UI
{
    /// <summary>
    /// A SCREEN IS A CENTRED COLUMN, AND THAT IS THE WHOLE OF THE LAYOUT MODEL.
    ///
    /// Every screen in the shipped build is one `.layer`, and a `.layer` is six
    /// lines of CSS:
    ///
    ///     display:flex; flex-direction:column; align-items:center;
    ///     padding: sa+22px sa+20px;
    ///     overflow-y:auto;
    ///     .layer::before,.layer::after{ content:''; flex:1 1 auto; min-height:0; }
    ///     .layer>*{ flex-shrink:0; }
    ///
    /// The two elastic pseudo-elements are the important part and they are easy to
    /// miss: they take all the leftover height and split it evenly, so THE COLUMN
    /// IS VERTICALLY CENTRED — and when the content is taller than the screen they
    /// collapse to nothing and it scrolls from the top instead. One declaration
    /// covers both a title screen on a tall phone and a manual on a short one.
    ///
    /// This port had been placing everything with absolute offsets from the top of
    /// a 1280-unit canvas — `new Vector2(0, -170)`, `-252`, `-346` — which is that
    /// composition transcribed at one screen size. It cannot be right anywhere
    /// else, because it is not the rule, it is one of the rule's outputs. So the
    /// rule is what gets ported.
    ///
    /// It re-runs when the rect changes rather than only at build, which is what
    /// makes a rotation or a fold work without rebuilding a screen.
    /// </summary>
    public class Flow : MonoBehaviour
    {
        struct Item
        {
            public RectTransform rt;
            public float h;         // fixed height, or 0 when measured
            public float maxW;
            public float mb;        // margin-bottom
            public string measure;  // when the height is however tall the text is
            public float size, track, lineH;
        }

        /// <summary>
        /// HOW TALL A PARAGRAPH IS, WORKED OUT RATHER THAN ASKED FOR.
        ///
        /// uGUI will answer this with `preferredHeight`, but only against the rect
        /// width it last laid out at — and a column has to know the height BEFORE
        /// it can place anything, so the answer would always be one frame stale and
        /// the first frame of every screen would be wrong.
        ///
        /// This interface is set entirely in a fixed-pitch face, which makes the
        /// question arithmetic instead: every glyph advances by the same amount, so
        /// the wrap can simply be run. The advance is the character cell plus the
        /// rule's tracking, and 0.6em is the cell of every monospace face this
        /// stack can resolve to.
        /// </summary>
        public static float TextHeight(string s, float width, float size, float track, float lineH)
        {
            if (string.IsNullOrEmpty(s) || width <= 1f) return size * lineH;

            float advance = size * (0.6f + track);
            int perLine = Mathf.Max(1, Mathf.FloorToInt(width / advance));

            int lines = 0;
            foreach (string para in s.Split('\n'))
            {
                int used = 0;
                lines++;
                foreach (string word in para.Split(' '))
                {
                    int need = word.Length + (used > 0 ? 1 : 0);
                    if (used > 0 && used + need > perLine) { lines++; used = word.Length; }
                    else used += need;
                    // a word longer than the line breaks inside itself
                    while (used > perLine) { lines++; used -= perLine; }
                }
            }
            return lines * size * lineH;
        }

        readonly List<Item> _items = new List<Item>();
        RectTransform _rt;
        RectTransform _view;
        Vector2 _last = new Vector2(-1f, -1f);

        /// <summary>The `::before`/`::after` spacers. Off for a column pinned to the top.</summary>
        public bool centred = true;

        /// <summary>Height of the last laid-out column, in CSS pixels.</summary>
        public float Height { get; private set; }

        public static Flow On(RectTransform content, RectTransform viewport)
        {
            var f = UiKit.Ensure<Flow>(content.gameObject);
            f._rt = content;
            f._view = viewport;
            return f;
        }

        /// <summary>A child of the column, with the margin that follows it.</summary>
        public RectTransform Add(RectTransform rt, float height, float maxW, float marginBottom)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            _items.Add(new Item { rt = rt, h = height, maxW = maxW, mb = marginBottom });
            _last = new Vector2(-1f, -1f);
            return rt;
        }

        /// <summary>
        /// A child whose height is however tall its text turns out to be — a
        /// paragraph, a manual line, a hint under a switch. Measured at the width
        /// it will actually be given, because that is what decides the wrap.
        /// </summary>
        public RectTransform AddText(RectTransform rt, string text, float size, float track,
                                     float maxW, float marginBottom, float lineH = 1f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            _items.Add(new Item
            {
                rt = rt, maxW = maxW, mb = marginBottom,
                measure = text, size = size, track = track, lineH = lineH,
            });
            _last = new Vector2(-1f, -1f);
            return rt;
        }

        /// <summary>Nothing, of a given height. `.heroSlot` is one, and so is a gap.</summary>
        public void Space(float h)
        {
            var rt = UiKit.Rect(_rt, "space", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Add(rt, h, 0f, 0f);
        }

        void LateUpdate()
        {
            if (_view == null) return;
            Vector2 size = _view.rect.size;
            if (size == _last) return;
            _last = size;
            Layout();
        }

        public void Layout()
        {
            if (_rt == null || _view == null) return;

            float avail = _view.rect.width;
            float viewH = _view.rect.height;
            if (avail <= 1f) return;

            // pass one: how tall is the column, at the width it is actually getting
            float total = 0f;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                float w = it.maxW > 0f ? Mathf.Min(it.maxW, avail) : avail;
                float h = it.measure != null
                    ? TextHeight(it.measure, w, it.size, it.track, it.lineH)
                    : it.h;
                total += h + it.mb;
            }
            // the last child's margin is inside the column in CSS too, so it stays

            Height = total;

            // pass two: place them, from the top of whatever space the spacers left
            float y = centred && total < viewH ? (viewH - total) * 0.5f : 0f;

            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                float w = it.maxW > 0f ? Mathf.Min(it.maxW, avail) : avail;
                float h = it.measure != null
                    ? TextHeight(it.measure, w, it.size, it.track, it.lineH)
                    : it.h;

                it.rt.sizeDelta = new Vector2(w, h);
                it.rt.anchoredPosition = new Vector2(0f, -y);
                y += h + it.mb;
            }

            // A column that fits does not scroll. One that does not fit is exactly
            // as tall as it is, and the viewport takes it from there.
            _rt.sizeDelta = new Vector2(0f, Mathf.Max(total, viewH));
        }
    }
}
