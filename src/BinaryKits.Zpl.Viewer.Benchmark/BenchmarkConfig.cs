using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;

namespace BinaryKits.Zpl.Viewer.Benchmark
{
    /// <summary>
    /// Benchmark configuration tuned for the geometry renderer rather than BenchmarkDotNet's defaults:
    ///
    /// <list type="bullet">
    ///   <item><b>One trimmed throughput job.</b> The default pipeline over 6 methods × 8 labels runs for ~30 min;
    ///   these are sub-millisecond-to-millisecond operations that converge fast, so 3 warmups / 8 iterations keep
    ///   the numbers stable while cutting wall-clock to a few minutes.</item>
    ///   <item><b><see cref="MemoryDiagnoser"/>.</b> Bytes allocated per op — allocation pressure is as important
    ///   as time for a render path that may run per-frame in a live preview.</item>
    ///   <item><b>Per-category baselines</b> (see <c>RenderBenchmarks</c>): the ratio column compares within a
    ///   category, so <c>Render</c> shows transparent vs opaque (≈ 1.0× — one unified path) and <c>Output</c>
    ///   shows the geometry PNG/PDF speedup vs the legacy raster baseline, instead of one meaningless global ratio.</item>
    ///   <item><b>Trend ratios + a rank column</b>, and the default GitHub-markdown report under
    ///   <c>BenchmarkDotNet.Artifacts/</c> so a run can be pasted straight into a PR or the README.</item>
    /// </list>
    /// </summary>
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(8)
                .WithLaunchCount(1)
                .WithId("Render"));

            AddDiagnoser(MemoryDiagnoser.Default);

            AddColumnProvider(DefaultColumnProviders.Instance);
            AddColumn(RankColumn.Arabic);
            AddLogger(ConsoleLogger.Default);
            // The default config already exports a GitHub-markdown report (BenchmarkDotNet.Artifacts/results/*.md);
            // we don't re-add MarkdownExporter.GitHub or it warns about a duplicate.

            WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Declared, MethodOrderPolicy.Declared));
            WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
        }
    }
}
