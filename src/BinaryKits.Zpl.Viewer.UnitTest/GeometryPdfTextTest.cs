using System.Text;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Geometry;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BinaryKits.Zpl.Viewer.UnitTest
{
    /// <summary>
    /// Text that is not knocked out is drawn with the glyph blitter, so it stays <b>real, selectable text</b> in
    /// the exported PDF (an embedded <c>/BaseFont</c>) and an SVG (<c>&lt;text&gt;</c>) — only a reverse field
    /// that actually intersects black forces the affected text to vector outlines.
    /// </summary>
    [TestClass]
    public class GeometryPdfTextTest
    {
        private static byte[] Render(string zpl, bool pdf)
        {
            IPrinterStorage storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;
            var renderer = new GeometryRenderer(storage, new DrawerOptions(new FontManager()) { OpaqueBackground = true });
            return pdf
                ? renderer.DrawPdf(elements, 102, 152, 8)
                : renderer.DrawSvg(elements, 102, 152, 8);
        }

        [TestMethod]
        public void DrawPdf_PlainText_EmbedsSelectableFont()
        {
            string raw = Encoding.Latin1.GetString(Render("^XA^FO40,40^A0N,40,40^FDHello World^FS^XZ", pdf: true));
            StringAssert.Contains(raw, "/BaseFont", "non-reverse text must embed a font (real selectable text), not only path outlines");
        }

        [TestMethod]
        public void DrawSvg_PlainText_EmitsTextElement()
        {
            string svg = Encoding.UTF8.GetString(Render("^XA^FO40,40^A0N,40,40^FDHello World^FS^XZ", pdf: false));
            StringAssert.Contains(svg, "<text", "non-reverse text must be a real <text> element in the SVG");
        }

        [TestMethod]
        public void DrawPdf_NonIntersectingReverseText_StillEmbedsFont()
        {
            // ^FR over a blank background causes no knockout (XOR over white = black), so it stays real text.
            string raw = Encoding.Latin1.GetString(Render("^XA^FO40,40^A0N,40,40^FR^FDReverse^FS^XZ", pdf: true));
            StringAssert.Contains(raw, "/BaseFont", "a reverse field that intersects nothing must stay real text");
        }

        [TestMethod]
        public void DrawPdf_ReverseTextOverBox_IsVectorOutline()
        {
            // The reverse text intersects the black box, so it is genuinely knocked out and rendered as geometry
            // (a white cut-out) — there is no font to embed for it.
            string raw = Encoding.Latin1.GetString(Render("^XA^FO50,50^GB300,120,120^FS^FO70,70^A0N,70,70^FR^FDHI^FS^XZ", pdf: true));
            StringAssert.DoesNotMatch(raw, new System.Text.RegularExpressions.Regex("/BaseFont"), "knocked-out reverse text must be vector outlines, not embedded text");
        }
    }
}
