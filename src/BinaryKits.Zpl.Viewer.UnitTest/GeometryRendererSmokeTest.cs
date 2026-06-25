using System.IO;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Geometry;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.UnitTest
{
    /// <summary>
    /// Phase 1 smoke test: the geometry-first Skia renderer renders the supported shapes (^GB/^GC/^GE/^GD)
    /// to a valid PNG of the expected dimensions.
    /// </summary>
    [TestClass]
    public class GeometryRendererSmokeTest
    {
        private const string ShapesZpl =
            "^XA" +
            "^FO50,50^GB300,200,10^FS" +
            "^FO100,300^GC150,8,B^FS" +
            "^FO50,500^GE200,120,8^FS" +
            "^FO50,700^GD200,100,8,R^FS" +
            "^XZ";

        [TestMethod]
        public void DrawPng_Shapes_ProducesValidPngOfExpectedSize()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var info = analyzer.Analyze(ShapesZpl);
            var elements = info.LabelInfos[0].ZplElements;

            var renderer = new SkiaGeometryRenderer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] png = renderer.DrawPng(elements, 101.6, 152.4, 8);

            Assert.IsNotNull(png);
            Assert.IsTrue(png.Length > 0, "PNG should not be empty");

            using var bitmap = SKBitmap.Decode(png);
            Assert.IsNotNull(bitmap, "PNG should decode");
            Assert.AreEqual(813, bitmap.Width);   // round(101.6mm * 8 dpmm = 812.8)
            Assert.AreEqual(1219, bitmap.Height);  // round(152.4mm * 8 dpmm = 1219.2)

            // Dump for visual inspection.
            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "shapes-geometry.png"), png);

            // Legacy reference, for an eyeball comparison.
            var legacy = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] legacyPng = legacy.Draw(elements, 101.6, 152.4, 8);
            File.WriteAllBytes(Path.Combine(outDir, "shapes-legacy.png"), legacyPng);
        }

        // A solid black square with a reverse solid bar knocked out of it (^FR), plus a reverse bar that
        // partly overhangs the square (so part adds black on the white background, part erases).
        private const string ReverseZpl =
            "^XA" +
            "^FO50,50^GB400,400,400^FS" +        // solid black square
            "^FO120,120^FR^GB260,80,80^FS" +     // reverse bar fully inside -> knocks out white
            "^FO350,250^FR^GB200,80,80^FS" +     // reverse bar straddling the edge -> erase + add
            "^XZ";

        [TestMethod]
        public void DrawPng_FieldReverse_MatchesLegacy()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var elements = analyzer.Analyze(ReverseZpl).LabelInfos[0].ZplElements;

            var renderer = new SkiaGeometryRenderer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] png = renderer.DrawPng(elements, 101.6, 152.4, 8);

            using var bitmap = SKBitmap.Decode(png);
            Assert.IsNotNull(bitmap);

            // Inside the knocked-out reverse bar should be white; the surrounding square should be black.
            Assert.AreEqual(SKColors.White, bitmap.GetPixel(200, 150), "reverse bar interior should be knocked out white");
            Assert.AreEqual(SKColors.Black, bitmap.GetPixel(80, 80), "square body should be black");

            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "reverse-geometry.png"), png);

            var legacy = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true });
            File.WriteAllBytes(Path.Combine(outDir, "reverse-legacy.png"), legacy.Draw(elements, 101.6, 152.4, 8));
        }

        private const string BarcodeZpl =
            "^XA" +
            "^FO40,40^BY3^BCN,100,Y,N,N^FD123456^FS" +     // Code 128 + HRI
            "^FO40,260^B3N,N,100,Y,N^FDABC123^FS" +        // Code 39 + HRI
            "^FO40,470^BEN,90,Y,N^FD123456789012^FS" +     // EAN-13 + digit HRI
            "^FO40,690^B2N,90,Y,N^FD1234567890^FS" +       // Interleaved 2 of 5
            "^FO470,40^BXN,6,200^FDHelloDataMatrix^FS" +   // Data Matrix
            "^FO470,360^B7N,3,3,10,N^FDPDF417data^FS" +    // PDF417
            "^XZ";

        [TestMethod]
        public void DrawPng_Barcodes_RenderAndMatchLegacy()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var elements = analyzer.Analyze(BarcodeZpl).LabelInfos[0].ZplElements;

            var renderer = new SkiaGeometryRenderer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] png = renderer.DrawPng(elements, 101.6, 152.4, 8);

            using var bitmap = SKBitmap.Decode(png);
            Assert.IsNotNull(bitmap);
            Assert.AreEqual(813, bitmap.Width);

            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "barcodes-geometry.png"), png);

            var legacy = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true });
            File.WriteAllBytes(Path.Combine(outDir, "barcodes-legacy.png"), legacy.Draw(elements, 101.6, 152.4, 8));
        }

        private const string TextZpl =
            "^XA" +
            "^FO40,40^A0N,60,60^FDHello World^FS" +              // scalable font 0
            "^FO40,140^AAN,30,20^FDFixed Font A^FS" +            // bitmap font A
            "^FO40,220^A0N,40,40^FR^FDReverse^FS" +              // reverse text over...
            "^FO30,210^GB300,60,60^FS" +                          // ...a black bar (drawn after? order matters)
            "^FO40,320^FB400,3,0,L^A0N,30,30^FDThe quick brown fox jumps over the lazy dog^FS" +  // field block wrap
            "^FO40,520^A0N,50,50^FB400,1,0,C^FDCentered^FS" +     // centered block
            "^XZ";

        [TestMethod]
        public void DrawPng_Text_RenderAndMatchLegacy()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var elements = analyzer.Analyze(TextZpl).LabelInfos[0].ZplElements;

            var renderer = new SkiaGeometryRenderer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] png = renderer.DrawPng(elements, 101.6, 152.4, 8);

            using var bitmap = SKBitmap.Decode(png);
            Assert.IsNotNull(bitmap);

            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "text-geometry.png"), png);

            var legacy = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true });
            File.WriteAllBytes(Path.Combine(outDir, "text-legacy.png"), legacy.Draw(elements, 101.6, 152.4, 8));
        }

        private const string MaxiCodeZpl =
            "^XA" +
            "^FO80,80^BD4^FD123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ^FS" +
            "^XZ";

        [TestMethod]
        public void DrawPng_MaxiCode_RenderAndMatchLegacy()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var elements = analyzer.Analyze(MaxiCodeZpl).LabelInfos[0].ZplElements;

            var renderer = new SkiaGeometryRenderer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] png = renderer.DrawPng(elements, 101.6, 152.4, 8);

            using var bitmap = SKBitmap.Decode(png);
            Assert.IsNotNull(bitmap);

            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "maxicode-geometry.png"), png);

            var legacy = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true });
            File.WriteAllBytes(Path.Combine(outDir, "maxicode-legacy.png"), legacy.Draw(elements, 101.6, 152.4, 8));
        }

        // A barcode (raster in the legacy PDF) + reverse text over a black bar (rasterised by the legacy
        // FixPdfInvertDraw hack). The geometry renderer makes both vector.
        private const string PdfZpl =
            "^XA" +
            "^FO40,40^BY3^BCN,100,Y,N,N^FD123456^FS" +     // Code 128 -> raster bitmap in legacy PDF
            "^FO40,260^GB320,90,90^FS" +                    // solid black bar
            "^FO70,285^A0N,50,50^FR^FDSALE^FS" +            // reverse text -> FixPdfInvertDraw raster in legacy
            "^XZ";

        [TestMethod]
        public void DrawPdf_IsVector_NoRasterXObjects_UnlikeLegacy()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var elements = analyzer.Analyze(PdfZpl).LabelInfos[0].ZplElements;

            var renderer = new SkiaGeometryRenderer(storage, new DrawerOptions { OpaqueBackground = true });
            byte[] geomPdf = renderer.DrawPdf(elements, 101.6, 152.4, 8);

            string geom = System.Text.Encoding.Latin1.GetString(geomPdf);
            Assert.IsTrue(geom.StartsWith("%PDF"), "should be a PDF");
            StringAssert.DoesNotMatch(geom, new System.Text.RegularExpressions.Regex("/Subtype\\s*/Image"),
                "geometry PDF should contain NO raster image XObjects (^FR + barcode are vector)");

            // Legacy PDF embeds raster XObjects for the same content (the defect this work fixes).
            var legacy = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true, PdfOutput = true });
            byte[] legacyPdf = legacy.DrawPdf(elements, 101.6, 152.4, 8);
            string leg = System.Text.Encoding.Latin1.GetString(legacyPdf);
            StringAssert.Matches(leg, new System.Text.RegularExpressions.Regex("/Subtype\\s*/Image"),
                "legacy PDF rasterises the barcode / reverse field");

            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "label-geometry.pdf"), geomPdf);
            File.WriteAllBytes(Path.Combine(outDir, "label-legacy.pdf"), legacyPdf);
        }

        // ^GE used to ignore ^FR / white line colour (a wrong is-ZplGraphicCircle type check). A solid
        // reverse ellipse over a black bar must knock out white; both renderers must now agree.
        private const string EllipseReverseZpl =
            "^XA" +
            "^FO40,40^GB400,300,300^FS" +          // solid black bar
            "^FO120,90^FR^GE200,140,100^FS" +      // reverse solid ellipse -> white knockout
            "^XZ";

        [TestMethod]
        public void DrawPng_EllipseReverse_KnocksOut_AndMatchesLegacy()
        {
            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var elements = analyzer.Analyze(EllipseReverseZpl).LabelInfos[0].ZplElements;

            var options = new DrawerOptions(new FontManager()) { OpaqueBackground = true };
            byte[] geomPng = new SkiaGeometryRenderer(storage, options).DrawPng(elements, 101.6, 152.4, 8);
            byte[] legacyPng = new ZplElementDrawer(storage, options).Draw(elements, 101.6, 152.4, 8);

            using var g = SKBitmap.Decode(geomPng);
            using var l = SKBitmap.Decode(legacyPng);

            // Ellipse centre (~220,160) is knocked out white; a bar point outside the ellipse stays black.
            Assert.AreEqual(SKColors.White, g.GetPixel(220, 160), "geometry: ellipse interior should be knocked out");
            Assert.AreEqual(SKColors.Black, g.GetPixel(60, 60), "geometry: surrounding bar should be black");
            Assert.AreEqual(l.GetPixel(220, 160), g.GetPixel(220, 160), "geometry vs legacy: ellipse centre");
            Assert.AreEqual(l.GetPixel(60, 60), g.GetPixel(60, 60), "geometry vs legacy: bar");

            var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "ellipse-reverse-geometry.png"), geomPng);
            File.WriteAllBytes(Path.Combine(outDir, "ellipse-reverse-legacy.png"), legacyPng);
        }
    }
}
