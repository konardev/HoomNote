using HoomNote.Canvas.Geometry;
using HoomNote.Canvas.Interaction;
using HoomNote.Core.Documents;

namespace HoomNote.Core.Tests;

public sealed class ImageCropSelectionTests
{
    [Theory]
    [InlineData(TransformHandle.Left, 20, 0, 20, 0, 180, 100)]
    [InlineData(TransformHandle.Right, -20, 0, 0, 0, 180, 100)]
    [InlineData(TransformHandle.Top, 0, 20, 0, 20, 200, 80)]
    [InlineData(TransformHandle.Bottom, 0, -20, 0, 0, 200, 80)]
    [InlineData(TransformHandle.TopLeft, 20, 20, 20, 20, 180, 80)]
    [InlineData(TransformHandle.TopRight, -20, 20, 0, 20, 180, 80)]
    [InlineData(TransformHandle.BottomLeft, 20, -20, 20, 0, 180, 80)]
    [InlineData(TransformHandle.BottomRight, -20, -20, 0, 0, 180, 80)]
    public void AnchorsChangeCropWithoutChangingTheImage(TransformHandle handle, double dx, double dy,
        double x, double y, double width, double height)
    {
        var image = new ImageObject { Bounds = new RectD(0, 0, 200, 100), AssetHash = "original.png" };
        var start = new ImageCropSelection(image, 400, 200);
        var crop = start.Drag(handle, new PointD(10, 10), new PointD(10 + dx, 10 + dy));
        Assert.Equal(new RectD(x, y, width, height), crop.Bounds);
        Assert.Equal(new RectD(x * 2, y * 2, width * 2, height * 2), crop.Pixels);
        Assert.Same(image, crop.Image);
        Assert.Equal(new RectD(0, 0, 200, 100), start.Bounds);
        Assert.Equal(start.Bounds, image.Bounds);
    }

    [Fact]
    public void CropUsesLocalCoordinatesForRotatedScaledImages()
    {
        var image = new ImageObject
        {
            Bounds = new RectD(10, 20, 200, 100),
            Transform = Transform2D.Scale(2, 3, new PointD(0, 0))
                .Then(Transform2D.Rotation(Math.PI / 2, new PointD(0, 0)))
        };
        var crop = new ImageCropSelection(image, 400, 200);
        var start = image.Transform.Apply(new PointD(10, 20));
        var end = image.Transform.Apply(new PointD(60, 40));
        var changed = crop.Drag(TransformHandle.TopLeft, start, end);
        Assert.Equal(60, changed.Bounds.X, 3);
        Assert.Equal(40, changed.Bounds.Y, 3);
        Assert.Equal(150, changed.Bounds.Width, 3);
        Assert.Equal(80, changed.Bounds.Height, 3);
    }

    [Fact]
    public void HandlesCannotInvertOrEscapeTheImageAndCanExpandAgain()
    {
        var crop = new ImageCropSelection(new ImageObject { Bounds = new RectD(0, 0, 200, 100) }, 400, 200);
        var collapsed = crop.Drag(TransformHandle.TopLeft, new(0, 0), new(500, 500));
        Assert.Equal(new RectD(199.5, 99.5, 0.5, 0.5), collapsed.Bounds);
        Assert.Equal(new RectD(399, 199, 1, 1), collapsed.Pixels);
        var expanded = collapsed.Drag(TransformHandle.TopLeft, new(0, 0), new(-500, -500));
        Assert.Equal(crop.Bounds, expanded.Bounds);
        Assert.Equal(crop.Bounds, crop.Drag(TransformHandle.BottomRight, new(0, 0), new(500, 500)).Bounds);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(4)]
    public void BarsHaveConstantScreenLengthAndMatchingHitTargets(double zoom)
    {
        var crop = new ImageCropSelection(new ImageObject { Bounds = new RectD(0, 0, 200, 100) }, 400, 200);
        var bars = crop.Bars(zoom).ToArray();
        Assert.Equal(12, bars.Length);
        Assert.DoesNotContain(bars, bar => bar.Handle is TransformHandle.Rotate or TransformHandle.Move);
        var right = Assert.Single(bars, bar => bar.Handle == TransformHandle.Right);
        Assert.Equal(24, (right.End.Y - right.Start.Y) * zoom, 4);
        Assert.Equal(TransformHandle.Right, crop.HitHandle(new PointD(200, 50), zoom));
        Assert.Equal(TransformHandle.None, crop.HitHandle(new PointD(100, 50), zoom));
    }

    [Fact]
    public void AspectFitPaddingIsNotPartOfTheCrop()
    {
        var crop = new ImageCropSelection(new ImageObject { Bounds = new RectD(10, 20, 200, 200) }, 400, 200);
        Assert.Equal(new RectD(10, 70, 200, 100), crop.FullBounds);
        Assert.Equal(new RectD(0, 0, 400, 200), crop.Pixels);
    }
}
