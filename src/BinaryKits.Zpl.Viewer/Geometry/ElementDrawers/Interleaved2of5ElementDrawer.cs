using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing.OneD;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>Interleaved2of5BarcodeDrawer</c> (<c>^B2</c>).</summary>
    public class Interleaved2of5ElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeInterleaved2of5;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplBarcodeInterleaved2of5 barcode)
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

            if (barcode.Mod10CheckDigit)
            {
                int sum = 0;
                for (int i = 0; i < content.Length; i++)
                {
                    if (!char.IsDigit(content[i]))
                    {
                        return currentPosition;
                    }

                    int digit = content[i] - '0';
                    int weight = ((content.Length - i) % 2 == 0) ? 3 : 1;
                    sum += digit * weight;
                }

                int checkDigit = (10 - (sum % 10)) % 10;
                content = $"{content}{checkDigit}";
            }

            if (content.Length % 2 != 0)
            {
                content = $"0{content}";
            }

            var writer = new ITFWriter();
            bool[] result = writer.encode(content);
            int narrow = barcode.ModuleWidth;
            int wide = (int)Math.Floor(barcode.WideBarToNarrowBarWidthRatio * narrow);
            result = AdjustWidths(result, wide, narrow);
            int width = result.Length;
            SKPath bars = BoolArrayToPath(result, barcode.Height);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                float emSize = (float)Math.Min(barcode.ModuleWidth * 10.0, 100.0);
                using var font = new SKFont(options.FontManager.FontLoader("A"), emSize);
                DrawInterpretationLine(content, font, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.PrintInterpretationLineAboveCode, options);
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
