using BinaryKits.Zpl.Label;

namespace BinaryKits.Zpl.Analyzer.Models
{
    public class FieldBlock
    {
        public int WidthOfTextBlockLine { get; set; }
        public int MaximumNumberOfLinesInTextBlock { get; set; }
        public int AddOrDeleteSpaceBetweenLines { get; set; }
        public TextJustification TextJustification { get; set; }
        public int HangingIndentOfTheSecondAndRemainingLines { get; set; }
    }
}
