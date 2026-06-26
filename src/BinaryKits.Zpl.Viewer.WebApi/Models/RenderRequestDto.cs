namespace BinaryKits.Zpl.Viewer.WebApi.Models
{
    public class RenderRequestDto
    {
        /// <summary>
        /// Zpl data
        /// </summary>
        public string ZplData { get; set; }
        /// <summary>
        /// Label width in Millimeter
        /// </summary>
        public double LabelWidth { get; set; } = 101.6;
        /// <summary>
        /// Label height in Millimeter
        /// </summary>
        public double LabelHeight { get; set; } = 152.4;
        /// <summary>
        /// Dots per Millimeter
        /// </summary>
        public int PrintDensityDpmm { get; set; } = 8;
        /// <summary>
        /// File type
        /// </summary>
        public string Type { get; set; } = "image";
        /// <summary>
        /// Use the geometry renderer for the image (PNG) output too. PDF always uses it.
        /// </summary>
        public bool UseGeometryRenderer { get; set; }
        /// <summary>
        /// Ink / ribbon colour as a hex string (e.g. "#FF0000"). Geometry renderer only; default black.
        /// </summary>
        public string RibbonColor { get; set; }
        /// <summary>
        /// Label stock / media colour as a hex string (e.g. "#FFFF00") or "transparent". Geometry renderer
        /// only; default white.
        /// </summary>
        public string LabelColor { get; set; }
    }
}
