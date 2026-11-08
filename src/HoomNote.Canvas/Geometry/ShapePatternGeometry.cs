using HoomNote.Core.Documents;

namespace HoomNote.Canvas.Geometry;

/// <summary>
/// Turns a dotted or dashed shape outline into ink strokes, so shapes reuse the same
/// distance-based pattern as handwriting in every renderer.
/// </summary>
public static class ShapePatternGeometry
{
    private const double RoundedCornerRadius = 12;

    public static IEnumerable<InkStrokeObject> ToPatternStrokes(ShapeObject shape)
    {
        var pattern = Enum.IsDefined(shape.LinePattern) ? shape.LinePattern : InkLinePattern.Solid;
        var style = new InkStyle
        {
            Tool = InkToolKind.Pen,
            LinePattern = pattern,
            Color = shape.StrokeColor,
            Width = shape.StrokeWidth,
            PressureEnabled = false,
            PressureSensitivity = 0,
            Smoothing = 0,
            PreserveSourceGeometry = true
        };
        foreach (var figure in Outline(shape))
        {
            if (figure.Count == 0) continue;
            yield return new InkStrokeObject
            {
                Id = shape.Id,
                ZIndex = shape.ZIndex,
                IsLocked = shape.IsLocked,
                IsHidden = shape.IsHidden,
                Transform = shape.Transform,
                Style = style,
                Points = figure.Select(point => new InkPoint(point.X, point.Y, 1f)).ToList()
            };
        }
    }

    /// <summary>Outline polylines in shape-local coordinates. Closed figures repeat their first point.</summary>
    public static IReadOnlyList<IReadOnlyList<PointD>> Outline(ShapeObject shape)
    {
        var bounds = shape.Bounds;
        switch (shape.Shape)
        {
            case ShapeKind.Circle:
            case ShapeKind.Ellipse:
                return [Ellipse(bounds)];
            case ShapeKind.RoundedRectangle:
                return [RoundedRectangle(bounds)];
            case ShapeKind.Line:
                return [[shape.StartPoint ?? new PointD(bounds.Left, bounds.Top),
                    shape.EndPoint ?? new PointD(bounds.Right, bounds.Bottom)]];
            case ShapeKind.Arrow:
                var start = shape.StartPoint ?? new PointD(bounds.Left, bounds.Top);
                var end = shape.EndPoint ?? new PointD(bounds.Right, bounds.Bottom);
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
                var head = Math.Clamp(length * 0.2, 9, 24);
                PointD Barb(double offset) => new(end.X - Math.Cos(angle + offset) * head,
                    end.Y - Math.Sin(angle + offset) * head);
                return [[start, end], [Barb(-0.55), end, Barb(0.55)]];
            case ShapeKind.Triangle:
                return [Closed([
                    new PointD(bounds.Center.X, bounds.Top), new PointD(bounds.Right, bounds.Bottom),
                    new PointD(bounds.Left, bounds.Bottom)])];
            case ShapeKind.Diamond:
                return [Closed([
                    bounds.Center with { Y = bounds.Top }, bounds.Center with { X = bounds.Right },
                    bounds.Center with { Y = bounds.Bottom }, bounds.Center with { X = bounds.Left }])];
            case ShapeKind.Star:
                return [Closed(ShapeGeometry.StarPoints(bounds))];
            default:
                return [Closed([
                    new PointD(bounds.Left, bounds.Top), new PointD(bounds.Right, bounds.Top),
                    new PointD(bounds.Right, bounds.Bottom), new PointD(bounds.Left, bounds.Bottom)])];
        }
    }

    private static List<PointD> Closed(IReadOnlyList<PointD> points)
    {
        var closed = points.ToList();
        if (closed.Count > 0) closed.Add(closed[0]);
        return closed;
    }

    private static List<PointD> Ellipse(RectD bounds)
    {
        var rx = bounds.Width / 2d;
        var ry = bounds.Height / 2d;
        var segments = SegmentsFor(Math.PI * (rx + ry), 32);
        var points = new List<PointD>(segments + 1);
        for (var index = 0; index <= segments; index++)
        {
            // Start at the top so the pattern seam sits in a predictable place.
            var angle = -Math.PI / 2d + Math.Tau * (index % segments) / segments;
            points.Add(new PointD(bounds.Center.X + Math.Cos(angle) * rx, bounds.Center.Y + Math.Sin(angle) * ry));
        }
        return points;
    }

    private static List<PointD> RoundedRectangle(RectD bounds)
    {
        var radius = Math.Min(RoundedCornerRadius, Math.Min(bounds.Width, bounds.Height) / 2d);
        var points = new List<PointD>();
        var segments = SegmentsFor(Math.PI * radius / 2d, 6);
        void Corner(double cx, double cy, double startAngle)
        {
            for (var index = 0; index <= segments; index++)
            {
                var angle = startAngle + Math.PI / 2d * index / segments;
                points.Add(new PointD(cx + Math.Cos(angle) * radius, cy + Math.Sin(angle) * radius));
            }
        }
        Corner(bounds.Right - radius, bounds.Top + radius, -Math.PI / 2d);
        Corner(bounds.Right - radius, bounds.Bottom - radius, 0);
        Corner(bounds.Left + radius, bounds.Bottom - radius, Math.PI / 2d);
        Corner(bounds.Left + radius, bounds.Top + radius, Math.PI);
        points.Add(points[0]);
        return points;
    }

    private static int SegmentsFor(double arcLength, int minimum) =>
        Math.Clamp((int)Math.Ceiling(arcLength / 2d), minimum, 720);
}
