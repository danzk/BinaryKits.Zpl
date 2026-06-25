using System.Collections.Generic;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// A label baked down to an ordered display list of fill / image ops plus the label's pixel
    /// dimensions (in ZPL dots; 1 dot = 1 unit). Pure data, decoupled from the output surface, so the
    /// same built artifact replays onto any
    /// <see cref="SKCanvas"/> — a raster <c>SKSurface</c> (PNG) or an <c>SKDocument</c> page (vector PDF).
    ///
    /// <para><see cref="Replay"/> draws the ops in document order — the painter's algorithm — so
    /// overlapping additive elements simply union visually (no boolean geometry, no fill-rule winding
    /// cancellation), and white-over-black / image layering follow from draw order.</para>
    /// </summary>
    public sealed class SkLabelDrawing
    {
        /// <summary>Label width in dots (1 dot = 1 unit).</summary>
        public int Width { get; }

        /// <summary>Label height in dots.</summary>
        public int Height { get; }

        private readonly IReadOnlyList<SkLabelOp> _ops;
        private readonly bool _opaqueBackground;
        private readonly bool _antialias;

        internal SkLabelDrawing(int width, int height, IReadOnlyList<SkLabelOp> ops, bool opaqueBackground, bool antialias)
        {
            this.Width = width;
            this.Height = height;
            _ops = ops;
            _opaqueBackground = opaqueBackground;
            _antialias = antialias;
        }

        /// <summary>
        /// Replay the display list onto <paramref name="canvas"/>. The canvas may back a raster surface or
        /// an <c>SKDocument</c> PDF page; because every black/white element is a filled <see cref="SKPath"/>,
        /// a PDF page records vectors for all of them (only genuine <c>^GF</c>/<c>^XG</c>/<c>^IM</c> raster
        /// images embed as images, the same as the legacy renderer).
        /// </summary>
        public void Replay(SKCanvas canvas)
        {
            // Always clear the whole label so the rendered region is the full label (white when opaque;
            // otherwise transparent — content is unaffected either way).
            canvas.Clear(_opaqueBackground ? SKColors.White : SKColors.Transparent);

            using (var black = new SKPaint { Color = SKColors.Black, IsAntialias = _antialias, Style = SKPaintStyle.Fill })
            using (var white = new SKPaint { Color = SKColors.White, IsAntialias = _antialias, Style = SKPaintStyle.Fill })
            {
                foreach (SkLabelOp op in _ops)
                {
                    if (op.IsImage)
                    {
                        SkImageOp img = op.Image;
                        if (img.Transform.IsIdentity)
                        {
                            canvas.DrawImage(img.Image, img.Destination);
                        }
                        else
                        {
                            canvas.Save();
                            SKMatrix m = img.Transform;
                            canvas.Concat(in m);
                            canvas.DrawImage(img.Image, img.Destination);
                            canvas.Restore();
                        }
                    }
                    else
                    {
                        op.Fill.FillType = SKPathFillType.Winding;
                        canvas.DrawPath(op.Fill, op.White ? white : black);
                    }
                }
            }
        }
    }
}
