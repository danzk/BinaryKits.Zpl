using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// A run of text to paint with the glyph blitter (<c>SKCanvas.DrawText</c>) rather than as filled outline
    /// geometry — so it stays <b>real, selectable text</b> in an exported PDF/SVG (and matches the legacy raster
    /// weight on a PNG). Carries everything <c>DrawText</c> needs: the string, the resolved typeface and size,
    /// the independent horizontal scale (the <c>^A</c> width, applied via <see cref="SKFont.ScaleX"/>), the
    /// baseline origin, and the transform (rotation) in effect when it was added.
    ///
    /// <para>Its companion outline path is kept on the owning <see cref="LabelOp"/>, so the same run can still be
    /// rendered as geometry and knocked out when a reverse field actually intersects it.</para>
    /// </summary>
    public sealed class TextRun
    {
        private SKPath _path;       // outline, built on demand (only when actually knocked out)
        private SKRect? _bounds;    // cheap measured bounds, for the knockout overlap filter

        public TextRun(string text, SKTypeface typeface, float size, float scaleX, SKPoint baseline)
        {
            this.Text = text;
            this.Typeface = typeface;
            this.Size = size;
            this.ScaleX = scaleX;
            this.Baseline = baseline;
            this.Transform = SKMatrix.CreateIdentity();
        }

        public string Text { get; }
        public SKTypeface Typeface { get; }
        public float Size { get; }
        public float ScaleX { get; }

        /// <summary>Baseline origin in the run's local (pre-transform) space.</summary>
        public SKPoint Baseline { get; }

        /// <summary>The transform (e.g. field rotation) in effect when the run was added; set by <c>DrawContext</c>.</summary>
        public SKMatrix Transform { get; internal set; }

        /// <summary>
        /// The run's outline geometry, in label space — built on demand and cached. Only ever materialised when
        /// the run is actually knocked out by a reverse field; text that prints normally is drawn with the glyph
        /// blitter and never builds this.
        /// </summary>
        public SKPath GetPath()
        {
            if (_path == null)
            {
                using (var font = new SKFont(this.Typeface, this.Size))
                {
                    SKPath outline = TextRenderer.BuildGeometryGlyphRun(font, this.Text, this.ScaleX, this.Baseline);
                    if (!this.Transform.IsIdentity)
                    {
                        outline.Transform(this.Transform);
                    }

                    _path = outline;
                }
            }

            return _path;
        }

        /// <summary>
        /// Cheap label-space bounds (font metrics, not the full outline), cached. Conservative — at least the ink
        /// extent — so it is safe as the bounding-box filter for whether a reverse/white field knocks this run out.
        /// </summary>
        public SKRect Bounds
        {
            get
            {
                if (_bounds == null)
                {
                    using (var font = new SKFont(this.Typeface, this.Size) { ScaleX = this.ScaleX })
                    {
                        font.MeasureText(this.Text, out SKRect r);
                        r.Offset(this.Baseline.X, this.Baseline.Y);
                        _bounds = this.Transform.IsIdentity ? r : this.Transform.MapRect(r);
                    }
                }

                return _bounds.Value;
            }
        }
    }
}
