using System;
using System.Collections.Generic;
using System.Linq;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Geometry.ElementDrawers;

using SkiaSharp;

// Alias DrawerOptions so the geometry drawer types (which share names with the legacy
// BinaryKits.Zpl.Viewer.ElementDrawers set) resolve unambiguously to the Geometry namespace.
using DrawerOptions = BinaryKits.Zpl.Viewer.ElementDrawers.DrawerOptions;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Geometry-first ZPL renderer over <see cref="SKPath"/>. Builds the label as an ordered display list
    /// (<see cref="SkLabelDrawing"/>) and
    /// replays it onto either a raster <c>SKSurface</c> (PNG) or an <c>SKDocument</c> page (vector PDF).
    /// Because every element is filled geometry, the PDF stays crisp vector — there is no
    /// <c>SKBlendMode.Xor</c> / <c>FixPdfInvertDraw</c> rasterisation.
    ///
    /// <para>Drawers are per-instance (not static), so a renderer instance is self-contained; create one
    /// per render request.</para>
    /// </summary>
    public sealed class SkiaGeometryRenderer
    {
        private readonly IGeometryElementDrawer[] _elementDrawers;
        private readonly DrawerOptions _options;
        private readonly IPrinterStorage _printerStorage;

        public SkiaGeometryRenderer(IPrinterStorage printerStorage, DrawerOptions options = null)
        {
            _printerStorage = printerStorage;
            _options = options ?? new DrawerOptions();
            _elementDrawers = new IGeometryElementDrawer[]
            {
                new GraphicBoxElementDrawer(),
                new GraphicCircleElementDrawer(),
                new GraphicEllipseElementDrawer(),
                new GraphicDiagonalLineElementDrawer(),
                new TextFieldElementDrawer(),
                new FieldBlockElementDrawer(),
                new GraphicSymbolElementDrawer(),
                new Barcode128ElementDrawer(),
                new Barcode39ElementDrawer(),
                new Barcode93ElementDrawer(),
                new BarcodeAnsiCodabarElementDrawer(),
                new Interleaved2of5ElementDrawer(),
                new BarcodeEAN13ElementDrawer(),
                new BarcodeUpcAElementDrawer(),
                new BarcodeUpcEElementDrawer(),
                new BarcodeUpcExtensionElementDrawer(),
                new QrCodeElementDrawer(),
                new DataMatrixElementDrawer(),
                new AztecBarcodeElementDrawer(),
                new Pdf417ElementDrawer(),
                new MaxiCodeElementDrawer(),
                new GraphicFieldElementDrawer(),
                new ImageMoveElementDrawer(),
                new RecallGraphicElementDrawer(),
            };
        }

        /// <summary>
        /// Build the label into a reusable <see cref="SkLabelDrawing"/> — the canonical, surface-agnostic
        /// artifact. Coordinates are in ZPL dots (1 dot = 1 unit).
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimetres</param>
        /// <param name="labelHeight">Label height in millimetres</param>
        /// <param name="printDensityDpmm">Dots per millimetre</param>
        public SkLabelDrawing CreateLabelDrawing(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            (int width, int height) = LabelSize(labelWidth, labelHeight, printDensityDpmm);
            List<SkLabelOp> ops = BuildContent(elements, width, height, printDensityDpmm);
            return new SkLabelDrawing(width, height, ops, _options.OpaqueBackground, _options.Antialias);
        }

        /// <summary>
        /// Rasterise the label to a PNG (or the configured <see cref="DrawerOptions.RenderFormat"/>) byte
        /// array via an <c>SKSurface</c>. <paramref name="scale"/> is an integer supersample factor (≥ 1):
        /// 1 renders at the native dot grid (1 dot = 1 px), higher values render at <c>scale</c>× the pixel
        /// density for a sharper print without changing the on-paper size.
        /// </summary>
        public byte[] DrawPng(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8,
            int scale = 1)
        {
            if (scale < 1)
            {
                scale = 1;
            }

            SkLabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);

            var info = new SKImageInfo(label.Width * scale, label.Height * scale);
            using (SKSurface surface = SKSurface.Create(info))
            {
                SKCanvas canvas = surface.Canvas;
                if (scale != 1)
                {
                    canvas.Scale(scale);
                }

                label.Replay(canvas);
                canvas.Flush();

                using (SKImage image = surface.Snapshot())
                using (SKData data = image.Encode(_options.RenderFormat, _options.RenderQuality))
                {
                    return data.ToArray();
                }
            }
        }

        /// <summary>
        /// Render the label to a <b>vector</b> PDF via <see cref="SKDocument.CreatePdf(System.IO.Stream)"/>.
        /// Every black/white element is a filled <see cref="SKPath"/>, so the PDF records vectors for all of
        /// them — there is no <c>SKBlendMode.Xor</c> / <c>FixPdfInvertDraw</c> rasterisation, and barcodes are
        /// vector modules rather than embedded bitmaps. Only genuine <c>^GF</c>/<c>^XG</c>/<c>^IM</c> raster
        /// images embed as images.
        ///
        /// <para>The page is sized in points (1 pt = 1/72 inch); the dot grid is mapped to points with
        /// <c>72 / (dpmm × 25.4)</c> — computed from the actual density, not hard-coded for 8 dpmm.</para>
        /// </summary>
        public byte[] DrawPdf(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            SkLabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);

            float pageWidthPt = (float)(labelWidth / 25.4 * 72.0);
            float pageHeightPt = (float)(labelHeight / 25.4 * 72.0);
            float dotToPoint = (float)(72.0 / (printDensityDpmm * 25.4));

            using (var ms = new System.IO.MemoryStream())
            {
                using (SKDocument document = SKDocument.CreatePdf(ms))
                {
                    SKCanvas pdfCanvas = document.BeginPage(pageWidthPt, pageHeightPt);
                    pdfCanvas.Scale(dotToPoint);   // dot grid -> points
                    label.Replay(pdfCanvas);
                    document.EndPage();
                    document.Close();
                }

                return ms.ToArray();
            }
        }

        /// <summary>Render both outputs from one built label: <c>[0]</c> PNG, <c>[1]</c> vector PDF.</summary>
        public List<byte[]> DrawMulti(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8,
            int scale = 1)
        {
            // Build once; replay onto each surface. (Two CreateLabelDrawing calls would re-run the drawer
            // pipeline; element drawers are stateless per render, but we keep this explicit and cheap.)
            return new List<byte[]>
            {
                DrawPng(elements, labelWidth, labelHeight, printDensityDpmm, scale),
                DrawPdf(elements, labelWidth, labelHeight, printDensityDpmm),
            };
        }

        private static (int width, int height) LabelSize(double labelWidth, double labelHeight, int printDensityDpmm)
        {
            // Match the existing renderers' Convert.ToInt32 (banker's) rounding so dimensions are identical.
            return (Convert.ToInt32(labelWidth * printDensityDpmm), Convert.ToInt32(labelHeight * printDensityDpmm));
        }

        /// <summary>
        /// Run the drawer pipeline, emitting an ordered display list of fill / image ops. Drawing the
        /// list in document order reproduces ZPL compositing by the painter's algorithm: additive black
        /// unions visually (no boolean geometry, no winding cancellation), white over black erases,
        /// images layer in order.
        ///
        /// <para>Field Reverse (<c>^FR</c>) is the one op that needs geometry boolean math, because a
        /// canvas can't raster-XOR into a vector PDF: a reverse field XORs against everything painted
        /// under it. We reproduce that as two paint ops — erase (white) where the field overlaps the
        /// visible black, and add (black) where it doesn't. A reverse only interacts with the black
        /// pieces it actually overlaps, so rather than compose against one ever-growing visible-black
        /// geometry (which made a reverse-heavy label quadratic), we keep the black-modifying ops as a
        /// list with bounds and compose only the few that overlap the field's bounding box. A piece
        /// disjoint from the field contributes nothing to the intersection or the subtraction, so the
        /// bounding-box filter is exact, not an approximation.</para>
        /// </summary>
        private List<SkLabelOp> BuildContent(
            IEnumerable<ZplElementBase> elements, int width, int height, int printDensityDpmm)
        {
            var context = new SkDrawContext(width, height);
            var ops = new List<SkLabelOp>();

            // Ordered black-compositing ops (Union = black fill, Difference = white fill, Xor = reverse),
            // each with its bounds. Built lazily the first time a reverse needs it; null until then, so
            // labels without ^FR pay nothing.
            List<BlackOp> blackOps = null;

            InternationalFont internationalFont = InternationalFont.ZCP850;
            SKPoint currentDefaultPosition = new SKPoint(0, 0);

            foreach (ZplElementBase element in elements)
            {
                if (element is ZplChangeInternationalFont changeFont)
                {
                    internationalFont = changeFont.InternationalFont;
                    continue;
                }

                IGeometryElementDrawer drawer = _elementDrawers.SingleOrDefault(o => o.CanDraw(element));
                if (drawer == null)
                {
                    continue;
                }

                try
                {
                    drawer.Prepare(_printerStorage, context);
                    currentDefaultPosition = drawer.Draw(element, _options, currentDefaultPosition, internationalFont, printDensityDpmm);

                    SKPath black = context.TakeBlack();
                    SKPath white = context.TakeWhite();

                    if (drawer.IsReverseDraw(element))
                    {
                        // ^FR: XOR the field against the visible black under it. A white-drawn reverse is
                        // normalised to black before the XOR (Skia's InvertDrawWhite), so white vs black
                        // reverse have the identical effect here — both arrive via `black`.
                        if (black != null)
                        {
                            blackOps = blackOps ?? BuildBlackOps(ops);

                            SKRect fieldBounds = black.Bounds;
                            SKPath localBlack = ComposeOverlapping(blackOps, fieldBounds);
                            if (localBlack == null)
                            {
                                ops.Add(SkLabelOp.Black(black));     // nothing underneath: reverse is all black
                            }
                            else
                            {
                                SKPath erase = black.Op(localBlack, SKPathOp.Intersect);  // erase where field overlaps black
                                SKPath add = black.Op(localBlack, SKPathOp.Difference);   // add black where it doesn't
                                if (erase != null && !erase.IsEmpty)
                                {
                                    ops.Add(SkLabelOp.WhiteFill(erase));
                                }

                                if (add != null && !add.IsEmpty)
                                {
                                    ops.Add(SkLabelOp.Black(add));
                                }
                            }

                            blackOps.Add(new BlackOp(black, SKPathOp.Xor));
                        }
                    }
                    else
                    {
                        if (black != null)
                        {
                            ops.Add(SkLabelOp.Black(black));
                            blackOps?.Add(new BlackOp(black, SKPathOp.Union));
                        }

                        if (white != null)
                        {
                            ops.Add(SkLabelOp.WhiteFill(white));
                            blackOps?.Add(new BlackOp(white, SKPathOp.Difference));
                        }
                    }

                    foreach (SkImageOp image in context.TakeImages())
                    {
                        ops.Add(SkLabelOp.Img(image));
                    }
                }
                catch (Exception ex)
                {
                    throw DescribeElementError(element, ex);
                }
            }

            return ops;
        }

        /// <summary>A black-compositing op kept for resolving <c>^FR</c>: a geometry, how it modifies the
        /// black region, and its bounds (cached so the reverse path can bounding-box-filter cheaply).</summary>
        private readonly struct BlackOp
        {
            public readonly SKPath Geom;
            public readonly SKPathOp Mode;   // Union (black), Difference (white), Xor (reverse)
            public readonly SKRect Bounds;

            public BlackOp(SKPath geom, SKPathOp mode)
            {
                this.Geom = geom;
                this.Mode = mode;
                this.Bounds = geom.Bounds;
            }
        }

        /// <summary>Seed the black-op list from the fill ops emitted so far (black = Union, white = Difference).</summary>
        private static List<BlackOp> BuildBlackOps(List<SkLabelOp> ops)
        {
            var list = new List<BlackOp>(ops.Count);
            foreach (SkLabelOp op in ops)
            {
                if (!op.IsImage)
                {
                    list.Add(new BlackOp(op.Fill, op.White ? SKPathOp.Difference : SKPathOp.Union));
                }
            }

            return list;
        }

        /// <summary>
        /// Compose the visible-black region restricted to <paramref name="bounds"/>, combining only the
        /// black ops whose bounds overlap it. Ops outside <paramref name="bounds"/> can't intersect a
        /// field there, so skipping them is exact; it keeps each reverse's boolean work proportional to
        /// the few pieces it actually overlaps instead of the whole accumulated black.
        /// </summary>
        private static SKPath ComposeOverlapping(List<BlackOp> blackOps, SKRect bounds)
        {
            SKPath local = null;
            foreach (BlackOp op in blackOps)
            {
                if (!Overlaps(op.Bounds, bounds))
                {
                    continue;
                }

                switch (op.Mode)
                {
                    case SKPathOp.Difference:
                        local = Exclude(local, op.Geom);
                        break;
                    case SKPathOp.Xor:
                        local = Xor(local, op.Geom);
                        break;
                    default:
                        local = Union(local, op.Geom);
                        break;
                }
            }

            return local;
        }

        /// <summary>Whether two axis-aligned rectangles overlap (strict — touching edges don't count).</summary>
        private static bool Overlaps(SKRect a, SKRect b)
            => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

        // Boolean helpers — used only on the ^FR path (the additive pipeline paints instead).
        private static SKPath Union(SKPath a, SKPath b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return a.Op(b, SKPathOp.Union);
        }

        private static SKPath Xor(SKPath a, SKPath b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return a.Op(b, SKPathOp.Xor);
        }

        private static SKPath Exclude(SKPath a, SKPath b)
        {
            if (a == null) return null;
            if (b == null) return a;
            return a.Op(b, SKPathOp.Difference);
        }

        private static Exception DescribeElementError(ZplElementBase element, Exception ex)
        {
            switch (element)
            {
                case ZplBarcode barcode:
                    return new Exception($"Error on zpl element \"{barcode.Content}\": {ex.Message}", ex);
                case ZplDataMatrix dataMatrix:
                    return new Exception($"Error on zpl element \"{dataMatrix.Content}\": {ex.Message}", ex);
                default:
                    return ex;
            }
        }
    }
}
