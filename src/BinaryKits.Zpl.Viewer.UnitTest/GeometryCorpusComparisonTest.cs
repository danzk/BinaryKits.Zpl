using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Geometry;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.UnitTest
{
    /// <summary>
    /// Renders the whole shared label corpus through the geometry-first renderer and the legacy raster
    /// renderer (sharing one <see cref="FontManager"/> so typefaces match) and asserts: identical pixel
    /// dimensions, high black-ink agreement, and a non-empty vector PDF. This locks in parity across every
    /// element type and proves the geometry renderer handles the full corpus without error.
    /// </summary>
    [TestClass]
    public class GeometryCorpusComparisonTest
    {
        private static readonly string LabelsRoot = Path.Combine(AppContext.BaseDirectory, "Labels");

        public TestContext TestContext { get; set; }

        public static IEnumerable<object[]> Labels()
        {
            if (!Directory.Exists(LabelsRoot))
            {
                yield break;
            }

            foreach (string path in Directory.EnumerateFiles(LabelsRoot, "*.zpl2", SearchOption.AllDirectories).OrderBy(p => p))
            {
                yield return new object[] { Path.GetFileName(path), path };
            }
        }

        public static string DisplayName(MethodInfo methodInfo, object[] data) => $"{methodInfo.Name}({data[0]})";

        [DataTestMethod]
        [DynamicData(nameof(Labels), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(DisplayName))]
        public void Geometry_Matches_Legacy(string name, string path)
        {
            (double width, double height) = SizeFromName(name);
            string zpl = File.ReadAllText(path);

            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var info = analyzer.Analyze(zpl);
            var elements = info.LabelInfos[0].ZplElements;

            // One shared FontManager so both renderers resolve identical typefaces.
            var options = new DrawerOptions(new FontManager()) { OpaqueBackground = true };
            var geom = new GeometryRenderer(storage, options);
            var legacy = new ZplElementDrawer(storage, options);

            byte[] geomPng = geom.DrawPng(elements, width, height, 8);
            byte[] legacyPng = legacy.Draw(elements, width, height, 8);

            using var ga = SKBitmap.Decode(geomPng);
            using var la = SKBitmap.Decode(legacyPng);
            Assert.AreEqual(la.Width, ga.Width, $"{name}: width mismatch");
            Assert.AreEqual(la.Height, ga.Height, $"{name}: height mismatch");

            double agreement = InkAgreement(ga, la);
            TestContext.WriteLine($"{name}: ink agreement {agreement:P2}");

            // The geometry renderer must also produce a non-empty vector PDF without throwing.
            byte[] pdf = geom.DrawPdf(elements, width, height, 8);
            Assert.IsTrue(pdf.Length > 100 && pdf[0] == (byte)'%', $"{name}: PDF not produced");

            if (agreement < 0.90)
            {
                var outDir = Path.Combine(Path.GetTempPath(), "GeometryRenderTests", "corpus");
                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir, name + ".geometry.png"), geomPng);
                File.WriteAllBytes(Path.Combine(outDir, name + ".legacy.png"), legacyPng);
            }

            Assert.IsTrue(agreement >= 0.90, $"{name}: ink agreement {agreement:P2} below 90% (diff PNGs written to temp)");
        }

        private static double InkAgreement(SKBitmap a, SKBitmap b)
        {
            SKColor[] pa = a.Pixels;
            SKColor[] pb = b.Pixels;
            int n = Math.Min(pa.Length, pb.Length);
            long match = 0;
            for (int i = 0; i < n; i++)
            {
                bool ai = pa[i].Red < 128;
                bool bi = pb[i].Red < 128;
                if (ai == bi)
                {
                    match++;
                }
            }

            return n == 0 ? 1.0 : (double)match / n;
        }

        private static (double width, double height) SizeFromName(string name)
        {
            Match m = Regex.Match(Path.GetFileNameWithoutExtension(name), @"(\d+)x(\d+)$");
            return m.Success
                ? (double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value))
                : (101.6, 152.4);
        }
    }
}
