using System.Numerics;
using HoomNote.Canvas.Geometry;
using HoomNote.Core.Documents;

namespace HoomNote.Canvas.Interaction;

/// <summary>A pending crop in image-local coordinates. Dragging never changes the document.</summary>
public sealed record ImageCropSelection
{
    public ImageObject Image { get; }
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public RectD FullBounds { get; }
    public RectD Bounds { get; private init; }
    private readonly Matrix3x2 _worldToLocal;

    public ImageCropSelection(ImageObject image, int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || !image.Bounds.IsFinite ||
            image.Bounds.Width <= 0 || image.Bounds.Height <= 0 ||
            !Matrix3x2.Invert(image.Transform.ToMatrix(), out _worldToLocal) ||
            !float.IsFinite(_worldToLocal.M11) || !float.IsFinite(_worldToLocal.M12) ||
            !float.IsFinite(_worldToLocal.M21) || !float.IsFinite(_worldToLocal.M22) ||
            !float.IsFinite(_worldToLocal.M31) || !float.IsFinite(_worldToLocal.M32))
            throw new ArgumentException("The image has invalid dimensions or a non-invertible transform.", nameof(image));
        Image = image;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        FullBounds = ImageLayout.Destination(image.Bounds, pixelWidth, pixelHeight, image.PreserveAspectRatio);
        Bounds = FullBounds;
    }

    public RectD Pixels => ImageCrop.PixelBounds(new RectD(
        (Bounds.X - FullBounds.X) / FullBounds.Width * PixelWidth,
        (Bounds.Y - FullBounds.Y) / FullBounds.Height * PixelHeight,
        Bounds.Width / FullBounds.Width * PixelWidth,
        Bounds.Height / FullBounds.Height * PixelHeight), PixelWidth, PixelHeight);

    public ImageCropSelection Drag(TransformHandle handle, PointD startWorld, PointD currentWorld)
    {
        var delta = Vector2.Transform(currentWorld.ToVector2(), _worldToLocal) -
                    Vector2.Transform(startWorld.ToVector2(), _worldToLocal);
        if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Y)) return this;
        var left = Bounds.Left;
        var top = Bounds.Top;
        var right = Bounds.Right;
        var bottom = Bounds.Bottom;
        var minWidth = FullBounds.Width / PixelWidth;
        var minHeight = FullBounds.Height / PixelHeight;
        if (handle is TransformHandle.Left or TransformHandle.TopLeft or TransformHandle.BottomLeft)
            left = Math.Clamp(left + delta.X, FullBounds.Left, right - minWidth);
        if (handle is TransformHandle.Right or TransformHandle.TopRight or TransformHandle.BottomRight)
            right = Math.Clamp(right + delta.X, left + minWidth, FullBounds.Right);
        if (handle is TransformHandle.Top or TransformHandle.TopLeft or TransformHandle.TopRight)
            top = Math.Clamp(top + delta.Y, FullBounds.Top, bottom - minHeight);
        if (handle is TransformHandle.Bottom or TransformHandle.BottomLeft or TransformHandle.BottomRight)
            bottom = Math.Clamp(bottom + delta.Y, top + minHeight, FullBounds.Bottom);
        return this with { Bounds = new RectD(left, top, right - left, bottom - top) };
    }

    public IEnumerable<(TransformHandle Handle, PointD Start, PointD End)> Bars(double zoom)
    {
        var matrix = Image.Transform.ToMatrix();
        var x = Vector2.Normalize(Vector2.TransformNormal(Vector2.UnitX, matrix));
        var y = Vector2.Normalize(Vector2.TransformNormal(Vector2.UnitY, matrix));
        var length = (float)(12 / Math.Max(zoom, 0.01));
        foreach (var (handle, local) in SelectionTransformer.GetHandles(Bounds))
        {
            if (handle == TransformHandle.Rotate) continue;
            var center = Image.Transform.Apply(local).ToVector2();
            if (handle is TransformHandle.Top or TransformHandle.Bottom)
                yield return Bar(handle, center - x * length, center + x * length);
            else if (handle is TransformHandle.Left or TransformHandle.Right)
                yield return Bar(handle, center - y * length, center + y * length);
            else
            {
                var right = handle is TransformHandle.TopRight or TransformHandle.BottomRight;
                var bottom = handle is TransformHandle.BottomLeft or TransformHandle.BottomRight;
                yield return Bar(handle, center, center + x * (right ? -length : length));
                yield return Bar(handle, center, center + y * (bottom ? -length : length));
            }
        }
    }

    public TransformHandle HitHandle(PointD world, double zoom)
    {
        var best = TransformHandle.None;
        var distance = (float)(10 / Math.Max(zoom, 0.01));
        foreach (var bar in Bars(zoom))
        {
            var a = bar.Start.ToVector2();
            var b = bar.End.ToVector2();
            var offset = b - a;
            var t = Math.Clamp(Vector2.Dot(world.ToVector2() - a, offset) / offset.LengthSquared(), 0, 1);
            var candidate = Vector2.Distance(world.ToVector2(), a + offset * t);
            if (candidate >= distance) continue;
            distance = candidate;
            best = bar.Handle;
        }
        return best;
    }

    private static (TransformHandle, PointD, PointD) Bar(TransformHandle handle, Vector2 start, Vector2 end) =>
        (handle, new PointD(start.X, start.Y), new PointD(end.X, end.Y));
}
