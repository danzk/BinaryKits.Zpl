using System.Collections.Generic;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// A label baked down to an ordered, <b>appearance-agnostic</b> display list of fill / image ops plus
    /// the label's pixel dimensions (in ZPL dots; 1 dot = 1 unit). The list records only geometry and an
    /// add/subtract tag (<c>White=false</c> = ink/add, <c>White=true</c> = media/subtract) — no colours and
    /// no mode. Colour and mode are supplied to <see cref="Render"/> via <see cref="RenderSettings"/>, so one
    /// built artifact renders onto any <see cref="SKCanvas"/> — a raster <c>SKSurface</c> (PNG) or an
    /// <c>SKDocument</c> page (vector PDF) — in any appearance, without rebuilding.
    /// </summary>
    public sealed class SkLabelDrawing
    {
        /// <summary>Label width in dots (1 dot = 1 unit).</summary>
        public int Width { get; }

        /// <summary>Label height in dots.</summary>
        public int Height { get; }

        private readonly IReadOnlyList<SkLabelOp> _ops;

        internal SkLabelDrawing(int width, int height, IReadOnlyList<SkLabelOp> ops)
        {
            this.Width = width;
            this.Height = height;
            _ops = ops;
        }

        /// <summary>
        /// Render the display list onto <paramref name="canvas"/> with the given appearance. An opaque stock
        /// uses the additive painter's algorithm (ink/media painted in document order); a transparent stock
        /// folds the ops into the single net-inked region and fills only that, leaving everything else a true
        /// hole (genuinely vector-transparent — works on an <c>SKDocument</c> PDF page).
        /// </summary>
        /// <summary>
        /// Render the label. A single path covers both opaque and transparent stock: clear the background to the
        /// stock/media colour when opaque (or to transparent when the background is not opaque, e.g. transparent
        /// stock), draw any raster graphics tinted so their media falls through to that background, then fill the
        /// net inked region (<see cref="DrawNetInk"/>) with the ribbon colour. Knockouts, gaps and reverse holes
        /// are simply never inked, so they reveal the background — a true (vector) hole when it is transparent,
        /// the stock colour when it is opaque. The painter's-order add/subtract/reverse compositing is folded
        /// into the net ink, so there is no separate "paint white over black" pass.
        /// </summary>
        public void Render(SKCanvas canvas, in RenderSettings settings)
        {
            canvas.Clear(settings.OpaqueBackground ? settings.Stock : SKColors.Transparent);

            // Raster graphics: tint ink → ribbon and map the media to alpha 0, so unprinted areas reveal the
            // background (the stock colour when opaque, a hole when transparent). Drawn under the geometry ink;
            // white / reverse fields subtract from geometry only, not from embedded images.
            using (SKPaint imagePaint = BuildImagePaint(settings))
            {
                foreach (SkLabelOp op in _ops)
                {
                    if (op.IsImage)
                    {
                        DrawImage(canvas, op.Image, imagePaint);
                    }
                }
            }

            using (var paint = new SKPaint { Color = settings.Ribbon, IsAntialias = settings.Antialias, Style = SKPaintStyle.Fill })
            {
                DrawNetInk(canvas, paint);
            }
        }

        private static void DrawImage(SKCanvas canvas, SkImageOp img, SKPaint paint)
        {
            if (img.Transform.IsIdentity)
            {
                canvas.DrawImage(img.Image, img.Destination, paint);
            }
            else
            {
                canvas.Save();
                SKMatrix m = img.Transform;
                canvas.Concat(in m);
                canvas.DrawImage(img.Image, img.Destination, paint);
                canvas.Restore();
            }
        }

        /// <summary>
        /// Fill the net inked region onto <paramref name="canvas"/> in document order. Each ink op is drawn as
        /// its <b>own</b> path, so overlapping ink from different ops composites by the painter's algorithm
        /// (opaque ribbon over ribbon) and never winding-cancels — unlike unioning everything into one path,
        /// where opposite-wound contours (e.g. text glyphs printed over barcode modules or a MaxiCode) would
        /// punch false white holes.
        ///
        /// <para>A white/reverse op is <em>deferred</em>: it accumulates (cheap <c>AddPath</c>) into the knockout
        /// of only the earlier ink pieces whose bounding box it overlaps. Each knocked-out piece then pays a
        /// single <see cref="SKPathOp.Difference"/> against its batched knockouts (since <c>A−w1−w2 = A−(w1∪w2)</c>)
        /// before being drawn; a later piece re-inks an earlier knockout, matching painter's order. Untouched
        /// ink draws straight from the op (no copy, no boolean) — only knocked-out ink allocates a temporary, so
        /// the boolean work is proportional to the few knocked-out elements, and a purely additive label (e.g. a
        /// barcode) pays none.</para>
        /// </summary>
        private void DrawNetInk(SKCanvas canvas, SKPaint paint)
        {
            var pieces = new List<SKPath>();        // op.Fill references (NOT owned)
            var pieceBounds = new List<SKRect>();
            var pieceKnockout = new List<SKPath>(); // owned accumulators, null until a knockout overlaps

            foreach (SkLabelOp op in _ops)
            {
                if (op.IsImage)
                {
                    continue;
                }

                if (op.White)
                {
                    SKRect whiteBounds = op.Fill.Bounds;
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        if (!Overlaps(pieceBounds[i], whiteBounds))
                        {
                            continue;
                        }

                        if (pieceKnockout[i] == null)
                        {
                            pieceKnockout[i] = new SKPath { FillType = SKPathFillType.Winding };
                        }

                        pieceKnockout[i].AddPath(op.Fill);
                    }
                    // white over nothing: no overlapping piece, nothing to subtract (transparent there anyway)
                }
                else
                {
                    pieces.Add(op.Fill);
                    pieceBounds.Add(op.Fill.Bounds);
                    pieceKnockout.Add(null);
                }
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieceKnockout[i] == null)
                {
                    if (!pieces[i].IsEmpty)
                    {
                        canvas.DrawPath(pieces[i], paint);   // op.Fill, drawn straight (not owned, not disposed)
                    }
                }
                else
                {
                    using (SKPath diff = pieces[i].Op(pieceKnockout[i], SKPathOp.Difference))
                    {
                        SKPath inked = diff ?? pieces[i];    // null Op result → fall back to the un-cut piece
                        if (!inked.IsEmpty)
                        {
                            canvas.DrawPath(inked, paint);
                        }
                    }

                    pieceKnockout[i].Dispose();
                }
            }
        }

        /// <summary>Whether two axis-aligned rectangles overlap (strict — touching edges don't count).</summary>
        private static bool Overlaps(SKRect a, SKRect b)
            => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

        /// <summary>
        /// Paint for raster graphics: tint the ink to the ribbon colour and map the media (white) to alpha 0, so
        /// the unprinted areas of a monochrome graphic reveal the page background — the stock colour when the
        /// background is opaque, a genuine hole when it is transparent. Output alpha = input alpha − luminance
        /// (black/opaque → opaque ribbon, white → transparent). For the default black-on-white graphic over a
        /// white background this is identity (black stays black; white maps to transparent over white = white).
        /// </summary>
        private static SKPaint BuildImagePaint(in RenderSettings settings)
        {
            float rr = settings.Ribbon.Red / 255f, rg = settings.Ribbon.Green / 255f, rb = settings.Ribbon.Blue / 255f;

            float[] matrix =
            {
                0,        0,        0,        0, rr,
                0,        0,        0,        0, rg,
                0,        0,        0,        0, rb,
                -0.299f,  -0.587f,  -0.114f,  1, 0,
            };

            return new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(matrix), IsAntialias = settings.Antialias };
        }
    }
}
