using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Label.Helpers;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry.ElementDrawers
{
    internal static class ImageDecoder
    {
        public static SKImage Decode(byte[] data)
            => data == null || data.Length == 0 ? null : SKImage.FromEncodedData(data);
    }

    /// <summary>Geometry port of <c>GraphicFieldElementDrawer</c> (<c>^GF</c>). Image kept as a raster.</summary>
    public class GraphicFieldElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicField;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicField graphicField)
            {
                return currentPosition;
            }

            byte[] imageData = ByteHelper.HexToBytes(graphicField.Data);
            SKImage image = ImageDecoder.Decode(imageData);
            if (image == null)
            {
                return currentPosition;
            }

            float x = graphicField.PositionX;
            float y = graphicField.PositionY;
            if (graphicField.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (graphicField.FieldTypeset != null)
            {
                y -= image.Height;
                if (y < 0) y = 0;
            }

            this.context.AddImage(image, SKRect.Create(x, y, image.Width, image.Height));
            return CalculateNextDefaultPosition(x, y, image.Width, image.Height, graphicField.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }

    /// <summary>Geometry port of <c>ImageMoveElementDrawer</c> (<c>^IM</c>).</summary>
    public class ImageMoveElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplImageMove;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplImageMove imageMove)
            {
                return currentPosition;
            }

            byte[] imageData = this.printerStorage.GetFile(imageMove.StorageDevice, imageMove.ObjectName);
            SKImage image = ImageDecoder.Decode(imageData);
            if (image == null)
            {
                return currentPosition;
            }

            float x = imageMove.PositionX;
            float y = imageMove.PositionY;
            if (imageMove.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (imageMove.FieldTypeset != null)
            {
                y -= image.Height;
                if (y < 0) y = 0;
            }

            this.context.AddImage(image, SKRect.Create(x, y, image.Width, image.Height));
            return CalculateNextDefaultPosition(x, y, image.Width, image.Height, imageMove.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }

    /// <summary>Geometry port of <c>RecallGraphicElementDrawer</c> (<c>^XG</c>).</summary>
    public class RecallGraphicElementDrawer : GeometryElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplRecallGraphic;

        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplRecallGraphic recallGraphic)
            {
                return currentPosition;
            }

            byte[] imageData = this.printerStorage.GetFile(recallGraphic.StorageDevice, recallGraphic.ImageName);
            SKImage image = ImageDecoder.Decode(imageData);
            if (image == null)
            {
                return currentPosition;
            }

            float x = recallGraphic.PositionX;
            float y = recallGraphic.PositionY;
            if (recallGraphic.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (recallGraphic.FieldTypeset != null)
            {
                y -= image.Height;
                if (y < 0) y = 0;
            }

            this.context.AddImage(image, SKRect.Create(x, y, image.Width, image.Height));
            return CalculateNextDefaultPosition(x, y, image.Width, image.Height, recallGraphic.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
