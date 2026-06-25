using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Geometry-first drawer contract: unlike <see cref="IElementDrawer"/> (which paints straight onto an
    /// <see cref="SKCanvas"/>), an element drawer here <em>accumulates</em> <see cref="SKPath"/> geometry
    /// into a <see cref="SkDrawContext"/> so the orchestrator can composite it (union / white-erase / <c>^FR</c>
    /// XOR).
    /// </summary>
    public interface IGeometryElementDrawer
    {
        /// <summary>Prepare the drawer with the printer storage and the geometry-accumulating context.</summary>
        void Prepare(IPrinterStorage printerStorage, SkDrawContext context);

        /// <summary>Check if the drawer can draw this element.</summary>
        bool CanDraw(ZplElementBase element);

        /// <summary>Element requires reverse (XOR) draw (<c>^FR</c>).</summary>
        bool IsReverseDraw(ZplElementBase element);

        /// <summary>Element is drawn white (inverted-color reverse).</summary>
        bool IsWhiteDraw(ZplElementBase element);

        /// <summary>Draw the element; returns the updated default field position.</summary>
        SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition);

        /// <summary>Draw the element with international font context.</summary>
        SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont);

        /// <summary>Draw the element with international font and print-density context.</summary>
        SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm);
    }
}
