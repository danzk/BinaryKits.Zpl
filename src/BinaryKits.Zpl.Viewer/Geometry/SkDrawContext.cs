using System.Collections.Generic;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.Geometry
{
    /// <summary>
    /// Accumulates geometry for the geometry-first pipeline. Element drawers do not paint immediately; they accumulate
    /// <see cref="SKPath"/> into per-element black / white / image buckets so the orchestrator can union,
    /// erase (white-over-black) or XOR (Field Reverse <c>^FR</c>) them against the running label. A
    /// transform stack mirrors <c>SKCanvas.Concat</c> + <c>SKAutoCanvasRestore</c>; the current transform
    /// is baked into each path as it is added (Skia is immediate-mode, so there is nothing to defer).
    /// </summary>
    public sealed class SkDrawContext
    {
        private readonly List<SKPath> _black = new List<SKPath>();
        private readonly List<SKPath> _white = new List<SKPath>();
        private readonly List<SkImageOp> _images = new List<SkImageOp>();

        private readonly Stack<SKMatrix> _transformStack = new Stack<SKMatrix>();
        private SKMatrix _current = SKMatrix.CreateIdentity();

        public SkDrawContext(int pixelWidth, int pixelHeight)
        {
            this.PixelWidth = pixelWidth;
            this.PixelHeight = pixelHeight;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }

        /// <summary>Push a transform; mirrors <c>SKCanvas.Concat(matrix)</c>. <paramref name="t"/> becomes
        /// innermost (applied first).</summary>
        public void PushTransform(SKMatrix t)
        {
            _transformStack.Push(_current);
            _current = SKMatrix.Concat(_current, t);
        }

        /// <summary>Pop the most recently pushed transform; mirrors leaving an <c>SKAutoCanvasRestore</c> scope.</summary>
        public void Pop()
        {
            _current = _transformStack.Count > 0 ? _transformStack.Pop() : SKMatrix.CreateIdentity();
        }

        /// <summary>Add a black (foreground) geometry, baking in the current transform.</summary>
        public void AddBlack(SKPath path)
        {
            if (path == null || path.IsEmpty)
            {
                return;
            }

            _black.Add(ApplyCurrent(path));
        }

        /// <summary>Add an explicit white geometry (e.g. <c>^GB...,W</c> without <c>^FR</c>).</summary>
        public void AddWhite(SKPath path)
        {
            if (path == null || path.IsEmpty)
            {
                return;
            }

            _white.Add(ApplyCurrent(path));
        }

        /// <summary>Add a raster image draw operation, baking in the current transform.</summary>
        public void AddImage(SKImage image, SKRect destination)
        {
            _images.Add(new SkImageOp(image, destination, _current));
        }

        private SKPath ApplyCurrent(SKPath path)
        {
            if (_current.IsIdentity)
            {
                return path;
            }

            // Skia is immediate-mode: bake the transform into a copy (do not mutate the incoming geometry).
            var clone = new SKPath(path);
            clone.Transform(_current);
            return clone;
        }

        // --- consumed by the orchestrator after each element's Draw() ---

        /// <summary>Combine and clear the current element's black geometry (null when none).</summary>
        public SKPath TakeBlack() => Combine(_black);

        /// <summary>Combine and clear the current element's white geometry (null when none).</summary>
        public SKPath TakeWhite() => Combine(_white);

        /// <summary>Return and clear the current element's image operations.</summary>
        public IReadOnlyList<SkImageOp> TakeImages()
        {
            if (_images.Count == 0)
            {
                return System.Array.Empty<SkImageOp>();
            }

            var copy = _images.ToArray();
            _images.Clear();
            return copy;
        }

        private static SKPath Combine(List<SKPath> parts)
        {
            if (parts.Count == 0)
            {
                return null;
            }

            SKPath result;
            if (parts.Count == 1)
            {
                result = parts[0];
            }
            else
            {
                // Accumulate sub-paths into one flat path under non-zero (Winding) fill — the painter's
                // replay and the ^FR boolean step both consume one combined fill per element.
                result = new SKPath { FillType = SKPathFillType.Winding };
                foreach (SKPath p in parts)
                {
                    result.AddPath(p);
                }
            }

            parts.Clear();
            return result;
        }
    }
}
