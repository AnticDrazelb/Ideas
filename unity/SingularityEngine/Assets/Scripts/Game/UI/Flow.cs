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
            public Text measure;    // when the height is however tall the text is
            public float lineH;     // line-height multiplier for a measured item
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
        public RectTransform AddText(RectTransform rt, Text t, float maxW, float marginBottom, float lineH = 1f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            _items.Add(new Item { rt = rt, maxW = maxW, mb = marginBottom, measure = t, lineH = lineH });
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
                float h = it.h;

                if (it.measure != null)
                {
                    // the generator needs the final width before it can say how many
                    // lines there are
                    var mr = it.measure.rectTransform;
                    mr.sizeDelta = new Vector2(w, mr.sizeDelta.y);
                    h = it.measure.preferredHeight * it.lineH;
                }

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
                float h = it.h;
                if (it.measure != null) h = it.measure.preferredHeight * it.lineH;

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
