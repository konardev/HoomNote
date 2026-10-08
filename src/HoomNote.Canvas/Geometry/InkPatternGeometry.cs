using HoomNote.Core.Documents;

namespace HoomNote.Canvas.Geometry;

/// <summary>Splits a fitted path by distance, so spacing does not depend on pointer sample rate.</summary>
public static class InkPatternGeometry
{
    public static IEnumerable<InkStrokeObject> Expand(InkStrokeObject stroke)
    {
        var style = stroke.Style.Normalize();
        if (style.LinePattern == InkLinePattern.Solid)
        {
            yield return stroke;
            yield break;
        }
        var points = StrokeOutlineBuilder.FitCenterline(stroke);
        if (points.Count == 0) yield break;
        var solid = style with { LinePattern = InkLinePattern.Solid, Smoothing = 0, PreserveSourceGeometry = true };
        InkStrokeObject Part(List<InkPoint> samples) => stroke with { Points = samples, Style = solid };
        if (points.Count == 1)
        {
            yield return Part([points[0]]);
            yield break;
        }

        var dotted = style.LinePattern == InkLinePattern.Dotted;
        var onLength = style.Width * 3d;
        var period = style.Width * (dotted ? 3d : 5d);
        var distance = 0d;
        var nextDot = 0d;
        List<InkPoint>? part = null;
        for (var index = 1; index < points.Count; index++)
        {
            var a = points[index - 1];
            var b = points[index];
            var length = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            if (length < 1e-8) continue;
            if (dotted)
            {
                while (nextDot <= distance + length)
                {
                    yield return Part([Interpolate(a, b, (nextDot - distance) / length)]);
                    nextDot += period;
                }
            }
            else
            {
                var offset = 0d;
                while (offset < length - 1e-8)
                {
                    var phase = (distance + offset) % period;
                    if (phase < 1e-7 || period - phase < 1e-7) phase = 0;
                    if (Math.Abs(phase - onLength) < 1e-7) phase = onLength;
                    var drawing = phase < onLength;
                    var step = Math.Min(length - offset, (drawing ? onLength : period) - phase);
                    if (drawing)
                    {
                        part ??= [Interpolate(a, b, offset / length)];
                        part.Add(Interpolate(a, b, (offset + step) / length));
                    }
                    offset += step;
                    if (drawing && phase + step >= onLength - 1e-7 && part is not null)
                    {
                        yield return Part(part);
                        part = null;
                    }
                }
            }
            distance += length;
        }
        if (part is not null) yield return Part(part);
        if (distance < 1e-8) yield return Part([points[0]]);
    }

    private static InkPoint Interpolate(InkPoint a, InkPoint b, double t) => new(
        a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t,
        a.Pressure + (b.Pressure - a.Pressure) * (float)t,
        a.TiltX + (b.TiltX - a.TiltX) * (float)t,
        a.TiltY + (b.TiltY - a.TiltY) * (float)t,
        a.TimestampMicroseconds + (long)((b.TimestampMicroseconds - a.TimestampMicroseconds) * t));
}
