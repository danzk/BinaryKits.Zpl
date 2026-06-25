using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>Geometry port of <c>GraphicCircleElementDrawer</c> (<c>^GC</c>). Border = outer disk with an inner counter-wound hole.</summary>
    public class GraphicCircleElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicCircle;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicCircle circle && circle.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicCircle circle && circle.LineColor == LineColor.White;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicCircle graphicCircle)
            {
                return currentPosition;
            }

            double radius = graphicCircle.Diameter / 2.0;
            double border = graphicCircle.BorderThickness;
            if (border > radius)
            {
                border = radius;
            }

            double baseX = graphicCircle.PositionX;
            double baseY = graphicCircle.PositionY;
            if (graphicCircle.UseDefaultPosition)
            {
                baseX = currentPosition.X;
                baseY = currentPosition.Y;
            }

            double cx = baseX + radius;
            double cy = baseY + radius;
            if (graphicCircle.FieldTypeset != null)
            {
                cy -= graphicCircle.Diameter;
                if (cy < radius)
                {
                    cy = radius;
                }
            }

            var center = new SKPoint((float)cx, (float)cy);

            SKPath borderGeometry;
            double innerRadius = radius - border;
            if (innerRadius <= 0)
            {
                borderGeometry = SkPathOps.Ellipse(center, (float)radius, (float)radius);
            }
            else
            {
                borderGeometry = SkPathOps.MakeEllipseRing(center, (float)radius, (float)radius, (float)innerRadius, (float)innerRadius);
            }

            if (!graphicCircle.ReversePrint && graphicCircle.LineColor == LineColor.White)
            {
                this.context.AddWhite(borderGeometry);
            }
            else
            {
                this.context.AddBlack(borderGeometry);
            }

            return CalculateNextDefaultPosition((float)baseX, (float)baseY, graphicCircle.Diameter, graphicCircle.Diameter, graphicCircle.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
