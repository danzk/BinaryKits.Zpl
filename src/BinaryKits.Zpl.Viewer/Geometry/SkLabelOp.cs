using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// One entry in a <see cref="SkLabelDrawing"/>'s display list: either a geometry fill (black or
    /// white) or an image draw. Replayed in document order, which reproduces ZPL compositing by the
    /// painter's algorithm — black over black unions visually, white over black erases, images layer in
    /// order — with no boolean geometry ops.
    /// </summary>
    public readonly struct SkLabelOp
    {
        /// <summary>Fill geometry; <c>null</c> marks an image op (see <see cref="Image"/>).</summary>
        public SKPath Fill { get; }

        /// <summary>Fill colour for a geometry op: <c>true</c> white, <c>false</c> black.</summary>
        public bool White { get; }

        /// <summary>The image to draw; valid only when <see cref="Fill"/> is <c>null</c>.</summary>
        public SkImageOp Image { get; }

        private SkLabelOp(SKPath fill, bool white, SkImageOp image)
        {
            this.Fill = fill;
            this.White = white;
            this.Image = image;
        }

        public bool IsImage => this.Fill == null;

        public static SkLabelOp Black(SKPath fill) => new SkLabelOp(fill, false, default);
        public static SkLabelOp WhiteFill(SKPath fill) => new SkLabelOp(fill, true, default);
        public static SkLabelOp Img(SkImageOp image) => new SkLabelOp(null, false, image);
    }
}
