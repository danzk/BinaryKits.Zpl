using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>GraphicBoxElementDrawer</c> (<c>^GB</c>). The border is built as a filled ring
    /// (outer shape with an inner counter-wound hole) instead of a centered stroke, so it stays exactly
    /// within the box bounds.
    /// </summary>
    public class GraphicBoxElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicBox;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicBox box && box.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicBox box && box.LineColor == LineColor.White;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicBox graphicBox)
            {
                return currentPosition;
            }

            int border = graphicBox.BorderThickness;
            int width1 = graphicBox.Width;
            int height1 = graphicBox.Height;

            // Mirror the Skia clamping of border/size.
            if (border > width1) width1 = border;
            if (border > height1) height1 = border;
            if (border > width1 / 2 && width1 <= height1) border = (int)Math.Ceiling(width1 / 2f);
            if (border > height1 / 2 && height1 <= width1) border = (int)Math.Ceiling(height1 / 2f);
            if (border < 1) border = 1;

            double baseX = graphicBox.PositionX;
            double baseY = graphicBox.PositionY;
            if (graphicBox.UseDefaultPosition)
            {
                baseX = currentPosition.X;
                baseY = currentPosition.Y;
            }

            // FieldTypeset (^FT) anchors the box bottom-up.
            double top = baseY;
            if (graphicBox.FieldTypeset != null)
            {
                top = baseY - height1;
                if (top < 0) top = 0;
            }

            // cornerRadius matches the Skia formula: the thinnest (1px) stroke pass defines the visible
            // outer contour, so the effective outer corner radius is ~cornerRadius; the inner hole keeps
            // the centered-stroke radius.
            double cornerRadius = (graphicBox.CornerRounding / 8.0) * (Math.Min(width1, height1) / 2.0);
            double rOuter = cornerRadius;
            double rInner = cornerRadius == 0 ? 0 : Math.Max(0, cornerRadius - border / 2.0);

            var outerRect = SKRect.Create((float)baseX, (float)top, width1, height1);

            SKPath borderGeometry;
            double iw = width1 - 2.0 * border;
            double ih = height1 - 2.0 * border;
            if (iw <= 0 || ih <= 0)
            {
                borderGeometry = SkPathOps.Rectangle(outerRect, (float)rOuter, (float)rOuter); // solid bar
            }
            else
            {
                var innerRect = SKRect.Create((float)(baseX + border), (float)(top + border), (float)iw, (float)ih);
                borderGeometry = SkPathOps.MakeRectRing(outerRect, (float)rOuter, innerRect, (float)rInner);
            }

            // Reverse always feeds the black bucket (the orchestrator decides background vs white XOR).
            if (!graphicBox.ReversePrint && graphicBox.LineColor == LineColor.White)
            {
                this.context.AddWhite(borderGeometry);
            }
            else
            {
                this.context.AddBlack(borderGeometry);
            }

            return CalculateNextDefaultPosition((float)baseX, (float)baseY, width1, height1, graphicBox.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
