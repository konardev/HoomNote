using System.Text.Json;
using HoomNote.Canvas.Geometry;
using HoomNote.Canvas.Rendering;
using HoomNote.Core.Documents;
using HoomNote.Core.Editing;

namespace HoomNote.Core.Tests;

public sealed class ImageCropAndInkPatternTests
{
    [Theory]
    [InlineData(InkToolKind.Pen)]
    [InlineData(InkToolKind.Pencil)]
    [InlineData(InkToolKind.Highlighter)]
    public void DotsUseDistanceAndPreserveStyle(InkToolKind tool)
    {
        var stroke = Stroke(InkLinePattern.Dotted) with { Style = new InkStyle
        {
            Tool = tool, Width = 2, LinePattern = InkLinePattern.Dotted, Smoothing = 0,
            Color = "#123456", Opacity = 0.4f
        } };
        var parts = InkPatternGeometry.Expand(stroke).ToArray();
        Assert.Equal(new double[] { 0, 6, 12, 18, 24, 30 }, parts.Select(p => p.Points.Single().X));
        Assert.All(parts, part =>
        {
            Assert.Equal(tool, part.Style.Tool);
            Assert.Equal("#123456", part.Style.Color);
            Assert.Equal(InkLinePattern.Solid, part.Style.LinePattern);
        });
    }

    [Fact]
    public void DashesContinueAcrossSamplesAndCorners()
    {
        var stroke = Stroke(InkLinePattern.Dashed) with
        {
            Points = [new(0, 0), new(3, 0), new(3, 12)]
        };
        var parts = InkPatternGeometry.Expand(stroke).ToArray();
        Assert.Equal(2, parts.Length);
        Assert.Equal(new PointD(3, 3), parts[0].Points[^1].Position);
        Assert.Equal(new PointD(3, 7), parts[1].Points[0].Position);
        Assert.Equal(new PointD(3, 12), parts[1].Points[^1].Position);
        Assert.Equal(0, InkBatchPolicy.CompatiblePrefixLength([stroke, stroke], 0, 10, 100));
    }

    [Fact]
    public void PatternRoundTripsAndLegacyStyleDefaultsToSolid()
    {
        var style = Stroke(InkLinePattern.Dashed).Style;
        Assert.Equal(style, JsonSerializer.Deserialize<InkStyle>(JsonSerializer.Serialize(style)));
        Assert.Equal(InkLinePattern.Solid, JsonSerializer.Deserialize<InkStyle>("{}")!.LinePattern);
        Assert.Equal(InkLinePattern.Solid, (style with { LinePattern = (InkLinePattern)999 }).Normalize().LinePattern);
    }

    [Fact]
    public void CropPreservesPlacementTransformAndCanBeUndone()
    {
        var image = new ImageObject
        {
            AssetHash = "original.png", Bounds = new RectD(10, 20, 200, 200),
            Transform = Transform2D.Scale(2, 3, new PointD(0, 0))
        };
        var cropped = ImageCrop.Apply(image, new RectD(50, 20, 100, 40), 200, 100, "cropped.png");
        Assert.Equal(new RectD(60, 90, 100, 40), cropped.Bounds);
        Assert.Equal(image.Transform, cropped.Transform);
        Assert.Equal(image.Id, cropped.Id);
        Assert.Equal("original.png", image.AssetHash);
        var document = new HoomNoteDocument();
        var page = new NotePage();
        page.Objects.Add(image);
        document.Pages.Add(page);
        var command = new ReplaceObjectsCommand(page.Id, [image], [cropped], "Crop image");
        command.Execute(document);
        Assert.Equal(cropped, Assert.Single(page.Objects));
        command.Undo(document);
        Assert.Equal(image, Assert.Single(page.Objects));
        command.Execute(document);
        Assert.Equal(cropped, Assert.Single(page.Objects));
    }

    [Fact]
    public void CropRoundsToPixelsAndRejectsEmptySelection()
    {
        Assert.Equal(new RectD(0, 3, 20, 7), ImageCrop.PixelBounds(new RectD(-4, 3.8, 30, 15), 20, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageCrop.PixelBounds(new RectD(0, 0, 0, 10), 20, 10));
    }

    private static InkStrokeObject Stroke(InkLinePattern pattern) => new()
    {
        Points = [new(0, 0), new(30, 0)],
        Style = new InkStyle { Width = 2, LinePattern = pattern, Smoothing = 0 }
    };
}
