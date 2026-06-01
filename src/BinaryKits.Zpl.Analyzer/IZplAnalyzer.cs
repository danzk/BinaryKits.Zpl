using BinaryKits.Zpl.Analyzer.Models;

namespace BinaryKits.Zpl.Analyzer
{
    public interface IZplAnalyzer
    {
        AnalyzeInfo Analyze(string zplData);
    }
}
