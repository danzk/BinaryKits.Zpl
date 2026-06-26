using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Geometry construction helpers over <see cref="SKPath"/>. All paths use
    /// <see cref="SKPathFillType.Winding"/> (non-zero) fill so the two-contour "ring" trick (outer
    /// clockwise + inner counter-clockwise) cuts a hole under non-zero winding.
    /// </summary>
    internal static class PathOps
    {
        /// <summary>A (possibly rounded) rectangle as a filled <see cref="SKPath"/>.</summary>
        public static SKPath Rectangle(SKRect rect, float rx, float ry)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            if (rx > 0f || ry > 0f)
            {
                path.AddRoundRect(rect, rx, ry, SKPathDirection.Clockwise);
            }
            else
            {
                path.AddRect(rect, SKPathDirection.Clockwise);
            }

            return path;
        }

        /// <summary>An ellipse from a centre and radii.</summary>
        public static SKPath Ellipse(SKPoint center, float radiusX, float radiusY)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            path.AddOval(FromCenter(center, radiusX, radiusY), SKPathDirection.Clockwise);
            return path;
        }

        /// <summary>
        /// A rounded-rectangle "ring" (outer rect minus inner rect) as one path with two opposite-winding
        /// contours — outer clockwise, inner counter-clockwise. Under non-zero fill the windings cancel
        /// inside the inner rect, producing the hole; because the hole comes from contour winding (not a
        /// FillRule applied across separate children) it survives a boolean combine and any nesting.
        /// </summary>
        public static SKPath MakeRectRing(SKRect outer, float outerRadius, SKRect inner, float innerRadius)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            AddRect(path, outer, outerRadius, SKPathDirection.Clockwise);
            AddRect(path, inner, innerRadius, SKPathDirection.CounterClockwise);
            return path;
        }

        /// <summary>Elliptical equivalent of <see cref="MakeRectRing"/>.</summary>
        public static SKPath MakeEllipseRing(SKPoint center, float outerRx, float outerRy, float innerRx, float innerRy)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            path.AddOval(FromCenter(center, outerRx, outerRy), SKPathDirection.Clockwise);
            path.AddOval(FromCenter(center, innerRx, innerRy), SKPathDirection.CounterClockwise);
            return path;
        }

        /// <summary>
        /// A horizontal-only scale about <paramref name="about"/>: x' = x*sx + about.X*(1-sx), y unchanged.
        /// </summary>
        public static SKMatrix HorizontalScale(float scaleX, SKPoint about)
            => SKMatrix.CreateScale(scaleX, 1f, about.X, about.Y);

        /// <summary>Boolean combine of two paths (used only on the <c>^FR</c> path).</summary>
        public static SKPath Combine(SKPath a, SKPath b, SKPathOp op) => a.Op(b, op);

        private static void AddRect(SKPath path, SKRect rect, float radius, SKPathDirection direction)
        {
            if (radius > 0f)
            {
                path.AddRoundRect(rect, radius, radius, direction);
            }
            else
            {
                path.AddRect(rect, direction);
            }
        }

        private static SKRect FromCenter(SKPoint center, float rx, float ry)
            => new SKRect(center.X - rx, center.Y - ry, center.X + rx, center.Y + ry);
    }
}
