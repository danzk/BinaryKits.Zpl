using BinaryKits.Zpl.Label.Elements;

namespace BinaryKits.Zpl.Analyzer.CommandAnalyzers
{
    public class RecallFormatCommandAnalyzer : ZplCommandAnalyzerBase
    {
        public RecallFormatCommandAnalyzer() : base("^XF") { }

        ///<inheritdoc/>
        public override ZplElementBase Analyze(string zplCommand, VirtualPrinter virtualPrinter, IPrinterStorage printerStorage)
        {
            string formatName = zplCommand.Substring(this.PrinterCommandPrefix.Length);

            return new ZplRecallFormat(formatName);
        }
    }
}
