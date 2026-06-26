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
        /// single net inked region (<see cref="BuildNetInk"/>) with the ribbon colour. Knockouts, gaps and
        /// reverse holes are simply never inked, so they reveal the background — a true (vector) hole when it is
        /// transparent, the stock colour when it is opaque. The painter's-order add/subtract/reverse compositing
        /// is pre-folded into the net ink, so there is no separate "paint white over black" pass.
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

            // The single net inked region, filled with the ribbon colour. Each segment keeps its own fill type
            // so SKPath.Op's hole encoding is preserved (never force Winding here, or reverse holes fill back in).
            using (var paint = new SKPaint { Color = settings.Ribbon, IsAntialias = settings.Antialias, Style = SKPaintStyle.Fill })
            {
                foreach (SKPath segment in BuildNetInk())
                {
                    if (!segment.IsEmpty)
                    {
                        canvas.DrawPath(segment, paint);
                    }

                    segment.Dispose();
                }
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
        /// Fold the geometry ops into the net inked region, returned as ordered paths to draw. Ink is kept at
        /// per-element granularity: each ink op is a piece (its <c>op.Fill</c>, not owned). A white/reverse op is
        /// <em>deferred</em> — appended (cheap <c>AddPath</c>) into the knockout accumulator of only the pieces
        /// whose bounding box it overlaps. At the end, every piece a knockout touched pays a single
        /// <see cref="SKPathOp.Difference"/> against its batched knockouts (since <c>A−w1−w2 = A−(w1∪w2)</c>) and
        /// is returned in document order; all untouched pieces merge into one seamless additive path returned
        /// last. So the boolean work is proportional to the few <em>knocked-out</em> elements, not the whole
        /// label, and a purely additive label (e.g. a barcode) pays no boolean cost. Caller owns the paths.
        ///
        /// <para>Drawing untouched ink last is safe: a piece a knockout never touched cannot overlap that
        /// knockout's hole (it would have been subtracted), so it only ever overlaps solid ink (order-neutral)
        /// or re-fills a hole it legitimately post-dates.</para>
        /// </summary>
        private List<SKPath> BuildNetInk()
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

            var result = new List<SKPath>();
            SKPath additive = null;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieceKnockout[i] == null)
                {
                    additive ??= new SKPath { FillType = SKPathFillType.Winding };
                    additive.AddPath(pieces[i]);
                }
                else
                {
                    SKPath diff = pieces[i].Op(pieceKnockout[i], SKPathOp.Difference);
                    result.Add(diff ?? new SKPath(pieces[i]));   // owned (Op result, or a copy if Op failed)
                    pieceKnockout[i].Dispose();
                }
            }

            if (additive != null)
            {
                result.Add(additive);
            }

            return result;
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
