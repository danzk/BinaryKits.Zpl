using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Geometry;
using BinaryKits.Zpl.Viewer.WebApi.Models;
using BinaryKits.Zpl.Viewer.WebApi.Properties;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BinaryKits.Zpl.Viewer.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ViewerController : ControllerBase
    {
        private readonly ILogger<ViewerController> _logger;

        public ViewerController(ILogger<ViewerController> logger)
        {
            this._logger = logger;
        }

        [HttpPost]
        public ActionResult<RenderResponseDto> Render(RenderRequestDto request)
        {
            try
            {
                return RenderZpl(request);
            }
            catch (Exception ex)
            {
                StringBuilder builder = new StringBuilder();
                while (ex is Exception) {
                    builder.AppendLine(ex.Message);
                    builder.AppendLine(ex.StackTrace);
                    ex = ex.InnerException;
                }

                return this.StatusCode(StatusCodes.Status500InternalServerError, builder.ToString());
            }
        }

        private ActionResult<RenderResponseDto> RenderZpl(RenderRequestDto request)
        {
            IPrinterStorage printerStorage = new PrinterStorage();

            var fontManager = new FontManager();
            //register default fonts
            fontManager.RegisterTypeface(SKTypeface.FromStream(new MemoryStream(Resources.TeX_Gyre_Heros_Cn_Bold)));
            fontManager.RegisterTypeface(SKTypeface.FromStream(new MemoryStream(Resources.DejaVu_Sans_Mono)));

            var drawerOptions = new DrawerOptions(fontManager);

            drawerOptions.OpaqueBackground = true; //set white background for viewer requests

            //Geometry-renderer appearance options (honoured by the geometry renderer; PDF always uses it).
            drawerOptions.UseGeometryRenderer = request.UseGeometryRenderer;
            SKColor? ribbon = ParseColor(request.RibbonColor);
            if (ribbon.HasValue)
            {
                drawerOptions.RibbonColor = ribbon.Value;
            }
            SKColor? stock = ParseColor(request.LabelColor);
            if (stock.HasValue)
            {
                drawerOptions.LabelColor = stock.Value;
            }

            //PDF mode (image mode is default)
            if (request.Type == "PDF")
            {
                drawerOptions.PdfOutput = true;
            }

            var drawer = new ZplElementDrawer(printerStorage, drawerOptions);
            // Geometry-first renderer: always used for PDF (vector output), optionally for PNG.
            var geomDrawer = new GeometryRenderer(printerStorage, drawerOptions);

            var analyzer = new ZplAnalyzer(printerStorage);
            var analyzeInfo = analyzer.Analyze(request.ZplData);

            var labels = new List<RenderLabelDto>();
            var pdfs = new List<RenderLabelDto>();
            var svgs = new List<RenderLabelDto>();
            foreach (var labelInfo in analyzeInfo.LabelInfos)
            {
                if (request.Type == "image" || request.Type == "both")
                {
                    var imageData = drawerOptions.UseGeometryRenderer
                        ? geomDrawer.DrawPng(labelInfo.ZplElements, request.LabelWidth, request.LabelHeight, request.PrintDensityDpmm)
                        : drawer.Draw(labelInfo.ZplElements, request.LabelWidth, request.LabelHeight, request.PrintDensityDpmm);
                    labels.Add(new RenderLabelDto
                    {
                        ImageBase64 = Convert.ToBase64String(imageData)
                    });
                }

                if (request.Type == "PDF" || request.Type == "both")
                {
                    // Vector PDF via the geometry renderer (^FR and barcodes stay vector; no FixPdfInvertDraw).
                    var pdfData = geomDrawer.DrawPdf(labelInfo.ZplElements, request.LabelWidth, request.LabelHeight, request.PrintDensityDpmm);
                    pdfs.Add(new RenderLabelDto
                    {
                        PdfBase64 = Convert.ToBase64String(pdfData)
                    });
                }

                if (request.Type == "SVG")
                {
                    // Vector SVG via the geometry renderer (outline-path text, vector barcodes/reverse; only
                    // genuine ^GF/^XG/^IM raster images embed).
                    var svgData = geomDrawer.DrawSvg(labelInfo.ZplElements, request.LabelWidth, request.LabelHeight, request.PrintDensityDpmm);
                    svgs.Add(new RenderLabelDto
                    {
                        SvgBase64 = Convert.ToBase64String(svgData)
                    });
                }
            }

            var response = new RenderResponseDto
            {
                Labels = labels.ToArray(),
                Pdfs = pdfs.ToArray(),
                Svgs = svgs.ToArray(),
                NonSupportedCommands = analyzeInfo.UnknownCommands
            };

            return this.StatusCode(StatusCodes.Status200OK, response);
        }

        /// <summary>Parse a hex colour string (e.g. "#FF0000", "#80FFFF00") or "transparent"; null if blank/invalid.</summary>
        private static SKColor? ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (value.Trim().Equals("transparent", StringComparison.OrdinalIgnoreCase))
            {
                return SKColors.Transparent;
            }

            return SKColor.TryParse(value, out SKColor color) ? color : (SKColor?)null;
        }
    }
}
