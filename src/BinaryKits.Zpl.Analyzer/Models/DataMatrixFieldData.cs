using BinaryKits.Zpl.Label;

namespace BinaryKits.Zpl.Analyzer.Models
{
    public class DataMatrixFieldData : FieldDataBase
    {
        public FieldOrientation FieldOrientation { get; set; }
        public int Height { get; set; }
        public QualityLevel QualityLevel { get; set; }
    }
}
