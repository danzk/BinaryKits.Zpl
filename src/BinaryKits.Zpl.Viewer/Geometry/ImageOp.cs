using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// A single image draw operation (for <c>^GF</c> / <c>^IM</c> / <c>^XG</c> elements that have no
    /// geometry equivalent). The transform is a baked <see cref="SKMatrix"/>.
    /// </summary>
    public readonly struct ImageOp
    {
        public ImageOp(SKImage image, SKRect destination, SKMatrix transform)
        {
            this.Image = image;
            this.Destination = destination;
            this.Transform = transform;
        }

        public SKImage Image { get; }
        public SKRect Destination { get; }
        public SKMatrix Transform { get; }
    }
}
