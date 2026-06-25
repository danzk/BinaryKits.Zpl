using System;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Analyzer.Symologies;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>Barcode128ElementDrawer</c> (<c>^BC</c>).</summary>
    public class Barcode128ElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcode128;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcode128 barcode)
            {
                return currentPosition;
            }

            string content = barcode.Content;
            if (barcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            Code128CodeSet codeSet = Code128CodeSet.Code128B;
            bool gs1 = false;
            if (string.IsNullOrEmpty(barcode.Mode) || barcode.Mode == "N")
            {
                codeSet = Code128CodeSet.Code128B;
            }
            else if (barcode.Mode == "A")
            {
                codeSet = Code128CodeSet.Code128;
            }
            else if (barcode.Mode == "D")
            {
                codeSet = Code128CodeSet.Code128;
                gs1 = true;
            }
            else if (barcode.Mode == "U")
            {
                codeSet = Code128CodeSet.Code128C;
                content = content.PadLeft(19, '0').Substring(0, 19);
                int checksum = 0;
                for (int i = 0; i < 19; i++)
                {
                    checksum += (content[i] - 48) * (i % 2 * 2 + 7);
                }

                content = $">8{content}{checksum % 10}";
            }

            float x = barcode.PositionX;
            float y = barcode.PositionY;
            if (barcode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            (bool[] data, string interpretation) = ZplCode128Symbology.Encode(content, codeSet, gs1);
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
