using System;
using System.Text.RegularExpressions;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing;
using ZXing.Common;
using ZXing.Datamatrix;
using ZXing.Datamatrix.Encoder;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>DataMatrixElementDrawer</c> (<c>^BX</c>).</summary>
    public class DataMatrixElementDrawer : GeometryBarcodeDrawerBase
    {
        private static readonly Regex gs1Regex = new Regex(@"^_1(.+)$", RegexOptions.Compiled);

        public override bool CanDraw(ZplElementBase element) => element is ZplDataMatrix;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplDataMatrix dataMatrix)
            {
                return currentPosition;
            }

            if (dataMatrix.Height == 0)
            {
                throw new Exception("Matrix Height is set to zero.");
            }

            if (string.IsNullOrWhiteSpace(dataMatrix.Content))
            {
                throw new Exception("Matrix Content is empty.");
            }

            float x = dataMatrix.PositionX;
            float y = dataMatrix.PositionY;
            if (dataMatrix.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = dataMatrix.Content;
            if (dataMatrix.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            bool gs1Mode = false;
            Match gs1Match = gs1Regex.Match(content);
            if (gs1Match.Success)
            {
                content = gs1Match.Groups[1].Value;
                gs1Mode = true;
            }

            var writer = new DataMatrixWriter();
            var encodingOptions = new DatamatrixEncodingOptions
            {
                SymbolShape = SymbolShapeHint.FORCE_SQUARE,
                CompactEncoding = gs1Mode,
                GS1Format = gs1Mode,
            };
            BitMatrix result = writer.encode(content, BarcodeFormat.DATA_MATRIX, 0, 0, encodingOptions.Hints);

            int width = result.Width * dataMatrix.Height;
            int height = result.Height * dataMatrix.Height;
            SKPath bars = BitMatrixToPath(result, dataMatrix.Height);
            DrawBarcode(bars, x, y, width, height, dataMatrix.FieldOrigin != null, dataMatrix.FieldOrientation);

            return CalculateNextDefaultPosition(x, y, width, height, dataMatrix.FieldOrigin != null, dataMatrix.FieldOrientation, currentPosition);
        }
    }
}
