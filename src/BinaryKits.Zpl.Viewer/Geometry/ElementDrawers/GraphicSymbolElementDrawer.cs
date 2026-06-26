using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>GraphicSymbolElementDrawer</c> (<c>^GS</c>). Renders a single glyph from the
    /// embedded graphic-symbol font (<c>ZplGS</c>) as baseline-anchored geometry.
    /// </summary>
    public class GraphicSymbolElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element.GetType() == typeof(ZplGraphicSymbol);

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplGraphicSymbol graphicSymbol)
            {
                return currentPosition;
            }

            float x = graphicSymbol.PositionX;
            float y = graphicSymbol.PositionY;
            FieldJustification fieldJustification = FieldJustification.None;

            if (graphicSymbol.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            (float fontSize, float scaleX) = FontScale.GetFontScaling("GS", graphicSymbol.Height, graphicSymbol.Width, printDensityDpmm);

            // remove incorrect scaling (mirrors Skia)
            fontSize /= 1.1f;
            float emSize = fontSize * 1.25f;

            SKTypeface typeface = options.FontManager.TypefaceGS;
            using var skFont = new SKFont(typeface, emSize);

            string displayText = $"{(char)graphicSymbol.Character}";
            float totalWidth = TextRenderer.MeasureAdvance(skFont, displayText, scaleX);
            SKRect textBounds = TextRenderer.MeasureTightBounds(skFont, displayText, scaleX);

            bool pushed = false;
            if (graphicSymbol.FieldOrigin != null)
            {
                switch (graphicSymbol.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(SKMatrix.CreateRotationDegrees(90, x + fontSize / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(SKMatrix.CreateRotationDegrees(180, x + textBounds.Width / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(SKMatrix.CreateRotationDegrees(270, x + textBounds.Width / 2, y + textBounds.Width / 2));
                        break;
                }

                fieldJustification = graphicSymbol.FieldOrigin.FieldJustification;
            }
            else
            {
                switch (graphicSymbol.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(SKMatrix.CreateRotationDegrees(90, x, y));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(SKMatrix.CreateRotationDegrees(180, x, y));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(SKMatrix.CreateRotationDegrees(270, x, y));
                        break;
                }

                fieldJustification = graphicSymbol.FieldTypeset.FieldJustification;
            }

            if (graphicSymbol.FieldTypeset == null)
            {
                y += fontSize;
            }

            float originX = x;
            if (fieldJustification == FieldJustification.Right)
            {
                originX = x - totalWidth;
            }

            SKPath geometry = TextRenderer.BuildGeometryGlyphRun(skFont, displayText, scaleX, new SKPoint(originX, y));
            this.context.AddBlack(geometry);

            if (pushed)
            {
                this.context.Pop();
            }

            return CalculateNextDefaultPosition(x, y, totalWidth, textBounds.Height, false, graphicSymbol.FieldOrientation, currentPosition);
        }

        private bool Push(SKMatrix matrix)
        {
            this.context.PushTransform(matrix);
            return true;
        }
    }
}
