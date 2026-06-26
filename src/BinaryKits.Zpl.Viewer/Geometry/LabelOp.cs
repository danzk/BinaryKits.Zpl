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
        /// <summary>Fill geometry; <c>null</c> marks an image op (see <see cref="Image"/>).</summary>
        public SKPath Fill { get; }

        /// <summary>Fill colour for a geometry op: <c>true</c> white, <c>false</c> black.</summary>
        public bool White { get; }

        /// <summary>The image to draw; valid only when <see cref="Fill"/> is <c>null</c>.</summary>
        public ImageOp Image { get; }

        private LabelOp(SKPath fill, bool white, ImageOp image)
        {
            this.Fill = fill;
            this.White = white;
            this.Image = image;
        }

        public bool IsImage => this.Fill == null;

        public static LabelOp Black(SKPath fill) => new LabelOp(fill, false, default);
        public static LabelOp WhiteFill(SKPath fill) => new LabelOp(fill, true, default);
        public static LabelOp Img(ImageOp image) => new LabelOp(null, false, image);
    }
}
