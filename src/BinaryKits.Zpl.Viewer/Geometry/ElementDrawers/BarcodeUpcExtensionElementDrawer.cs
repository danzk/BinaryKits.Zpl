using System;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Analyzer.Symologies;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>BarcodeUpcExtensionElementDrawer</c>.</summary>
    public class BarcodeUpcExtensionElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeUpcExtension;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcodeUpcExtension barcode)
            {
                return currentPosition;
            }

            float x = barcode.PositionX;
            float y = barcode.PositionY;
            if (barcode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = barcode.Content;
            if (barcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (content.Length <= 2)
            {
                content = content.PadLeft(2, '0');
            }
            else
            {
                content = content.PadLeft(5, '0').Substring(0, 5);
            }

            string interpretation = content;

            bool[] data = UpcExtensionSymbology.Encode(content);
            int width = data.Length * barcode.ModuleWidth;
            SKPath bars = BoolArrayToPath(data, barcode.Height, barcode.ModuleWidth);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                float emSize = FontScale.GetBitmappedFontSize("A", Math.Min(barcode.ModuleWidth, 10), printDensityDpmm).Value;
                using var font = new SKFont(options.FontManager.FontLoader("A"), emSize);
                DrawInterpretationLine(interpretation, font, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.PrintInterpretationLineAboveCode, options);
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
