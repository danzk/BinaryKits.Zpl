using System;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing.OneD;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>BarcodeUpcEElementDrawer</c> (<c>^B9</c>).</summary>
    public class BarcodeUpcEElementDrawer : GeometryBarcodeDrawerBase
    {
        private static readonly bool[] guards = new bool[51];

        static BarcodeUpcEElementDrawer()
        {
            foreach (int idx in new[] { 0, 2, 46, 48, 50 })
            {
                guards[idx] = true;
            }
        }

        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeUpcE;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcodeUpcE barcode)
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

            // [S]DDDDDD[C]
            if (content.Length < 7)
            {
                content = content.PadLeft(7, '0');
            }
            else if (content.Length <= 8)
            {
                content = content.Substring(0, 7);
            }
            else
            {
                string numberSystem = "0";
                content = content.PadRight(10, '0');
                if (content.Length > 10)
                {
                    numberSystem = content.Substring(0, 1);
                    content = content.Substring(1, 10);
                }

                int manufacturer = int.Parse(content.Substring(0, 5));
                int product = int.Parse(content.Substring(5, 5));

                if (manufacturer % 100 == 0)
                {
                    int trail = manufacturer / 100 % 10;
                    if (trail <= 2)
                    {
                        content = $"{numberSystem}{manufacturer / 1000:D2}{product % 1000:D3}{trail}";
                    }
                    else
                    {
                        content = $"{numberSystem}{manufacturer / 100:D3}{product % 100:D2}{3}";
                    }
                }
                else if (manufacturer % 10 == 0)
                {
                    content = $"{numberSystem}{manufacturer / 10:D4}{product % 10:D1}{4}";
                }
                else
                {
                    content = $"{numberSystem}{manufacturer:D5}{Math.Max(product % 10, 5):D1}";
                }
            }

            string interpretation = content;

            if (barcode.PrintCheckDigit)
            {
                string expanded = UPCEReader.convertUPCEtoUPCA(content);
                int checksum = 0;
                for (int i = 0; i < 11; i++)
                {
                    checksum += (expanded[i] - 48) * (i % 2 * 2 + 7);
                }

                interpretation = string.Format("{0}{1}", interpretation, checksum % 10);
            }

            var writer = new UPCEWriter();
            bool[] result = writer.encode(content);
            int width = result.Length * barcode.ModuleWidth;
            SKPath bars = BoolArrayToPath(result, barcode.Height, barcode.ModuleWidth);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                float emSize = FontScale.GetBitmappedFontSize("A", Math.Min(barcode.ModuleWidth, 10), printDensityDpmm).Value;
                using var font = new SKFont(options.FontManager.FontLoader("A"), emSize);
                if (barcode.PrintInterpretationLineAboveCode)
                {
                    DrawInterpretationLine(interpretation, font, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, true, options);
                }
                else
                {
                    DrawDigitInterpretationLine(guards, null, interpretation, font, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.ModuleWidth, options, i => (i == 0) ? 4 : (i == 6 ? 6 : 0));
                }
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
