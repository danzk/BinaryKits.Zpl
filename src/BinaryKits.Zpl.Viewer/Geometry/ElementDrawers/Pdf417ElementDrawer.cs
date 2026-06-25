using System;
using System.Collections.Generic;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing;
using ZXing.Common;
using ZXing.PDF417;
using ZXing.PDF417.Internal;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>Pdf417ElementDrawer</c> (<c>^B7</c>). Renders at module resolution and lets
    /// the <c>DrawBarcode</c> scale transform handle the pixel sizing (one rect per horizontal run per
    /// bar, sampling one in every <c>AspectRatio</c> rows that ZXing emits).
    /// </summary>
    public class Pdf417ElementDrawer : GeometryBarcodeDrawerBase
    {
        // PDF417_ASPECT_RATIO=A3: ZXing writes each codeword bar across 3 identical pixel rows in the
        // matrix to encode the 3:1 height/width aspect ratio. We sample one row per bar instead.
        private const int AspectRatio = 3;

        public override bool CanDraw(ZplElementBase element) => element is ZplPDF417;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplPDF417 pdf417)
            {
                return currentPosition;
            }

            if (pdf417.Height == 0)
            {
                throw new Exception("PDF417 Height is set to zero.");
            }

            string content = pdf417.Content;
            if (pdf417.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("PDF147 Content is empty.");
            }

            float x = pdf417.PositionX;
            float y = pdf417.PositionY;
            if (pdf417.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            int mincols, maxcols, minrows, maxrows;
            if (pdf417.Rows != null)
            {
                minrows = pdf417.Rows.Value;
                maxrows = pdf417.Rows.Value;
            }
            else
            {
                minrows = 3;
                maxrows = 90;
            }

            if (pdf417.Columns != null)
            {
                mincols = pdf417.Columns.Value;
                maxcols = pdf417.Columns.Value;
            }
            else
            {
                mincols = 1;
                maxcols = 30;

                if (pdf417.Rows != null)
                {
                    minrows /= 2;
                }
            }

            var writer = new PDF417Writer();
            var hints = new Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.PDF417_COMPACT, pdf417.Compact },
                { EncodeHintType.PDF417_COMPACTION, Compaction.AUTO },
                { EncodeHintType.PDF417_ASPECT_RATIO, PDF417AspectRatio.A3 },
                { EncodeHintType.PDF417_IMAGE_ASPECT_RATIO, 1.0f },
                { EncodeHintType.MARGIN, 0 },
                { EncodeHintType.ERROR_CORRECTION, ConvertErrorCorrection(pdf417.SecurityLevel) },
                { EncodeHintType.PDF417_DIMENSIONS, new Dimensions(mincols, maxcols, minrows, maxrows) },
            };

            BitMatrix matrix = writer.encode(content, BarcodeFormat.PDF_417, 0, 0, hints);

            // Module-resolution geometry: one rect per horizontal run per bar (one in AspectRatio rows).
            // The DrawBarcode scale transform sizes each module to (moduleWidth × Height).
            int moduleRows = matrix.Height / AspectRatio;
            int width = matrix.Width * pdf417.ModuleWidth;
            int height = moduleRows * pdf417.Height;
            SKPath bars = BitMatrixToPath(matrix, pixelScale: 1, rowStride: AspectRatio);
            DrawBarcode(bars, x, y, width, height, pdf417.FieldOrigin != null, pdf417.FieldOrientation,
                scaleX: pdf417.ModuleWidth, scaleY: pdf417.Height);

            return CalculateNextDefaultPosition(x, y, width, height, pdf417.FieldOrigin != null, pdf417.FieldOrientation, currentPosition);
        }

        private static PDF417ErrorCorrectionLevel ConvertErrorCorrection(int correction)
        {
            switch (correction)
            {
                case 0: return PDF417ErrorCorrectionLevel.L0;
                case 1: return PDF417ErrorCorrectionLevel.L1;
                case 2: return PDF417ErrorCorrectionLevel.L2;
                case 3: return PDF417ErrorCorrectionLevel.L3;
                case 4: return PDF417ErrorCorrectionLevel.L4;
                case 5: return PDF417ErrorCorrectionLevel.L5;
                case 6: return PDF417ErrorCorrectionLevel.L6;
                case 7: return PDF417ErrorCorrectionLevel.L7;
                case 8: return PDF417ErrorCorrectionLevel.L8;
                default: return PDF417ErrorCorrectionLevel.AUTO;
            }
        }
    }
}
