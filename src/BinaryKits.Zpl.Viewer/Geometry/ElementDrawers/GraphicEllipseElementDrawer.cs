using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>GraphicEllipseElementDrawer</c> (<c>^GE</c>). Border = outer ellipse with an
    /// inner counter-wound hole, honouring Field Reverse (<c>^FR</c>) and white line colour.
    /// </summary>
    public class GraphicEllipseElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicEllipse;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicEllipse ellipse && ellipse.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicEllipse ellipse && ellipse.LineColor == LineColor.White;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicEllipse graphicEllipse)
            {
                return currentPosition;
            }

            double border = graphicEllipse.BorderThickness;
            double width = graphicEllipse.Width;
            double height = graphicEllipse.Height;

            if (width < border * 2) border = width / 2;
            if (height < border * 2) border = height / 2;

            double x = graphicEllipse.PositionX;
            double y = graphicEllipse.PositionY;
            if (graphicEllipse.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (graphicEllipse.FieldTypeset != null)
            {
                y -= height;
                if (y < 0) y = 0;
            }

            var center = new SKPoint((float)(x + width / 2.0), (float)(y + height / 2.0));
            double outerRx = width / 2.0;
            double outerRy = height / 2.0;

            SKPath borderGeometry;
            double innerRx = outerRx - border;
            double innerRy = outerRy - border;
            if (innerRx <= 0 || innerRy <= 0)
            {
                borderGeometry = SkPathOps.Ellipse(center, (float)outerRx, (float)outerRy);
            }
            else
            {
                borderGeometry = SkPathOps.MakeEllipseRing(center, (float)outerRx, (float)outerRy, (float)innerRx, (float)innerRy);
            }

            // Reverse always feeds the black bucket (the orchestrator decides background vs white XOR).
            if (!graphicEllipse.ReversePrint && graphicEllipse.LineColor == LineColor.White)
            {
                this.context.AddWhite(borderGeometry);
            }
            else
            {
                this.context.AddBlack(borderGeometry);
            }

            // Skia advances using the half-border-adjusted x/y; replicate.
            double halfBorder = border / 2.0;
            return CalculateNextDefaultPosition((float)(x + halfBorder), (float)(y + halfBorder), (float)width, (float)height, graphicEllipse.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
