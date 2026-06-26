using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Produces text as filled <see cref="SKPath"/> geometry positioned at a baseline origin via
    /// <see cref="SKFont.GetTextPath(string, SKPoint)"/> — glyph outlines composed by the font's design
    /// advances (NOT HarfBuzz shaping), matching the legacy text drawers' positioning.
    ///
    /// <para>Building text as geometry (rather than <c>DrawText</c>) is what lets <c>^FR</c> boolean-compose
    /// over text and keeps it crisp vector in a PDF/SVG. Filled outlines render a touch heavier than the glyph
    /// blitter's gamma-corrected antialiasing — an inherent, print-imperceptible edge difference.</para>
    /// </summary>
    public static class TextRenderer
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
        /// Build filled text geometry at a baseline origin: one <see cref="SKFont.GetTextPath(string, SKPoint)"/>
        /// composes the whole run (glyph outlines positioned by the font's advances), then the horizontal scale
        /// is applied about the baseline. Pixel-identical to accumulating <c>GetGlyphPath</c> per glyph.
        /// </summary>
        public static SKPath BuildGeometryGlyphRun(SKFont font, string text, float scaleX, SKPoint baselineOrigin)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new SKPath { FillType = SKPathFillType.Winding };
            }

            SKPath outline = font.GetTextPath(text, baselineOrigin);
            if (outline == null)
            {
                return new SKPath { FillType = SKPathFillType.Winding };
            }

            outline.FillType = SKPathFillType.Winding;   // nonzero winding so glyph counters cut holes correctly

            if (scaleX != 1f)
            {
                outline.Transform(PathOps.HorizontalScale(scaleX, baselineOrigin));
            }

            return outline;
        }
    }
}
