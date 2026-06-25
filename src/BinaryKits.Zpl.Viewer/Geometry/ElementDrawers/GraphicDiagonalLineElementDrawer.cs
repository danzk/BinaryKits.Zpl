using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>GraphicDiagonalLineElementDrawer</c> (<c>^GD</c>). The thick diagonal is a
    /// filled parallelogram (an <see cref="SKPath"/> with MoveTo/LineTo/Close).
    /// </summary>
    public class GraphicDiagonalLineElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicDiagonalLine;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicDiagonalLine line && line.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicDiagonalLine line && line.LineColor == LineColor.White;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicDiagonalLine graphicLine)
            {
                return currentPosition;
            }

            int border = graphicLine.BorderThickness;
            int width = graphicLine.Width;
            int height = graphicLine.Height;

            double x = graphicLine.PositionX;
            double y = graphicLine.PositionY;
            if (graphicLine.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (graphicLine.FieldTypeset != null)
            {
                y -= height;
                if (y < 0) y = 0;
            }

            float fx = (float)x;
            float fy = (float)y;

            var geometry = new SKPath { FillType = SKPathFillType.Winding };
            if (graphicLine.RightLeaningDiagonal)
            {
                geometry.MoveTo(fx, fy + height);
                geometry.LineTo(fx + border, fy + height);
                geometry.LineTo(fx + border + width, fy);
                geometry.LineTo(fx + width, fy);
                geometry.Close();
            }
            else
            {
                geometry.MoveTo(fx, fy);
                geometry.LineTo(fx + border, fy);
                geometry.LineTo(fx + border + width, fy + height);
                geometry.LineTo(fx + width, fy + height);
                geometry.Close();
            }

            if (!graphicLine.ReversePrint && graphicLine.LineColor == LineColor.White)
            {
                this.context.AddWhite(geometry);
            }
            else
            {
                this.context.AddBlack(geometry);
            }

            return CalculateNextDefaultPosition(fx, fy, width, height, graphicLine.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
