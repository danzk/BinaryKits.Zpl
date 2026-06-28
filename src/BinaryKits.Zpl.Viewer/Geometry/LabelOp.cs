using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// One entry in a <see cref="LabelDrawing"/>'s display list: either a geometry fill (black or
    /// white) or an image draw. Rendered in document order, which reproduces ZPL compositing by the
    /// painter's algorithm — black over black unions visually, white over black erases, images layer in
    /// order — with no boolean geometry ops.
    /// </summary>
    public readonly struct LabelOp
    {
        private readonly SKPath _fill;   // eager geometry; null for a text op (built on demand) or an image op

        /// <summary>Fill colour for a geometry op: <c>true</c> white, <c>false</c> black.</summary>
        public bool White { get; }

        /// <summary>The image to draw; valid only for an image op (<see cref="IsImage"/>).</summary>
        public ImageOp Image { get; }

        /// <summary>
        /// Non-<c>null</c> marks a text op: an ink (black) run drawn with the glyph blitter (<c>DrawText</c>) so
        /// it stays real text in a PDF/SVG — unless a reverse field knocks it out, in which case it falls back to
        /// filling its <see cref="Fill"/> outline (materialised on demand).
        /// </summary>
        public TextRun Text { get; }

        private LabelOp(SKPath fill, bool white, ImageOp image, TextRun text)
        {
            this._fill = fill;
            this.White = white;
            this.Image = image;
            this.Text = text;
        }

        /// <summary>Fill geometry. For a text op this materialises the run's outline on demand (only happens when
        /// the text is actually knocked out); <c>null</c> only for an image op.</summary>
        public SKPath Fill => this._fill ?? this.Text?.GetPath();

        /// <summary>Cheap bounding box for the knockout/overlap filter — never builds a text op's outline.</summary>
        public SKRect Bounds => this.Text != null ? this.Text.Bounds : (this._fill?.Bounds ?? SKRect.Empty);

        public bool IsImage => this._fill == null && this.Text == null;
        public bool IsText => this.Text != null;

        public static LabelOp Black(SKPath fill) => new LabelOp(fill, false, default, null);
        public static LabelOp WhiteFill(SKPath fill) => new LabelOp(fill, true, default, null);
        public static LabelOp Img(ImageOp image) => new LabelOp(null, false, image, null);
        public static LabelOp TextOp(TextRun text) => new LabelOp(null, false, default, text);
    }
}
