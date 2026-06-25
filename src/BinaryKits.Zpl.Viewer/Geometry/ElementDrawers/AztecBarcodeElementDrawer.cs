using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing.Aztec;
using ZXing.Common;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>AztecBarcodeElementDrawer</c> (<c>^BO</c>).</summary>
    public class AztecBarcodeElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplAztecBarcode;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplAztecBarcode aztecBarcode)
            {
                return currentPosition;
            }

            float x = aztecBarcode.PositionX;
            float y = aztecBarcode.PositionY;
            if (aztecBarcode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = aztecBarcode.Content;
            if (aztecBarcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            var writer = new AztecWriter();
            var encodingOptions = new AztecEncodingOptions();
            if (aztecBarcode.ErrorControl >= 1 && aztecBarcode.ErrorControl <= 99)
            {
                encodingOptions.ErrorCorrection = aztecBarcode.ErrorControl;
            }
            else if (aztecBarcode.ErrorControl >= 101 && aztecBarcode.ErrorControl <= 104)
            {
                encodingOptions.Layers = 100 - aztecBarcode.ErrorControl;
            }
            else if (aztecBarcode.ErrorControl >= 201 && aztecBarcode.ErrorControl <= 232)
            {
                encodingOptions.Layers = aztecBarcode.ErrorControl - 200;
            }
            else if (aztecBarcode.ErrorControl == 300)
            {
                encodingOptions.PureBarcode = true;
            }

            BitMatrix result = writer.encode(content, ZXing.BarcodeFormat.AZTEC, 0, 0, encodingOptions.Hints);

            int width = result.Width * aztecBarcode.MagnificationFactor;
            int height = result.Height * aztecBarcode.MagnificationFactor;
            SKPath bars = BitMatrixToPath(result, aztecBarcode.MagnificationFactor);
            DrawBarcode(bars, x, y, width, height, aztecBarcode.FieldOrigin != null, aztecBarcode.FieldOrientation);

            return CalculateNextDefaultPosition(x, y, width, height, aztecBarcode.FieldOrigin != null, aztecBarcode.FieldOrientation, currentPosition);
        }
    }
}
