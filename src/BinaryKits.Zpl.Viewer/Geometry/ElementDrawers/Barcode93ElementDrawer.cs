using System;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing.OneD;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>Barcode93ElementDrawer</c> (<c>^BA</c>).</summary>
    public class Barcode93ElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcode93;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcode93 barcode)
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

            var writer = new Code93Writer();
            bool[] result = writer.encode(content);
            int width = result.Length * barcode.ModuleWidth;
            SKPath bars = BoolArrayToPath(result, barcode.Height, barcode.ModuleWidth);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                float emSize = FontScale.GetBitmappedFontSize("A", Math.Min(barcode.ModuleWidth, 10), printDensityDpmm).Value;
                using var font = new SKFont(options.FontManager.FontLoader("A"), emSize);
                DrawInterpretationLine(content, font, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.PrintInterpretationLineAboveCode, options);
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
