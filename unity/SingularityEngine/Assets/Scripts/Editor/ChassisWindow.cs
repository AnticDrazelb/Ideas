using System.IO;
using Singularity.Game;
using UnityEditor;
using UnityEngine;

namespace Singularity.EditorTools
{
    /// <summary>
    /// SWAP THE MACHINE. Singularity → Chassis.
    ///
    /// Drop in a picture of a case, place the opening, press Cut. The window
    /// writes Resources/chassis.png and Resources/chassis.asset, and the game is
    /// wearing a different housing.
    ///
    /// THIS EXISTS BECAUSE THE ANSWER USED TO BE A PYTHON SCRIPT. The cut was
    /// unity/tools/chassis/cut.py, the measurements it printed were consts in
    /// Chassis.cs, and swapping the case therefore meant: install pillow and
    /// numpy, hand-measure four numbers into the script, run it, copy the file,
    /// hand-copy four more numbers into C#, and hope. Every step of that is a
    /// place to be one pixel out, and being one pixel out on a nine-slice shows
    /// up as a seam rather than as an error.
    ///
    /// WHAT IT CAN FIND AND WHAT IT CANNOT, said plainly because the difference
    /// is the whole usability of the thing:
    ///
    ///   IT FINDS THE SILHOUETTE, and therefore the crop, exactly. A flood fill
    ///   from the border over everything below the dark threshold; whatever it
    ///   cannot reach is the machine. On the shipped mock-up this reproduces the
    ///   crop that was hand-measured for cut.py to the pixel, on all four sides,
    ///   which is why the crop is not a field you type in any more.
    ///
    ///   IT CANNOT FIND THE OPENING. The obvious rule — the glass is the dark
    ///   rectangle in the middle — is false for exactly the pictures anybody
    ///   would use, because a mock-up of a machine has the machine's screen
    ///   turned ON. Half the reference's screen is brighter than the metal
    ///   around it, and every automatic detector tried on it found a chamfer
    ///   groove or the foot instead. So the four edges of the opening are yours,
    ///   they are seeded from the case's own proportions, and there is a live
    ///   overlay so that placing them is looking rather than arithmetic.
    ///
    /// THE PREVIEW IS THE POINT. Under the source picture is the case as the game
    /// will actually assemble it — the twelve real pieces, at a real panel size,
    /// stretched the way they will stretch. That is where a corner size that cuts
    /// through the notch, or a band too shallow to hold the metal, is visible.
    /// Nothing else in this project can show you that without a build.
    /// </summary>
    public class ChassisWindow : EditorWindow
    {
        const string ArtPath = "Assets/Resources/chassis.png";
        const string SpecPath = "Assets/Resources/chassis.asset";

        ChassisSpec _spec;
        Texture2D _source;          // decoded from the file, always readable
        string _sourcePath;
        Texture2D _preview;         // the cut, kept in memory until it is written
        string _status = "";
        bool _statusBad;

        // the panel the composed preview is drawn against, in canvas units
        float _panelW = 720f, _panelH = 1280f;

        [MenuItem("Singularity/Chassis")]
        public static void Open()
        {
            var w = GetWindow<ChassisWindow>("Chassis");
            w.minSize = new Vector2(460f, 620f);
        }

        void OnEnable()
        {
            _spec = AssetDatabase.LoadAssetAtPath<ChassisSpec>(SpecPath);
            if (_spec == null) return;
            if (_spec.source != null) Decode(PathOf(_spec.source));
            else if (!string.IsNullOrEmpty(_spec.sourcePath)) Decode(_spec.sourcePath);
        }

        void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                Body();
            }
        }
        Vector2 _scroll;

        void Body()
        {
            EditorGUILayout.LabelField("THE SPEC", EditorStyles.boldLabel);
            _spec = (ChassisSpec)EditorGUILayout.ObjectField("chassis.asset", _spec, typeof(ChassisSpec), false);
            if (_spec == null)
            {
                EditorGUILayout.HelpBox(
                    "There is no chassis.asset. The game falls back to the shipped case's " +
                    "measurements without one, which works and is not what you want while " +
                    "you are changing the case.", MessageType.Info);
                if (GUILayout.Button("Create Assets/Resources/chassis.asset")) CreateSpec();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("THE PICTURE", EditorStyles.boldLabel);
            var picked = (Texture2D)EditorGUILayout.ObjectField("source", _spec.source, typeof(Texture2D), false);
            if (picked != _spec.source)
            {
                _spec.source = picked;
                if (picked != null) { Decode(PathOf(picked)); Seed(); }
                EditorUtility.SetDirty(_spec);
            }

            // OR A PATH, because the reference mock-up is not in the project and
            // should not be: it is two thirds of a megabyte of source art for a
            // game that will never ship it. unity/tools/chassis/mockup.png is
            // ../../tools/chassis/mockup.png from the project folder.
            if (_spec.source == null)
            {
                EditorGUILayout.BeginHorizontal();
                string path = EditorGUILayout.TextField("or a path", _spec.sourcePath);
                if (path != _spec.sourcePath) { _spec.sourcePath = path; EditorUtility.SetDirty(_spec); }
                if (GUILayout.Button("Load", GUILayout.Width(60f)))
                {
                    Decode(_spec.sourcePath);
                    if (_source != null) Seed();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (_source == null)
            {
                EditorGUILayout.HelpBox("Pick the picture the case should be cut out of.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("size", _source.width + " x " + _source.height);

            // ROWS FIRST, because everything downstream is measured inside it.
            // The reference mock-up ends in five rows of pure white, which are not
            // part of any machine and which the flood fill cannot cross, so they
            // would otherwise join the silhouette and drag the crop with them.
            _spec.sourceRows = Mathf.Clamp(
                EditorGUILayout.IntField("rows to use", _spec.sourceRows), 1, _source.height);
            _spec.darkBelow = EditorGUILayout.Slider("background under", _spec.darkBelow, 1f, 40f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("crop", Fmt(_spec.crop) + "   " +
                                       (int)(_spec.crop.z - _spec.crop.x) + " x " +
                                       (int)(_spec.crop.w - _spec.crop.y));
            if (GUILayout.Button("Measure", GUILayout.Width(80f))) MeasureCrop();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("THE OPENING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Yours to place — see the overlay. The tool cannot find this: on a mock-up " +
                "with the machine switched on, the glass is not reliably darker than the " +
                "metal around it.", MessageType.None);
            Vector4 g = _spec.glass;
            g.x = EditorGUILayout.Slider("left", g.x, _spec.crop.x, _spec.crop.z);
            g.z = EditorGUILayout.Slider("right", g.z, _spec.crop.x, _spec.crop.z);
            g.y = EditorGUILayout.Slider("top", g.y, _spec.crop.y, _spec.crop.w);
            g.w = EditorGUILayout.Slider("bottom", g.w, _spec.crop.y, _spec.crop.w);
            _spec.glass = g;
            _spec.glassRadius = EditorGUILayout.Slider("corner radius", _spec.glassRadius, 0f, 80f);

            EditorGUILayout.LabelField("bezel, art pixels",
                string.Format("left {0:0} right {1:0} top {2:0} bottom {3:0}",
                              g.x - _spec.crop.x, _spec.crop.z - 1 - g.z,
                              g.y - _spec.crop.y, _spec.crop.w - 1 - g.w));

            SourceOverlay();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("THE NINE-SLICE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Both corner cuts have to land in a stretch of edge whose profile is " +
                "CONSTANT — past the notch on the top, above the waist on the side — " +
                "because only the four sides stretch and what stretches has to be " +
                "featureless along the direction it stretches in.", MessageType.None);
            _spec.corner = EditorGUILayout.FloatField("corner", _spec.corner);
            _spec.armW = EditorGUILayout.FloatField("arm width", _spec.armW);
            _spec.armH = EditorGUILayout.FloatField("arm height", _spec.armH);
            _spec.sideW = EditorGUILayout.FloatField("rail width", _spec.sideW);
            _spec.bandH = EditorGUILayout.FloatField("band height", _spec.bandH);
            _spec.scale = EditorGUILayout.Slider("art pixels to units", _spec.scale, 0.5f, 3f);
            EditorGUILayout.LabelField("bezel, canvas units",
                string.Format("left {0:0.#} right {1:0.#} top {2:0.#} bottom {3:0.#}",
                              _spec.left * _spec.scale, _spec.right * _spec.scale,
                              _spec.top * _spec.scale, _spec.bottom * _spec.scale));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("THE CASE, ASSEMBLED", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _panelW = EditorGUILayout.FloatField("panel", _panelW);
            _panelH = EditorGUILayout.FloatField("x", _panelH);
            EditorGUILayout.EndHorizontal();
            Composed();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cut")) Cut(false);
            if (GUILayout.Button("Cut and write")) Cut(true);
            EditorGUILayout.EndHorizontal();

            if (_status.Length > 0)
                EditorGUILayout.HelpBox(_status, _statusBad ? MessageType.Error : MessageType.Info);
        }

        static string Fmt(Vector4 v)
            => string.Format("{0:0} {1:0} {2:0} {3:0}", v.x, v.y, v.z, v.w);

        // ---- the source, decoded ---------------------------------------------

        /// <summary>
        /// READ FROM THE FILE, NOT FROM THE IMPORTED TEXTURE, and this is not
        /// fussiness. An imported texture is only readable on the CPU if somebody
        /// ticked Read/Write, it is compressed to a block format that has thrown
        /// colour away, and it may have been resampled to a power of two — so
        /// GetPixels32 on it returns a lossy, possibly wrong-sized picture that
        /// would then be cut and shipped. The bytes on disk are the picture.
        /// </summary>
        static string PathOf(Texture2D asset) => AssetDatabase.GetAssetPath(asset);

        void Decode(string path)
        {
            _source = null;
            _sourcePath = path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Say("Cannot find " + path + " on disk.", true);
                return;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path)))
            {
                Say("Unity could not decode " + path + ".", true);
                return;
            }
            _source = tex;
            if (_spec != null && _spec.sourceRows > tex.height) _spec.sourceRows = tex.height;
        }

        /// <summary>
        /// Sensible numbers for a picture nobody has measured yet: the crop from
        /// the silhouette, which is exact, and an opening seeded at the same
        /// PROPORTIONS the current case has. Swapping one photograph of a machine
        /// for another usually lands within a few pixels of right, and the overlay
        /// shows you the rest.
        /// </summary>
        void Seed()
        {
            if (_source == null || _spec == null) return;
            MeasureCrop();
            float w = _spec.crop.z - _spec.crop.x, h = _spec.crop.w - _spec.crop.y;
            _spec.glass = new Vector4(
                _spec.crop.x + w * 0.082f, _spec.crop.y + h * 0.059f,
                _spec.crop.z - w * 0.082f, _spec.crop.w - h * 0.061f);
        }

        void MeasureCrop()
        {
            if (_source == null || _spec == null) return;
            int rows = Mathf.Clamp(_spec.sourceRows, 1, _source.height);
            byte[] px = Rgba(_source, rows);
            bool[] inside;
            int x0, y0, x1, y1;
            if (!ChassisCut.Silhouette(px, _source.width, rows, _spec.darkBelow,
                                       out inside, out x0, out y0, out x1, out y1))
            {
                Say("The flood fill found no machine — every pixel is background. " +
                    "Raise the threshold, or the picture is not on black.", true);
                return;
            }
            _spec.crop = new Vector4(x0, y0, x1, y1);
            _spec.artW = x1 - x0;
            _spec.artH = y1 - y0;
            EditorUtility.SetDirty(_spec);
            Say("Silhouette measured: " + (x1 - x0) + " x " + (y1 - y0) + ".", false);
        }

        /// <summary>
        /// Top-down RGBA, which is what ChassisCut wants and the opposite of the
        /// way Unity hands out pixels: GetPixels32 counts rows from the BOTTOM.
        /// One flip here, one flip back on the way out, and nothing in between has
        /// to know.
        /// </summary>
        static byte[] Rgba(Texture2D tex, int rows)
        {
            Color32[] src = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var outp = new byte[w * rows * 4];
            for (int y = 0; y < rows; y++)
            {
                int sy = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = src[sy * w + x];
                    int i = (y * w + x) * 4;
                    outp[i] = c.r; outp[i + 1] = c.g; outp[i + 2] = c.b; outp[i + 3] = c.a;
                }
            }
            return outp;
        }

        // ---- the cut -----------------------------------------------------------

        void Cut(bool write)
        {
            if (_source == null || _spec == null) return;

            int rows = Mathf.Clamp(_spec.sourceRows, 1, _source.height);
            var s = new ChassisCut.Settings
            {
                rows = rows,
                cropX0 = Mathf.RoundToInt(_spec.crop.x),
                cropY0 = Mathf.RoundToInt(_spec.crop.y),
                cropX1 = Mathf.RoundToInt(_spec.crop.z),
                cropY1 = Mathf.RoundToInt(_spec.crop.w),
                glassX0 = _spec.glass.x,
                glassY0 = _spec.glass.y,
                glassX1 = _spec.glass.z,
                glassY1 = _spec.glass.w,
                glassRadius = _spec.glassRadius,
                darkBelow = _spec.darkBelow,
                bleed = 4,
            };

            ChassisCut.Result r = ChassisCut.Run(Rgba(_source, rows), _source.width, rows, s);

            // back into Unity's bottom-up order
            var px = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
            {
                int sy = r.height - 1 - y;
                for (int x = 0; x < r.width; x++)
                {
                    int i = (sy * r.width + x) * 4;
                    px[y * r.width + x] = new Color32(r.rgba[i], r.rgba[i + 1], r.rgba[i + 2], r.rgba[i + 3]);
                }
            }
            _preview = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
            _preview.SetPixels32(px);
            _preview.Apply();

            _spec.artW = r.width;
            _spec.artH = r.height;
            _spec.left = r.left;
            _spec.right = r.right;
            _spec.top = r.top;
            _spec.bottom = r.bottom;
            EditorUtility.SetDirty(_spec);

            if (!write)
            {
                Say("Cut " + r.width + " x " + r.height + ", bezel " +
                    Mathf.RoundToInt(r.left) + "/" + Mathf.RoundToInt(r.right) + "/" +
                    Mathf.RoundToInt(r.top) + "/" + Mathf.RoundToInt(r.bottom) +
                    ". Not written yet.", false);
                return;
            }

            File.WriteAllBytes(ArtPath, ImageConversion.EncodeToPNG(_preview));
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Chassis.Reload();
            Say("Wrote " + ArtPath + " and the spec. " + r.width + " x " + r.height + ".", false);
        }

        void CreateSpec()
        {
            var spec = ScriptableObject.CreateInstance<ChassisSpec>();
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(spec, SpecPath);
            AssetDatabase.SaveAssets();
            _spec = spec;
            Say("Created " + SpecPath + " with the shipped case's measurements.", false);
        }

        void Say(string s, bool bad) { _status = s; _statusBad = bad; }

        // ---- the two previews ---------------------------------------------------

        /// <summary>The source with the crop and the opening drawn on it.</summary>
        void SourceOverlay()
        {
            if (_source == null) return;
            Rect box = GUILayoutUtility.GetRect(10f, 260f);
            Rect fit = Fit(box, _source.width, _source.height);
            EditorGUI.DrawRect(box, new Color(0.06f, 0.06f, 0.07f));
            GUI.DrawTexture(fit, _source, ScaleMode.StretchToFill);

            float sx = fit.width / _source.width, sy = fit.height / _source.height;
            Rect crop = new Rect(fit.x + _spec.crop.x * sx, fit.y + _spec.crop.y * sy,
                                 (_spec.crop.z - _spec.crop.x) * sx, (_spec.crop.w - _spec.crop.y) * sy);
            Outline(crop, new Color(0.77f, 0.41f, 0.21f, 0.9f));

            Rect glass = new Rect(fit.x + _spec.glass.x * sx, fit.y + _spec.glass.y * sy,
                                  (_spec.glass.z - _spec.glass.x) * sx, (_spec.glass.w - _spec.glass.y) * sy);
            Outline(glass, new Color(0.36f, 0.72f, 0.82f, 0.95f));

            // the rows the cut ignores, greyed out, because forgetting to set that
            // is how five rows of white end up in the silhouette
            if (_spec.sourceRows < _source.height)
            {
                float y = fit.y + _spec.sourceRows * sy;
                EditorGUI.DrawRect(new Rect(fit.x, y, fit.width, fit.yMax - y), new Color(0, 0, 0, 0.6f));
            }
        }

        /// <summary>
        /// THE TWELVE PIECES, WHERE THE GAME WILL PUT THEM.
        ///
        /// The same arithmetic Chassis.Fit does, drawn with the cut texture rather
        /// than composed out of Images: eight arms pinned to their corners at a
        /// fixed size, four sides taking whatever is left. If a corner cut lands
        /// on the notch or a band is too shallow to hold the metal in its stretch,
        /// this is where it is visible, and it is visible before anything is
        /// written to disk.
        /// </summary>
        void Composed()
        {
            Texture2D t = _preview != null ? _preview : null;
            Rect box = GUILayoutUtility.GetRect(10f, 300f);
            EditorGUI.DrawRect(box, new Color(0.03f, 0.03f, 0.035f));
            if (t == null)
            {
                EditorGUI.LabelField(box, "  Press Cut to see the case assembled.");
                return;
            }

            float pw = Mathf.Max(1f, _panelW), ph = Mathf.Max(1f, _panelH);
            Rect panel = Fit(box, pw, ph);
            float u = panel.width / pw;                 // canvas units to screen pixels
            float k = _spec.scale * u;                  // art pixels to screen pixels
            float C = _spec.corner, Aw = _spec.armW, Ah = _spec.armH;
            float Sw = _spec.sideW, Bh = _spec.bandH;
            float W = _spec.artW, H = _spec.artH;

            // one window onto the art, placed against a corner of the panel
            void Piece(float sx, float sy, float sw, float sh, int ax, int ay, float ox,
                       float dw, float dh)
            {
                float x = ax == 0 ? panel.x + ox * u : panel.xMax - dw + ox * u;
                float y = ay == 1 ? panel.y : panel.yMax - dh;
                // texCoords count y UP from the bottom of the texture; the art is
                // measured DOWN from the top, which is the one conversion here
                var uv = new Rect(sx / W, (H - sy - sh) / H, sw / W, sh / H);
                GUI.DrawTextureWithTexCoords(new Rect(x, y, dw, dh), t, uv);
            }

            Piece(0f, 0f, Aw, C, 0, 1, 0f, Aw * k, C * k);
            Piece(Aw, 0f, C - Aw, Ah, 0, 1, Aw * _spec.scale, (C - Aw) * k, Ah * k);
            Piece(W - Aw, 0f, Aw, C, 1, 1, 0f, Aw * k, C * k);
            Piece(W - C, 0f, C - Aw, Ah, 1, 1, -Aw * _spec.scale, (C - Aw) * k, Ah * k);
            Piece(0f, H - C, Aw, C, 0, 0, 0f, Aw * k, C * k);
            Piece(Aw, H - Ah, C - Aw, Ah, 0, 0, Aw * _spec.scale, (C - Aw) * k, Ah * k);
            Piece(W - Aw, H - C, Aw, C, 1, 0, 0f, Aw * k, C * k);
            Piece(W - C, H - Ah, C - Aw, Ah, 1, 0, -Aw * _spec.scale, (C - Aw) * k, Ah * k);

            // the four that stretch
            float span = panel.width - C * k * 2f;
            float rise = panel.height - C * k * 2f;
            GUI.DrawTextureWithTexCoords(new Rect(panel.x + C * k, panel.y, span, Bh * k), t,
                new Rect(C / W, (H - Bh) / H, (W - C * 2f) / W, Bh / H));
            GUI.DrawTextureWithTexCoords(new Rect(panel.x + C * k, panel.yMax - Bh * k, span, Bh * k), t,
                new Rect(C / W, 0f, (W - C * 2f) / W, Bh / H));
            GUI.DrawTextureWithTexCoords(new Rect(panel.x, panel.y + C * k, Sw * k, rise), t,
                new Rect(0f, C / H, Sw / W, (H - C * 2f) / H));
            GUI.DrawTextureWithTexCoords(new Rect(panel.xMax - Sw * k, panel.y + C * k, Sw * k, rise), t,
                new Rect((W - Sw) / W, C / H, Sw / W, (H - C * 2f) / H));

            // where the interface is allowed to draw
            Outline(new Rect(panel.x + _spec.left * _spec.scale * u,
                             panel.y + _spec.top * _spec.scale * u,
                             panel.width - (_spec.left + _spec.right) * _spec.scale * u,
                             panel.height - (_spec.top + _spec.bottom) * _spec.scale * u),
                    new Color(0.36f, 0.72f, 0.82f, 0.7f));
        }

        /// <summary>The largest w:h rect that fits in <paramref name="box"/>, centred.</summary>
        static Rect Fit(Rect box, float w, float h)
        {
            float s = Mathf.Min(box.width / w, box.height / h);
            float dw = w * s, dh = h * s;
            return new Rect(box.x + (box.width - dw) * 0.5f, box.y + (box.height - dh) * 0.5f, dw, dh);
        }

        static void Outline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
