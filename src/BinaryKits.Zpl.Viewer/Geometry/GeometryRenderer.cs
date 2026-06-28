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
    /// (<see cref="LabelDrawing"/>) and
    /// renders it onto either a raster <c>SKSurface</c> (PNG) or an <c>SKDocument</c> page (vector PDF).
    /// Because every element is filled geometry, the PDF stays crisp vector — there is no
    /// <c>SKBlendMode.Xor</c> / <c>FixPdfInvertDraw</c> rasterisation.
    ///
    /// <para>Drawers are per-instance (not static), so a renderer instance is self-contained; create one
    /// per render request.</para>
    /// </summary>
    public sealed class GeometryRenderer
    {
        private readonly IGeometryElementDrawer[] _elementDrawers;
        private readonly DrawerOptions _options;
        private readonly IPrinterStorage _printerStorage;

        public GeometryRenderer(IPrinterStorage printerStorage, DrawerOptions options = null)
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
        /// Build the label into a reusable <see cref="LabelDrawing"/> — the canonical, surface-agnostic
        /// artifact. Coordinates are in ZPL dots (1 dot = 1 unit).
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimetres</param>
        /// <param name="labelHeight">Label height in millimetres</param>
        /// <param name="printDensityDpmm">Dots per millimetre</param>
        public LabelDrawing CreateLabelDrawing(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            (int width, int height) = LabelSize(labelWidth, labelHeight, printDensityDpmm);
            List<LabelOp> ops = BuildContent(elements, width, height, printDensityDpmm);
            return new LabelDrawing(width, height, ops);
        }

        /// <summary>The appearance/mode for rendering, derived from the drawer options.</summary>
        private RenderSettings BuildRenderSettings()
            => new RenderSettings(_options.RibbonColor, _options.LabelColor, _options.OpaqueBackground, _options.Antialias);

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

            LabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);

            var info = new SKImageInfo(label.Width * scale, label.Height * scale);
            using (SKSurface surface = SKSurface.Create(info))
            {
                SKCanvas canvas = surface.Canvas;
                if (scale != 1)
                {
                    canvas.Scale(scale);
                }

                label.Render(canvas, BuildRenderSettings());
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
            LabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);

            float pageWidthPt = (float)(labelWidth / 25.4 * 72.0);
            float pageHeightPt = (float)(labelHeight / 25.4 * 72.0);
            float dotToPoint = (float)(72.0 / (printDensityDpmm * 25.4));

            using (var ms = new System.IO.MemoryStream())
            {
                using (SKDocument document = SKDocument.CreatePdf(ms))
                {
                    SKCanvas pdfCanvas = document.BeginPage(pageWidthPt, pageHeightPt);
                    pdfCanvas.Scale(dotToPoint);   // dot grid -> points
                    label.Render(pdfCanvas, BuildRenderSettings());
                    document.EndPage();
                    document.Close();
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Render the label to a <b>vector</b> SVG via <see cref="SKSvgCanvas"/>, reusing the same built
        /// op-list and unified render path as <see cref="DrawPng"/> / <see cref="DrawPdf"/>. Every black/white
        /// element — text included, since glyphs are outline paths — serialises as a filled <c>&lt;path&gt;</c>,
        /// so the SVG is fully scalable; only genuine <c>^GF</c>/<c>^XG</c>/<c>^IM</c> raster images embed (as
        /// <c>&lt;image&gt;</c> data URIs). <c>^FR</c> reverse holes are filled-path geometry (no blend mode),
        /// so they survive as real vector cut-outs.
        ///
        /// <para>Coordinates are the dot grid (1 dot = 1 SVG user unit), so the document's width/height/viewBox
        /// are in dots. Scale on the consumer side for a physical print size.</para>
        /// </summary>
        public byte[] DrawSvg(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            LabelDrawing label = CreateLabelDrawing(elements, labelWidth, labelHeight, printDensityDpmm);

            using (var stream = new SKDynamicMemoryWStream())
            {
                // The SVG is finalised (closing </svg> written) when the canvas is disposed, so read the
                // stream only after the inner using block closes it.
                using (SKCanvas canvas = SKSvgCanvas.Create(SKRect.Create(label.Width, label.Height), stream))
                {
                    label.Render(canvas, BuildRenderSettings());
                }

                using (SKData data = stream.DetachAsData())
                {
                    return PostProcessSvg(data.ToArray(), label.Width, label.Height);
                }
            }
        }

        /// <summary>
        /// Fix up the raw SVG from <see cref="SKSvgCanvas"/>:
        /// <list type="number">
        ///   <item>Add a <c>viewBox</c> spanning the dot grid — Skia emits <c>width</c>/<c>height</c> only, so the
        ///   document has a fixed pixel size and won't scale to its container; the viewBox lets consumers scale it
        ///   freely while preserving the coordinate system and aspect ratio.</item>
        ///   <item>Append a CSS generic fallback to every <c>&lt;text&gt;</c> <c>font-family</c>. Skia writes only
        ///   the single resolved family (e.g. <c>TeX Gyre Heros Cn</c>), with no fallback chain, so a viewer that
        ///   lacks that exact font drops to its default <i>serif</i>. We append <c>sans-serif</c> (or
        ///   <c>monospace</c> for fixed-pitch families) so ZPL text always falls back to the right kind of face.</item>
        /// </list>
        /// </summary>
        private static byte[] PostProcessSvg(byte[] svgBytes, int width, int height)
        {
            string svg = System.Text.Encoding.UTF8.GetString(svgBytes);

            int open = svg.IndexOf("<svg", StringComparison.Ordinal);
            if (open >= 0)
            {
                int close = svg.IndexOf('>', open);
                if (close >= 0 && svg.IndexOf("viewBox", open, close - open, StringComparison.Ordinal) < 0)
                {
                    svg = svg.Insert(close, $" viewBox=\"0 0 {width} {height}\"");
                }
            }

            svg = System.Text.RegularExpressions.Regex.Replace(svg, "font-family=\"([^\"]*)\"", AppendGenericFontFallback);
            return System.Text.Encoding.UTF8.GetBytes(svg);
        }

        /// <summary>Append a CSS generic family (and a couple of common named faces) to an SVG <c>font-family</c>
        /// value, unless it already ends in a generic. Fixed-pitch families get <c>monospace</c>; everything else
        /// gets <c>sans-serif</c> (ZPL's built-in fonts are a Helvetica-like proportional face and a monospace face,
        /// never serif).</summary>
        private static string AppendGenericFontFallback(System.Text.RegularExpressions.Match match)
        {
            string family = match.Groups[1].Value;
            string trimmed = family.TrimEnd();
            if (trimmed.EndsWith("serif", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("monospace", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("cursive", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("fantasy", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;   // already ends in a CSS generic — leave it
            }

            bool monospace = family.IndexOf("mono", StringComparison.OrdinalIgnoreCase) >= 0
                || family.IndexOf("console", StringComparison.OrdinalIgnoreCase) >= 0
                || family.IndexOf("courier", StringComparison.OrdinalIgnoreCase) >= 0;

            string fallback = monospace ? ", 'Courier New', monospace" : ", Helvetica, Arial, sans-serif";
            return $"font-family=\"{family}{fallback}\"";
        }

        /// <summary>Render both outputs from one built label: <c>[0]</c> PNG, <c>[1]</c> vector PDF.</summary>
        public List<byte[]> DrawMulti(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8,
            int scale = 1)
        {
            // Build once; render onto each surface. (Two CreateLabelDrawing calls would re-run the drawer
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
        private List<LabelOp> BuildContent(
            IEnumerable<ZplElementBase> elements, int width, int height, int printDensityDpmm)
        {
            var context = new DrawContext(width, height);
            var ops = new List<LabelOp>();

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
                    IReadOnlyList<TextRun> texts = context.TakeText();

                    if (drawer.IsReverseDraw(element))
                    {
                        // ^FR: XOR the field against the visible black under it. The field geometry is the
                        // element's whole ink — its black geometry plus any text outlines. A white-drawn reverse
                        // is normalised to black before the XOR (Skia's InvertDrawWhite), so white vs black
                        // reverse have the identical effect here.
                        SKPath field = CombineReverseField(black, texts);
                        if (field != null)
                        {
                            blackOps = blackOps ?? BuildBlackOps(ops);

                            SKPath localBlack = ComposeOverlapping(blackOps, field.Bounds);
                            if (localBlack == null)
                            {
                                // Nothing underneath: the reverse is just black (XOR over white), so it causes no
                                // knockout — keep text as real text, and emit any non-text geometry plainly.
                                if (black != null)
                                {
                                    ops.Add(LabelOp.Black(black));
                                }

                                foreach (TextRun t in texts)
                                {
                                    ops.Add(LabelOp.TextOp(t));
                                }
                            }
                            else
                            {
                                // Real knockout: erase where the field overlaps black, add black where it doesn't.
                                // Text caught here is rendered as geometry (it is genuinely knocked out).
                                SKPath erase = field.Op(localBlack, SKPathOp.Intersect);
                                SKPath add = field.Op(localBlack, SKPathOp.Difference);
                                if (erase != null && !erase.IsEmpty)
                                {
                                    ops.Add(LabelOp.WhiteFill(erase));
                                }

                                if (add != null && !add.IsEmpty)
                                {
                                    ops.Add(LabelOp.Black(add));
                                }
                            }

                            blackOps.Add(new BlackOp(field, SKPathOp.Xor));
                        }
                    }
                    else
                    {
                        if (black != null)
                        {
                            ops.Add(LabelOp.Black(black));
                            blackOps?.Add(new BlackOp(black, SKPathOp.Union));
                        }

                        if (white != null)
                        {
                            ops.Add(LabelOp.WhiteFill(white));
                            blackOps?.Add(new BlackOp(white, SKPathOp.Difference));
                        }

                        // Text is ink (black) but kept as a text op so it can be drawn as real text; its outline
                        // still joins the black region (lazily) so a later reverse can knock it out.
                        foreach (TextRun t in texts)
                        {
                            LabelOp textOp = LabelOp.TextOp(t);
                            ops.Add(textOp);
                            blackOps?.Add(new BlackOp(textOp, SKPathOp.Union));
                        }
                    }

                    foreach (ImageOp image in context.TakeImages())
                    {
                        ops.Add(LabelOp.Img(image));
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
            private readonly SKPath _geom;   // eager: a reverse field, or a geometry op
            private readonly TextRun _run;   // lazy: a text op's outline, built only if it overlaps a reverse
            public readonly SKPathOp Mode;   // Union (black), Difference (white), Xor (reverse)
            public readonly SKRect Bounds;

            public BlackOp(SKPath geom, SKPathOp mode)
            {
                this._geom = geom;
                this._run = null;
                this.Mode = mode;
                this.Bounds = geom.Bounds;
            }

            public BlackOp(LabelOp op, SKPathOp mode)
            {
                this.Mode = mode;
                this.Bounds = op.Bounds;       // cheap — never builds a text outline
                this._run = op.IsText ? op.Text : null;
                this._geom = op.IsText ? null : op.Fill;
            }

            /// <summary>The geometry; a text op's outline materialises here, only for the few that a reverse overlaps.</summary>
            public SKPath Geom => this._geom ?? this._run.GetPath();
        }

        /// <summary>Seed the black-op list from the fill ops emitted so far (black = Union, white = Difference).
        /// Uses each op's cheap bounds, so text outlines are not built unless a reverse actually overlaps them.</summary>
        private static List<BlackOp> BuildBlackOps(List<LabelOp> ops)
        {
            var list = new List<BlackOp>(ops.Count);
            foreach (LabelOp op in ops)
            {
                if (!op.IsImage)
                {
                    list.Add(new BlackOp(op, op.White ? SKPathOp.Difference : SKPathOp.Union));
                }
            }

            return list;
        }

        /// <summary>The reverse field's geometry for the <c>^FR</c> XOR: the element's black geometry unioned with
        /// any text outlines. Returns <paramref name="black"/> unchanged when there is no text.</summary>
        private static SKPath CombineReverseField(SKPath black, IReadOnlyList<TextRun> texts)
        {
            if (texts.Count == 0)
            {
                return black;
            }

            var field = new SKPath { FillType = SKPathFillType.Winding };
            if (black != null)
            {
                field.AddPath(black);
            }

            foreach (TextRun t in texts)
            {
                field.AddPath(t.GetPath());   // a reverse field's own text outline is genuinely needed
            }

            return field;
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
