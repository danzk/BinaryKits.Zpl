using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Appearance and mode for rendering a (mode-agnostic) <see cref="SkLabelDrawing"/> onto a canvas.
    /// The display list itself carries no colour or mode, so one built label can be rendered with
    /// different settings — black-on-white, a coloured ribbon/stock, or a transparent stock — without
    /// rebuilding.
    /// </summary>
    public readonly struct RenderSettings
    {
        public RenderSettings(SKColor ribbon, SKColor stock, bool opaqueBackground, bool antialias)
        {
            this.Ribbon = ribbon;
            this.Stock = stock;
            this.OpaqueBackground = opaqueBackground;
            this.Antialias = antialias;
        }

        /// <summary>Ink / foreground colour — the printer ribbon.</summary>
        public SKColor Ribbon { get; }

        /// <summary>
        /// Label stock / media colour — the page background when <see cref="OpaqueBackground"/> is set. A fully
        /// transparent stock (<see cref="SKColor.Alpha"/> == 0) leaves the background transparent, so knockouts,
        /// gaps and reverse holes become genuine (vector) holes rather than stock-coloured fills.
        /// </summary>
        public SKColor Stock { get; }

        /// <summary>Clear the page to the <see cref="Stock"/> colour; otherwise leave it transparent.</summary>
        public bool OpaqueBackground { get; }

        /// <summary>Whether geometry/edges are anti-aliased.</summary>
        public bool Antialias { get; }

        /// <summary>Convenience: the stock has no opacity, so the rendered page background is transparent.</summary>
        public bool TransparentStock => this.Stock.Alpha == 0;
    }
}
