using System;
using System.Collections.Generic;
using System.Linq;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

using ZXing.Common;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry-first base for barcode drawers. Barcodes are emitted as <see cref="SKPath"/> (one
    /// rectangle per module run) instead of bitmaps, so they participate in the fill/XOR pipeline and
    /// stay crisp vector in a PDF. Rotation pivots match the legacy <c>GetRotationMatrix</c>.
    /// </summary>
    public abstract class GeometryBarcodeDrawerBase : GeometryElementDrawerBase
    {
        /// <summary>Minimum acceptable margin between a barcode and its interpretation line, in pixels.</summary>
        protected const float MIN_LABEL_MARGIN = 5f;

        /// <summary>Build bar geometry (local 0-based coords) from a 1-D module array, run-length merged.</summary>
        protected static SKPath BoolArrayToPath(bool[] array, int height, int moduleWidth = 1)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            int col = 0;
            while (col < array.Length)
            {
                if (!array[col]) { col++; continue; }
                int start = col;
                while (col < array.Length && array[col]) { col++; }
                path.AddRect(SKRect.Create(start * moduleWidth, 0, (col - start) * moduleWidth, height));
            }

            return path;
        }

        /// <summary>Bar geometry where a module is filled only when both array and mask are set.</summary>
        protected static SKPath BoolArrayWithMaskToPath(bool[] array, bool[] mask, int height, int moduleWidth = 1)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            int col = 0;
            while (col < array.Length)
            {
                bool on = array[col] && mask[col];
                if (!on) { col++; continue; }
                int start = col;
                while (col < array.Length && array[col] && mask[col]) { col++; }
                path.AddRect(SKRect.Create(start * moduleWidth, 0, (col - start) * moduleWidth, height));
            }

            return path;
        }

        /// <summary>
        /// Build 2-D matrix geometry (local 0-based coords), one rectangle per horizontal run on each
        /// sampled row. <paramref name="pixelScale"/> sizes the rects directly; <paramref name="rowStride"/>
        /// reads one in every N rows (PDF417 with an A3 aspect writes each bar across 3 identical rows).
        /// </summary>
        protected static SKPath BitMatrixToPath(BitMatrix matrix, int pixelScale, int rowStride = 1)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            int sampledRows = matrix.Height / rowStride;
            for (int r = 0; r < sampledRows; r++)
            {
                int sourceRow = r * rowStride;
                int col = 0;
                while (col < matrix.Width)
                {
                    if (!matrix[col, sourceRow]) { col++; continue; }
                    int start = col;
                    while (col < matrix.Width && matrix[col, sourceRow]) { col++; }
                    path.AddRect(SKRect.Create(start * pixelScale, r * pixelScale, (col - start) * pixelScale, pixelScale));
                }
            }

            return path;
        }

        protected static SKMatrix GetRotationMatrix(float x, float y, int width, int height, bool useFieldOrigin, FieldOrientation fieldOrientation)
        {
            SKMatrix matrix = SKMatrix.Empty;
            if (useFieldOrigin)
            {
                switch (fieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        matrix = SKMatrix.CreateRotationDegrees(90, x + height / 2, y + height / 2);
                        break;
                    case FieldOrientation.Rotated180:
                        matrix = SKMatrix.CreateRotationDegrees(180, x + width / 2, y + height / 2);
                        break;
                    case FieldOrientation.Rotated270:
                        matrix = SKMatrix.CreateRotationDegrees(270, x + width / 2, y + width / 2);
                        break;
                }
            }
            else
            {
                switch (fieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        matrix = SKMatrix.CreateRotationDegrees(90, x, y);
                        break;
                    case FieldOrientation.Rotated180:
                        matrix = SKMatrix.CreateRotationDegrees(180, x, y);
                        break;
                    case FieldOrientation.Rotated270:
                        matrix = SKMatrix.CreateRotationDegrees(270, x, y);
                        break;
                }
            }

            return matrix;
        }

        /// <summary>
        /// Place local bar geometry at (x,y) with the non-field-origin y-adjust and rotation. Optional
        /// <paramref name="scaleX"/>/<paramref name="scaleY"/> are applied to the geometry before
        /// translation (and before rotation), so a caller may emit bars at module resolution and let the
        /// transform do the final pixel sizing.
        /// </summary>
        protected void DrawBarcode(SKPath barsLocal, float x, float y, int barcodeWidth, int barcodeHeight, bool useFieldOrigin, FieldOrientation fieldOrientation, float scaleX = 1f, float scaleY = 1f)
        {
            float drawY = y;
            if (!useFieldOrigin)
            {
                drawY -= barcodeHeight;
                if (drawY < 0) drawY = 0;
            }

            SKMatrix rot = GetRotationMatrix(x, y, barcodeWidth, barcodeHeight, useFieldOrigin, fieldOrientation);
            bool hasRot = rot != SKMatrix.Empty;
            if (hasRot) this.context.PushTransform(rot);
            this.context.PushTransform(SKMatrix.CreateTranslation(x, drawY));
            bool scaled = scaleX != 1f || scaleY != 1f;
            if (scaled) this.context.PushTransform(SKMatrix.CreateScale(scaleX, scaleY));
            this.context.AddBlack(barsLocal);
            if (scaled) this.context.Pop();
            this.context.Pop();
            if (hasRot) this.context.Pop();
        }

        /// <summary>Centered single-string interpretation line, mirroring <c>DrawInterpretationLine</c>.</summary>
        protected void DrawInterpretationLine(string interpretation, SKFont font, float x, float y, int barcodeWidth, int barcodeHeight, bool useFieldOrigin, FieldOrientation fieldOrientation, bool printAboveCode, DrawerOptions options)
        {
            SKMatrix rot = GetRotationMatrix(x, y, barcodeWidth, barcodeHeight, useFieldOrigin, fieldOrientation);
            bool hasRot = rot != SKMatrix.Empty;
            if (hasRot) this.context.PushTransform(rot);

            SKRect textBounds = SkTextRenderer.MeasureTightBounds(font, interpretation, 1f);
            float penX = x + (barcodeWidth - textBounds.Width) / 2;
            float yy = y;
            if (!useFieldOrigin)
            {
                yy -= barcodeHeight;
                if (yy < 0) yy = 0;
            }

            float margin = Math.Max((SkTextRenderer.LineSpacing(font) - textBounds.Height) / 2, MIN_LABEL_MARGIN);
            float baselineY = printAboveCode ? yy - margin : yy + barcodeHeight + textBounds.Height + margin;

            SKPath g = SkTextRenderer.BuildGeometryGlyphRun(font, interpretation, 1f, new SKPoint(penX, baselineY));
            this.context.AddBlack(g);

            if (hasRot) this.context.Pop();
        }

        /// <summary>
        /// Per-digit interpretation line with guard bars (EAN-13 / UPC-A / UPC-E). <paramref name="extraModulesAfter"/>
        /// returns the extra module gap inserted after digit index i.
        /// </summary>
        protected void DrawDigitInterpretationLine(
            bool[] guardArray, bool[] maskArray, string interpretation, SKFont font,
            float x, float y, int barcodeWidth, int barcodeHeight, bool useFieldOrigin,
            FieldOrientation fieldOrientation, int moduleWidth, DrawerOptions options,
            Func<int, int> extraModulesAfter)
        {
            SKMatrix rot = GetRotationMatrix(x, y, barcodeWidth, barcodeHeight, useFieldOrigin, fieldOrientation);
            bool hasRot = rot != SKMatrix.Empty;
            if (hasRot) this.context.PushTransform(rot);

            SKRect textBounds = SkTextRenderer.MeasureTightBounds(font, interpretation, 1f);
            float yy = y;
            if (!useFieldOrigin)
            {
                yy -= barcodeHeight;
                if (yy < 0) yy = 0;
            }

            float margin = Math.Max((SkTextRenderer.LineSpacing(font) - textBounds.Height) / 2, MIN_LABEL_MARGIN);
            int spacing = moduleWidth * 7;

            int guardHeight = (int)(margin + textBounds.Height / 2);
            SKPath guard = maskArray == null
                ? BoolArrayToPath(guardArray, guardHeight, moduleWidth)
                : BoolArrayWithMaskToPath(guardArray, maskArray, guardHeight, moduleWidth);

            this.context.PushTransform(SKMatrix.CreateTranslation(x, yy + barcodeHeight));
            this.context.AddBlack(guard);
            this.context.Pop();

            float cx = x;
            float baselineY = yy + barcodeHeight + textBounds.Height + margin;
            for (int i = 0; i < interpretation.Length; i++)
            {
                string digit = interpretation[i].ToString();
                SKRect digitBounds = SkTextRenderer.MeasureTightBounds(font, digit, 1f);
                float penX = cx - (spacing + digitBounds.Width) / 2 - moduleWidth;
                SKPath g = SkTextRenderer.BuildGeometryGlyphRun(font, digit, 1f, new SKPoint(penX, baselineY));
                this.context.AddBlack(g);

                cx += spacing + extraModulesAfter(i) * moduleWidth;
            }

            if (hasRot) this.context.Pop();
        }

        /// <summary>Copy of <c>BarcodeDrawerBase.AdjustWidths</c> (pure bool[] expansion).</summary>
        protected static bool[] AdjustWidths(bool[] array, int wide, int narrow)
        {
            List<bool> result = new List<bool>();
            bool last = true;
            int count = 0;
            foreach (bool current in array)
            {
                if (current != last)
                {
                    result.AddRange(Enumerable.Repeat(last, count == 1 ? narrow : wide));
                    last = current;
                    count = 0;
                }

                count += 1;
            }

            result.AddRange(Enumerable.Repeat(last, narrow));
            return result.ToArray();
        }
    }
}
