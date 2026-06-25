using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.ElementDrawers;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Base class for geometry-first drawers. <see cref="CalculateNextDefaultPosition"/> is ported
    /// verbatim from the legacy <c>ElementDrawerBase</c> so default field-position chaining is
    /// identical across backends.
    /// </summary>
    public abstract class GeometryElementDrawerBase : IGeometryElementDrawer
    {
        protected IPrinterStorage printerStorage;
        protected SkDrawContext context;

        public void Prepare(IPrinterStorage printerStorage, SkDrawContext context)
        {
            this.printerStorage = printerStorage;
            this.context = context;
        }

        public abstract bool CanDraw(ZplElementBase element);

        public virtual bool IsReverseDraw(ZplElementBase element) => false;

        public virtual bool IsWhiteDraw(ZplElementBase element) => false;

        public virtual SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition)
            => currentPosition;

        public virtual SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
            => this.Draw(element, options, currentPosition);

        public virtual SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont, int printDensityDpmm)
            => this.Draw(element, options, currentPosition, internationalFont);

        protected virtual SKPoint CalculateNextDefaultPosition(float x, float y, float elementWidth, float elementHeight, bool useFieldOrigin, FieldOrientation fieldOrientation, SKPoint currentPosition)
        {
            if (useFieldOrigin)
            {
                switch (fieldOrientation)
                {
                    case FieldOrientation.Normal:
                        return new SKPoint(x + elementWidth, y + elementHeight);
                    case FieldOrientation.Rotated90:
                        return new SKPoint(x, y + elementHeight);
                    case FieldOrientation.Rotated180:
                        return new SKPoint(x - elementWidth, y);
                    case FieldOrientation.Rotated270:
                        return new SKPoint(x, y - elementHeight);
                }
            }
            else
            {
                switch (fieldOrientation)
                {
                    case FieldOrientation.Normal:
                        return new SKPoint(x + elementWidth, y);
                    case FieldOrientation.Rotated90:
                        return new SKPoint(x, y + elementWidth);
                    case FieldOrientation.Rotated180:
                        return new SKPoint(x - elementWidth, y);
                    case FieldOrientation.Rotated270:
                        return new SKPoint(x, y - elementWidth);
                }
            }

            return currentPosition;
        }
    }
}
