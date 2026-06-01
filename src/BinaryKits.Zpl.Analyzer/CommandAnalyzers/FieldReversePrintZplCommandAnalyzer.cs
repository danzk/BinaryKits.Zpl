using BinaryKits.Zpl.Label.Elements;

namespace BinaryKits.Zpl.Analyzer.CommandAnalyzers
{
    public class FieldReversePrintZplCommandAnalyzer : ZplCommandAnalyzerBase
    {
        public FieldReversePrintZplCommandAnalyzer() : base("^FR") { }

        ///<inheritdoc/>
        public override ZplElementBase Analyze(string zplCommand, VirtualPrinter virtualPrinter, IPrinterStorage printerStorage)
        {
            virtualPrinter.SetNextElementFieldReverse();

            return null;
        }
    }
}
