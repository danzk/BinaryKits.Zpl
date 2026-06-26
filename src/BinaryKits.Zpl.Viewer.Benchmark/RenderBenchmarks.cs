using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Geometry;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Benchmark
{
    /// <summary>
    /// Wall-clock + allocation benchmarks for the geometry-first renderer, one parameter set per representative
    /// label. Each label is parsed once in <see cref="Setup"/> (sharing one <see cref="PrinterStorage"/> across
    /// parse and render so <c>~DG</c>/<c>~DY</c> downloads resolve); the benchmarks then isolate the stages:
    ///
    /// <list type="bullet">
    ///   <item><see cref="Build"/> — analyse the ordered op-list (<c>CreateLabelDrawing</c>).</item>
    ///   <item><see cref="RenderOpaque"/> / <see cref="RenderTransparent"/> — the unified net-ink render onto a
    ///   pre-built surface, differing only in the page background (they should match — the paths are one).</item>
    ///   <item><see cref="DrawPng"/> / <see cref="DrawPdf"/> — full build + render + encode (PNG raster, vector PDF).</item>
    ///   <item><see cref="LegacyPng"/> — the legacy raster renderer, the baseline.</item>
    /// </list>
    /// </summary>
    [Config(typeof(BenchmarkConfig))]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class RenderBenchmarks
    {
        private const int Dpmm = 8;
        private static readonly string LabelsRoot = Path.Combine(AppContext.BaseDirectory, "Labels");

        [Params(
            "BarcodePDF417-102x152",
            "DataMatrix-102x152",
            "QrCode-54x86",
            "MaxiCode-102x152",
            "Barcode128-102x152",
            "Example11-102x152",
            "Example1-102x152",
            "FieldDataText1-102x152")]
        public string Label { get; set; }

        private double _width;
        private double _height;
        private ZplElementBase[] _elements;
        private SkiaGeometryRenderer _geometry;
        private ZplElementDrawer _legacy;
        private SkLabelDrawing _drawing;
        private SKSurface _surface;
        private SKCanvas _canvas;
        private RenderSettings _opaque;
        private RenderSettings _transparent;

        [GlobalSetup]
        public void Setup()
        {
            string path = Directory.EnumerateFiles(LabelsRoot, Label + ".zpl2", SearchOption.AllDirectories).First();
            (_width, _height) = SizeFromName(Label);
            string zpl = File.ReadAllText(path);

            IPrinterStorage storage = new PrinterStorage();
            _elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;

            var options = new DrawerOptions(new FontManager()) { OpaqueBackground = true };
            _geometry = new SkiaGeometryRenderer(storage, options);
            _legacy = new ZplElementDrawer(storage, options);

            _opaque = new RenderSettings(SKColors.Black, SKColors.White, opaqueBackground: true, antialias: true);
            _transparent = new RenderSettings(SKColors.Black, SKColors.Transparent, opaqueBackground: false, antialias: true);

            _drawing = _geometry.CreateLabelDrawing(_elements, _width, _height, Dpmm);
            _surface = SKSurface.Create(new SKImageInfo(_drawing.Width, _drawing.Height));
            _canvas = _surface.Canvas;
        }

        [GlobalCleanup]
        public void Cleanup() => _surface?.Dispose();

        [Benchmark]
        [BenchmarkCategory("Build")]
        public SkLabelDrawing Build() => _geometry.CreateLabelDrawing(_elements, _width, _height, Dpmm);

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Render")]
        public SKSurface RenderOpaque()
        {
            _drawing.Render(_canvas, _opaque);
            _canvas.Flush();
            return _surface;
        }

        [Benchmark]
        [BenchmarkCategory("Render")]
        public SKSurface RenderTransparent()
        {
            _drawing.Render(_canvas, _transparent);
            _canvas.Flush();
            return _surface;
        }

        [Benchmark]
        [BenchmarkCategory("Output")]
        public byte[] DrawPng() => _geometry.DrawPng(_elements, _width, _height, Dpmm);

        [Benchmark]
        [BenchmarkCategory("Output")]
        public byte[] DrawPdf() => _geometry.DrawPdf(_elements, _width, _height, Dpmm);

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Output")]
        public byte[] LegacyPng() => _legacy.Draw(_elements, _width, _height, Dpmm);

        private static (double width, double height) SizeFromName(string name)
        {
            Match m = Regex.Match(name, @"(\d+)x(\d+)$");
            return m.Success
                ? (double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value))
                : (101.6, 152.4);
        }
    }
}
