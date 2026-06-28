using System;
using System.IO;
using System.Linq;
using System.Text;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Geometry;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.UnitTest
{
    /// <summary>
    /// Spike: render the corpus through <see cref="GeometryRenderer.DrawSvg"/> and check vector fidelity.
    /// Writes the SVGs to GeometryRenderTests/svg under the working directory for inspection. Validates the two risk areas from the
    /// feasibility discussion: (1) reverse/barcode labels stay pure vector (no embedded raster), (2) genuine
    /// raster graphics embed as <c>&lt;image&gt;</c>, and probes whether the image color-filter (media → alpha 0)
    /// survives serialisation by decoding the embedded PNG.
    /// </summary>
    [TestClass]
    public class GeometrySvgSpikeTest
    {
        private static readonly string LabelsRoot = Path.Combine(AppContext.BaseDirectory, "Labels");
        private static readonly string OutDir = Path.Combine(TestOutput.Root, "svg");

        private static (string svg, byte[] bytes) Render(string name, SKColor? stock = null)
        {
            string path = Directory.EnumerateFiles(LabelsRoot, name + ".zpl2", SearchOption.AllDirectories).First();
            (double w, double h) = SizeFromName(name);
            string zpl = File.ReadAllText(path);

            IPrinterStorage storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;

            var options = new DrawerOptions(new FontManager()) { OpaqueBackground = true };
            if (stock.HasValue)
            {
                options.LabelColor = stock.Value;
            }

            byte[] bytes = new GeometryRenderer(storage, options).DrawSvg(elements, w, h, 8);

            Directory.CreateDirectory(OutDir);
            string suffix = stock == SKColors.Transparent ? "-transparent" : "";
            File.WriteAllBytes(Path.Combine(OutDir, name + suffix + ".svg"), bytes);
            return (Encoding.UTF8.GetString(bytes), bytes);
        }

        [DataTestMethod]
        [DataRow("FieldReversePrint1-54x86")]
        [DataRow("Barcode128-102x152")]
        [DataRow("Example11-102x152")]
        public void DrawSvg_ReverseAndBarcode_AreVector(string name)
        {
            (string svg, _) = Render(name);

            StringAssert.Contains(svg, "<svg", $"{name}: not an SVG document");
            StringAssert.Contains(svg, "viewBox=\"0 0 ", $"{name}: missing a viewBox (won't scale to its container)");
            StringAssert.Contains(svg, "<path", $"{name}: no vector path geometry");
            Assert.IsFalse(svg.Contains("<image"), $"{name}: contains an embedded raster <image> — not pure vector");
        }

        [TestMethod]
        public void DrawSvg_Example11_Transparent_IsVector()
        {
            (string svg, _) = Render("Example11-102x152", SKColors.Transparent);
            StringAssert.Contains(svg, "<path", "transparent Example11: no vector path geometry");
            Assert.IsFalse(svg.Contains("<image"), "transparent Example11: unexpected embedded raster");
        }

        [TestMethod]
        public void DrawSvg_GraphicField_EmbedsImage_AndProbesColorFilter()
        {
            // Transparent stock forces the image color-filter to matter: a monochrome graphic's white media must
            // map to alpha 0. If SKSvgCanvas drops the filter, the embedded PNG will still have opaque white.
            (string svg, _) = Render("GraphicField-54x86", SKColors.Transparent);
            StringAssert.Contains(svg, "<image", "GraphicField: expected an embedded raster <image>");

            // Pull the first base64 PNG out of the data URI and report whether its 'media' came through transparent.
            int b64 = svg.IndexOf("base64,", StringComparison.Ordinal);
            Assert.IsTrue(b64 > 0, "GraphicField: no base64 image data");
            int start = b64 + "base64,".Length;
            int end = svg.IndexOfAny(new[] { '"', '\'' }, start);
            byte[] png = Convert.FromBase64String(svg.Substring(start, end - start));

            using var bmp = SKBitmap.Decode(png);
            bool anyTransparent = false;
            bool anyOpaqueWhite = false;
            for (int i = 0; i < bmp.Pixels.Length; i++)
            {
                SKColor p = bmp.Pixels[i];
                if (p.Alpha == 0) anyTransparent = true;
                if (p.Alpha == 255 && p.Red > 250 && p.Green > 250 && p.Blue > 250) anyOpaqueWhite = true;
            }

            Console.WriteLine($"GraphicField embedded image {bmp.Width}x{bmp.Height}: anyTransparent={anyTransparent}, anyOpaqueWhite={anyOpaqueWhite}");
            // No assert on the filter outcome yet — this is a spike probe; the Console line reports the verdict.
        }

        private static (double w, double h) SizeFromName(string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(name, @"(\d+)x(\d+)$");
            return m.Success ? (double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value)) : (101.6, 152.4);
        }
    }
}
