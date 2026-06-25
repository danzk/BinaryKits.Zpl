using System;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing.OneD;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>BarcodeAnsiCodabarElementDrawer</c> (<c>^BK</c>).</summary>
    public class BarcodeAnsiCodabarElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeAnsiCodabar;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcodeAnsiCodabar barcode)
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

            string content = barcode.Content.Trim('*');
            if (barcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            string interpretation = string.Format("*{0}*", content);

            var writer = new CodaBarWriter();
            bool[] result = writer.encode(content);
            int narrow = barcode.ModuleWidth;
            int wide = (int)Math.Floor(barcode.WideBarToNarrowBarWidthRatio * narrow);
            result = AdjustWidths(result, wide, narrow);
            int width = result.Length;
            SKPath bars = BoolArrayToPath(result, barcode.Height);
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
