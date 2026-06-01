using BinaryKits.Zpl.Label.Elements;

namespace BinaryKits.Zpl.Analyzer.Models
{
    public class LabelInfo
    {
        public string DownloadFormatName { get; set; }
        public ZplElementBase[] ZplElements { get; set; }
    }
}
