using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    /// <summary>
    /// Geometry port of <c>FieldBlockElementDrawer</c> (<c>^FB</c>). Word-wraps, lays out multiple
    /// baseline lines and applies per-line justification. Font resolution reuses the existing
    /// <c>FontManager</c> (pixel fonts + font-0 condense out of scope); the horizontal scale is applied
    /// as a geometry transform.
    /// </summary>
    public class FieldBlockElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplFieldBlock;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplFieldBlock fieldBlock && fieldBlock.ReversePrint;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplFieldBlock fieldBlock)
            {
                return currentPosition;
            }

            ZplFont font = fieldBlock.Font;
            (float fontSize, float scaleX) = FontScale.GetFontScaling(font.FontName, font.FontHeight, font.FontWidth, printDensityDpmm);

            SKTypeface typeface = options.FontManager.FontLoader(font.FontName);
            using var skFont = new SKFont(typeface, fontSize);

            string text = fieldBlock.Text;
            if (fieldBlock.HexadecimalIndicator is char hexIndicator)
            {
                text = text.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (font.FontName == "0")
            {
                if (options.ReplaceDashWithEnDash)
                {
                    text = text.Replace("-", " – ");
                }

                if (options.ReplaceUnderscoreWithEnSpace)
                {
                    text = text.Replace('_', ' ');
                }
            }

            float capHeight = TextRenderer.CapHeight(skFont);

            float x = fieldBlock.PositionX;
            float y = fieldBlock.PositionY + capHeight;
            if (fieldBlock.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y + capHeight;
            }

            List<string> textLines = WordWrap(text, skFont, scaleX, fieldBlock.Width);
            int hangingIndent = 0;
            float lineHeight = fontSize + fieldBlock.LineSpace;

            // The ZPL printer does not include trailing line spacing in the total height.
            float totalHeight = lineHeight * fieldBlock.MaxLineCount - fieldBlock.LineSpace;

            if (fieldBlock.FieldTypeset != null)
            {
                totalHeight = lineHeight * (fieldBlock.MaxLineCount - 1) + capHeight;
                y -= totalHeight;
            }

            bool pushed = false;
            if (fieldBlock.FieldOrigin != null)
            {
                switch (font.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(SKMatrix.CreateRotationDegrees(90, fieldBlock.PositionX + totalHeight / 2, fieldBlock.PositionY + totalHeight / 2));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(SKMatrix.CreateRotationDegrees(180, fieldBlock.PositionX + fieldBlock.Width / 2f, fieldBlock.PositionY + totalHeight / 2));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(SKMatrix.CreateRotationDegrees(270, fieldBlock.PositionX + fieldBlock.Width / 2f, fieldBlock.PositionY + fieldBlock.Width / 2f));
                        break;
                }
            }
            else
            {
                switch (font.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(SKMatrix.CreateRotationDegrees(90, fieldBlock.PositionX, fieldBlock.PositionY));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(SKMatrix.CreateRotationDegrees(180, fieldBlock.PositionX, fieldBlock.PositionY));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(SKMatrix.CreateRotationDegrees(270, fieldBlock.PositionX, fieldBlock.PositionY));
                        break;
                }
            }

            foreach (string textLine in textLines)
            {
                x = fieldBlock.PositionX + hangingIndent;

                SKRect textBounds = TextRenderer.MeasureTightBounds(skFont, textLine, scaleX);
                float diff = fieldBlock.Width - textBounds.Width;

                switch (fieldBlock.TextJustification)
                {
                    case TextJustification.Center:
                        x += diff / 2 - textBounds.Left;
                        break;
                    case TextJustification.Right:
                        x += diff - textBounds.Left * 2;
                        hangingIndent = -fieldBlock.HangingIndent;
                        break;
                    case TextJustification.Left:
                    case TextJustification.Justified:
                    default:
                        hangingIndent = fieldBlock.HangingIndent;
                        break;
                }

                SKPath geometry = TextRenderer.BuildGeometryGlyphRun(skFont, textLine, scaleX, new SKPoint(x, y));
                this.context.AddBlack(geometry);
                y += lineHeight;
            }

            if (pushed)
            {
                this.context.Pop();
            }

            return CalculateNextDefaultPosition(fieldBlock.PositionX, fieldBlock.PositionY, fieldBlock.Width, totalHeight, fieldBlock.FieldOrigin != null, font.FieldOrientation, currentPosition);
        }

        private bool Push(SKMatrix matrix)
        {
            this.context.PushTransform(matrix);
            return true;
        }

        private static List<string> WordWrap(string text, SKFont font, float scaleX, int maxWidth)
        {
            float spaceWidth = TextRenderer.MeasureAdvance(font, " ", scaleX);
            List<string> lines = new List<string>();

            Stack<string> words = new Stack<string>(text.Split(new[] { ' ' }, StringSplitOptions.None).AsEnumerable().Reverse());
            StringBuilder line = new StringBuilder();
            float width = 0;
            while (words.Count != 0)
            {
                string word = words.Pop();
                if (word.Contains(@"\&"))
                {
                    string[] subwords = word.Split(new[] { @"\&" }, 2, StringSplitOptions.None);
                    word = subwords[0];
                    words.Push(subwords[1]);
                    float wordWidth = TextRenderer.MeasureAdvance(font, word, scaleX);
                    if (width + wordWidth <= maxWidth)
                    {
                        line.Append(word);
                        lines.Add(line.ToString());
                        line = new StringBuilder();
                        width = 0;
                    }
                    else
                    {
                        if (line.Length > 0)
                        {
                            lines.Add(line.ToString().Trim());
                        }

                        lines.Add(word);
                        line = new StringBuilder();
                        width = 0;
                    }
                }
                else
                {
                    float wordWidth = TextRenderer.MeasureAdvance(font, word, scaleX);
                    if (width + wordWidth <= maxWidth)
                    {
                        line.Append(word + " ");
                        width += wordWidth + spaceWidth;
                    }
                    else
                    {
                        if (line.Length > 0)
                        {
                            lines.Add(line.ToString().Trim());
                        }

                        line = new StringBuilder(word + " ");
                        width = wordWidth + spaceWidth;
                    }
                }
            }

            lines.Add(line.ToString().Trim());
            return lines;
        }
    }
}
