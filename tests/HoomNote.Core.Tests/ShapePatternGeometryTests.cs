using System.Text.Json;
using HoomNote.Canvas.Geometry;
using HoomNote.Core.Documents;

namespace HoomNote.Core.Tests;

public sealed class ShapePatternGeometryTests
{
    [Fact]
    public void DashedLineUsesShapeStyleAndInkDashSpacing()
    {
        var line = new ShapeObject
        {
            Shape = ShapeKind.Line, Bounds = new RectD(0, 0, 30, 1),
            StartPoint = new PointD(0, 0), EndPoint = new PointD(30, 0),
            StrokeColor = "#123456", StrokeWidth = 2, LinePattern = InkLinePattern.Dashed,
            Transform = Transform2D.Scale(2, 2, new PointD(0, 0)), ZIndex = 7
        };
        var outline = Assert.Single(ShapePatternGeometry.ToPatternStrokes(line));
        Assert.Equal(line.Id, outline.Id);
        Assert.Equal(line.Transform, outline.Transform);
        Assert.Equal(7, outline.ZIndex);
        Assert.Equal("#123456", outline.Style.Color);
        Assert.Equal(2, outline.Style.Width);
        Assert.False(outline.Style.PressureEnabled);

        var dashes = InkPatternGeometry.Expand(outline).ToArray();
        // Same 3w on / 2w off rhythm as a dashed pen stroke of equal width.
        Assert.Equal(new double[] { 0, 10, 20 }, dashes.Select(dash => dash.Points[0].X));
        Assert.All(dashes, dash => Assert.Equal(6, dash.Points[^1].X - dash.Points[0].X, 6));
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle, 120)]
    [InlineData(ShapeKind.Square, 120)]
    [InlineData(ShapeKind.Diamond, 89.4427191)]
    public void ClosedPolygonOutlinesTraceThePerimeter(ShapeKind kind, double perimeter)
    {
        var shape = new ShapeObject { Shape = kind, Bounds = new RectD(10, 10, 40, 20) };
        var figure = Assert.Single(ShapePatternGeometry.Outline(shape));
        Assert.Equal(figure[0], figure[^1]);
        Assert.Equal(perimeter, Length(figure), 3);
    }

    [Fact]
    public void EllipseAndRoundedRectangleStayOnTheirBounds()
    {
        var bounds = new RectD(0, 0, 100, 50);
        foreach (var kind in new[] { ShapeKind.Ellipse, ShapeKind.Circle, ShapeKind.RoundedRectangle })
        {
            var figure = Assert.Single(ShapePatternGeometry.Outline(new ShapeObject { Shape = kind, Bounds = bounds }));
            Assert.Equal(figure[0].X, figure[^1].X, 6);
            Assert.Equal(figure[0].Y, figure[^1].Y, 6);
            Assert.All(figure, point =>
            {
                Assert.InRange(point.X, -1e-6, 100 + 1e-6);
                Assert.InRange(point.Y, -1e-6, 50 + 1e-6);
            });
        }
    }

    [Fact]
    public void ArrowOutlineHasShaftAndHead()
    {
        var arrow = new ShapeObject
        {
            Shape = ShapeKind.Arrow, StartPoint = new PointD(0, 0), EndPoint = new PointD(100, 0),
            LinePattern = InkLinePattern.Dotted
        };
        var figures = ShapePatternGeometry.Outline(arrow);
        Assert.Equal(2, figures.Count);
        Assert.Equal(new PointD(100, 0), figures[0][^1]);
        Assert.Equal(new PointD(100, 0), figures[1][1]);
        Assert.All(ShapePatternGeometry.ToPatternStrokes(arrow).SelectMany(InkPatternGeometry.Expand),
            dot => Assert.Single(dot.Points));
    }

    [Fact]
    public void ShapePatternRoundTripsAndLegacyShapesStaySolid()
    {
        CanvasObject shape = new ShapeObject { Shape = ShapeKind.Star, LinePattern = InkLinePattern.Dotted };
        var json = JsonSerializer.Serialize(shape);
        var restored = Assert.IsType<ShapeObject>(JsonSerializer.Deserialize<CanvasObject>(json));
        Assert.Equal(InkLinePattern.Dotted, restored.LinePattern);
        var legacy = Assert.IsType<ShapeObject>(JsonSerializer.Deserialize<CanvasObject>("""{"$type":"shape"}"""));
        Assert.Equal(InkLinePattern.Solid, legacy.LinePattern);
    }

    private static double Length(IReadOnlyList<PointD> points)
    {
        var total = 0d;
        for (var index = 1; index < points.Count; index++)
            total += Math.Sqrt(Math.Pow(points[index].X - points[index - 1].X, 2) +
                               Math.Pow(points[index].Y - points[index - 1].Y, 2));
        return total;
    }
}
