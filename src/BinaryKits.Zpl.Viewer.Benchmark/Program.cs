using BenchmarkDotNet.Running;

namespace BinaryKits.Zpl.Viewer.Benchmark
{
    /// <summary>
    /// Entry point for the geometry-renderer benchmarks. Run in Release:
    /// <code>dotnet run -c Release --project src/BinaryKits.Zpl.Viewer.Benchmark</code>
    /// Pass BenchmarkDotNet switches through, e.g. <c>-- --filter *RenderBenchmarks*</c> or
    /// <c>-- --filter *DrawPdf*</c> to run a subset.
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
