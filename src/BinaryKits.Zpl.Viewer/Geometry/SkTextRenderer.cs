using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Produces text as filled <see cref="SKPath"/> geometry positioned at a baseline origin. Each glyph
    /// outline comes from <see cref="SKFont.GetGlyphPath(ushort)"/>, offset by the running per-glyph advance
    /// from <see cref="SKFont.GetGlyphWidths(System.ReadOnlySpan{ushort})"/> — the font design advances (NOT
    /// HarfBuzz shaping), matching the legacy text drawers' positioning.
    ///
    /// <para>Building text as geometry (rather than <c>DrawText</c>) is what lets <c>^FR</c> boolean-compose
    /// over text and keeps it crisp vector in a PDF.</para>
    /// </summary>
    public static class SkTextRenderer
    {
        /// <summary>Line spacing — Skia's recommended spacing at the font size (barcode interpretation margins).</summary>
        public static float LineSpacing(SKFont font) => font.Spacing;

        /// <summary>Cap height of "X" (independent of scaleX).</summary>
        public static float CapHeight(SKFont font)
        {
            using (SKPath g = BuildGeometryGlyphRun(font, "X", 1f, new SKPoint(0, 0)))
            {
                return g.IsEmpty ? font.Size : g.TightBounds.Height;
            }
        }

        /// <summary>Advance width of text, scaled by scaleX.</summary>
        public static float MeasureAdvance(SKFont font, string text, float scaleX)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            ushort[] glyphs = font.GetGlyphs(text);
            float[] widths = font.GetGlyphWidths(glyphs);
            float sum = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                sum += widths[i];
            }

            return sum * scaleX;
        }

        /// <summary>Tight bounds of text relative to a (0,0) baseline (Left/Width feed FieldBlock justification).</summary>
        public static SKRect MeasureTightBounds(SKFont font, string text, float scaleX)
        {
            using (SKPath g = BuildGeometryGlyphRun(font, text, scaleX, new SKPoint(0, 0)))
            {
                return g.IsEmpty ? SKRect.Empty : g.TightBounds;
            }
        }

        /// <summary>
        /// Build filled text geometry at a baseline origin: accumulate each glyph's outline path offset by
        /// the running advance, then apply the horizontal scale about the baseline.
        /// </summary>
        public static SKPath BuildGeometryGlyphRun(SKFont font, string text, float scaleX, SKPoint baselineOrigin)
        {
            var outline = new SKPath { FillType = SKPathFillType.Winding };
            if (string.IsNullOrEmpty(text))
            {
                return outline;
            }

            ushort[] glyphs = font.GetGlyphs(text);
            float[] widths = font.GetGlyphWidths(glyphs);

            float penX = baselineOrigin.X;
            for (int i = 0; i < glyphs.Length; i++)
            {
                using (SKPath glyphPath = font.GetGlyphPath(glyphs[i]))
                {
                    if (glyphPath != null && !glyphPath.IsEmpty)
                    {
                        using (var positioned = new SKPath(glyphPath))
                        {
                            positioned.Transform(SKMatrix.CreateTranslation(penX, baselineOrigin.Y));
                            outline.AddPath(positioned);
                        }
                    }
                }

                penX += widths[i];
            }

            if (scaleX != 1f)
            {
                outline.Transform(SkPathOps.HorizontalScale(scaleX, baselineOrigin));
            }

            return outline;
        }
    }
}
