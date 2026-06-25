using System;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Analyzer.Symologies;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>MaxiCodeElementDrawer</c> (<c>^BD</c>). MaxiCode is not in ZXing; the hexagon
    /// pattern + three concentric finder rings are reproduced as one <see cref="SKPath"/>, mirroring the
    /// ISO/IEC 16023 dimensions exactly. Two aliased scan converters disagree on the non-axis-aligned
    /// hexagons by a sub-pixel silhouette, so exact-pixel parity is not expected for this symbology.
    /// </summary>
    public class MaxiCodeElementDrawer : GeometryBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplMaxiCode;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplMaxiCode maxiCode)
            {
                return currentPosition;
            }

            float x = maxiCode.PositionX;
            float y = maxiCode.PositionY;
            if (maxiCode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = maxiCode.Content;
            if (maxiCode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            bool[] data = MaxiCodeSymbology.Encode(content, maxiCode.Mode);

            (SKPath geometry, int width, int height) = BuildMaxiCode(data, printDensityDpmm);
            DrawBarcode(geometry, x, y, width, height, maxiCode.FieldOrigin != null, maxiCode.FieldOrientation);

            return CalculateNextDefaultPosition(x, y, width, height, maxiCode.FieldOrigin != null, maxiCode.FieldOrientation, currentPosition);
        }

        private static (SKPath geometry, int width, int height) BuildMaxiCode(bool[] data, int dpmm)
        {
            // ISO/IEC 16023:2000 pp. 16, 38-40 — dimensions copied verbatim from the Skia drawer.
            double L, H, W, V, X, Y;
            double gX, gV;
            (double dx, double dy)[] pattern;
            double xoff, yoff;

            if (dpmm == 8)
            {
                W = 7; V = 8; X = W; Y = 6;
                gX = 1; gV = 1;
                xoff = 4; yoff = 2;
                pattern = new[]
                {
                    (0d, 3d), (2d, 2d), (1.5d, 0d), (2d, -2d),
                    (0d, -3d), (-2d, -2d), (-1.5d, 0d),
                };
                L = 29 * W;
                H = 32 * Y;
            }
            else if (dpmm == 12)
            {
                W = 10; V = 12; X = W; Y = 9;
                gX = 2; gV = 2;
                xoff = 5; yoff = 3;
                pattern = new[]
                {
                    (0d, 4d), (3d, 3d), (1.5d, 0d), (3d, -3d),
                    (0d, -4d), (-3d, -3d), (-1.5d, 0d),
                };
                L = 29 * W;
                H = 32 * Y;
            }
            else
            {
                L = 25.50 * dpmm;
                W = L / 29;
                V = 1.1547 * W;
                X = W;
                Y = 0.866 * W;
                H = 32 * Y;
                gX = dpmm / 6.0;
                gV = 1.1547 * gX;
                xoff = W / 2;
                yoff = (V - gV) / 4;

                double hexW = (X - gX) / 2;
                double hexH = (V - gV) / 4;
                pattern = new[]
                {
                    (0d, hexH * 2), (hexW, hexH), (hexW, -hexH),
                    (0d, -hexH * 2), (-hexW, -hexH),
                };
            }

            double r1 = 0.51 * dpmm, r2 = 1.18 * dpmm, r3 = 1.86 * dpmm;
            double r4 = 2.53 * dpmm, r5 = 3.20 * dpmm, r6 = 3.87 * dpmm;

            int width = (int)Math.Ceiling(L + X - gX);
            int height = (int)Math.Ceiling(H + V - gV);

            // Hexagons. Modules do not overlap, so each filled contour is independent under non-zero fill.
            var combined = new SKPath { FillType = SKPathFillType.Winding };
            int idx = 0;
            for (int j = 0; j < 33; j++)
            {
                for (int i = 0; i < 30 - j % 2; i++)
                {
                    bool on = idx < data.Length && data[idx];
                    idx++;
                    if (!on)
                    {
                        continue;
                    }

                    double cx = i * W + j % 2 * xoff;
                    double cy = j * Y + yoff;
                    combined.MoveTo((float)cx, (float)cy);
                    foreach ((double dx, double dy) in pattern)
                    {
                        cx += dx;
                        cy += dy;
                        combined.LineTo((float)cx, (float)cy);
                    }

                    combined.Close();
                }
            }

            // Finder pattern: three annuli (R1-R2, R3-R4, R5-R6).
            double finderX = 14 * W + (X - gX) / 2;
            double finderY = 16 * Y + (V - gV) / 2;
            var center = new SKPoint((float)finderX, (float)finderY);

            combined.AddPath(Annulus(center, r2, r1));
            combined.AddPath(Annulus(center, r4, r3));
            combined.AddPath(Annulus(center, r6, r5));

            return (combined, width, height);
        }

        private static SKPath Annulus(SKPoint center, double outerRadius, double innerRadius)
            => SkPathOps.MakeEllipseRing(center, (float)outerRadius, (float)outerRadius, (float)innerRadius, (float)innerRadius);
    }
}
