using System.IO;

namespace BinaryKits.Zpl.Viewer.UnitTest
{
    /// <summary>
    /// Where the render tests drop their PNG/SVG/report artifacts for inspection: a <c>GeometryRenderTests</c>
    /// folder under the current working directory (not %TEMP%), so they sit next to the test run and are easy
    /// to find. Tests still call <see cref="Directory.CreateDirectory(string)"/> before writing.
    /// </summary>
    internal static class TestOutput
    {
        public static readonly string Root = Path.Combine(Directory.GetCurrentDirectory(), "GeometryRenderTests");
    }
}
