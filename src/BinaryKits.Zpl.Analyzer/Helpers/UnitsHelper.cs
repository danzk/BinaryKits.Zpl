namespace BinaryKits.Zpl.Analyzer.Helpers
{
    internal static class UnitsHelper
    {
        internal static double ConvertMillimetersToInches(double labelWidth)
        {
            return labelWidth / 25.4;
        }
    }
}
