using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>TextFieldElementDrawer</c> (<c>^FD</c> text). Text is baseline-anchored
    /// geometry, reproducing Skia's <c>DrawShapedText</c> baseline math (<c>y += capHeight</c>), rotation
    /// pivots and Left/Right/Auto justification. Font resolution reuses the existing <c>FontManager</c>
    /// (pixel fonts + font-0 condensed stack are out of scope), and the horizontal scale is applied as a
    /// geometry transform about the baseline (not baked into the <see cref="SKFont"/>).
    /// </summary>
    public class TextFieldElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element.GetType() == typeof(ZplTextField);

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplTextField textField && textField.ReversePrint;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplTextField textField)
            {
                return currentPosition;
            }

            float x = textField.PositionX;
            float y = textField.PositionY;
            FieldJustification fieldJustification = FieldJustification.None;

            if (textField.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            ZplFont font = textField.Font;
            (float fontSize, float scaleX) = FontScale.GetFontScaling(font.FontName, font.FontHeight, font.FontWidth, printDensityDpmm);

            SKTypeface typeface = options.FontManager.FontLoader(font.FontName);
            using var skFont = new SKFont(typeface, fontSize);   // scaleX applied via geometry, not the font

            string displayText = textField.Text;
            if (textField.HexadecimalIndicator is char hexIndicator)
            {
                displayText = displayText.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (font.FontName == "0")
            {
                if (options.ReplaceDashWithEnDash)
                {
                    displayText = displayText.Replace("-", " – ");
                }

                if (options.ReplaceUnderscoreWithEnSpace)
                {
                    displayText = displayText.Replace('_', ' ');
                }
            }

            float capHeight = TextRenderer.CapHeight(skFont);
            float totalWidth = TextRenderer.MeasureAdvance(skFont, displayText, scaleX);
            SKRect tightBounds = TextRenderer.MeasureTightBounds(skFont, displayText, scaleX);

            // Rotation pivots use the pre-baseline x/y (mirrors the Skia ordering).
            bool pushed = false;
            if (textField.FieldOrigin != null)
            {
                switch (font.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(SKMatrix.CreateRotationDegrees(90, x + fontSize / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(SKMatrix.CreateRotationDegrees(180, x + tightBounds.Width / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(SKMatrix.CreateRotationDegrees(270, x + tightBounds.Width / 2, y + tightBounds.Width / 2));
                        break;
                }

                fieldJustification = textField.FieldOrigin.FieldJustification;
            }
            else
            {
                switch (font.FieldOrientation)
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

                fieldJustification = textField.FieldTypeset.FieldJustification;
            }

            if (textField.FieldTypeset == null)
            {
                y += capHeight;
            }

            // Left/Right/Auto -> emulate alignment by shifting the (left-origin) baseline.
            float originX = x;
            if (fieldJustification == FieldJustification.Right)
            {
                originX = x - totalWidth;
            }
            else if (fieldJustification == FieldJustification.Auto && IsRightToLeft(displayText))
            {
                originX = x - totalWidth;
            }

            this.context.AddText(new TextRun(displayText, typeface, fontSize, scaleX, new SKPoint(originX, y)));

            if (pushed)
            {
                this.context.Pop();
            }

            return CalculateNextDefaultPosition(x, y, totalWidth, tightBounds.Height, false, font.FieldOrientation, currentPosition);
        }

        private bool Push(SKMatrix matrix)
        {
            this.context.PushTransform(matrix);
            return true;
        }

        internal static bool IsRightToLeft(string text)
        {
            foreach (char c in text)
            {
                if ((c >= 0x0590 && c <= 0x05FF) || // Hebrew
                    (c >= 0x0600 && c <= 0x06FF) || // Arabic
                    (c >= 0x0700 && c <= 0x074F) || // Syriac
                    (c >= 0x0750 && c <= 0x077F) || // Arabic Supplement
                    (c >= 0x08A0 && c <= 0x08FF) || // Arabic Extended-A
                    (c >= 0xFB1D && c <= 0xFDFF) || // Hebrew/Arabic presentation forms
                    (c >= 0xFE70 && c <= 0xFEFF))   // Arabic presentation forms-B
                {
                    return true;
                }
            }

            return false;
        }
    }
}
