using System.Xml.Linq;
using HoomNote.Core.Documents;
using HoomNote.Core.Services;
using HoomNote.Infrastructure.Export;
using HoomNote.Infrastructure.Serialization;
using HoomNote.Infrastructure.Storage;
using System.Text.Json;

namespace HoomNote.Infrastructure.Tests;

public sealed class InkPatternExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoomNote.Tests", Guid.NewGuid().ToString("N"));
    public InkPatternExportTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(InkLinePattern.Dotted, "circle", 6)]
    [InlineData(InkLinePattern.Dashed, "path", 3)]
    public async Task ExportsRetainPatternGaps(InkLinePattern pattern, string element, int count)
    {
        var store = new ContentAddressedAssetStore(Path.Combine(_root, "assets"));
        var document = HoomNoteDocument.Create("Pattern export");
        document.Pages.Add(new NotePage { Objects = [new InkStrokeObject
        {
            Points = [new(10, 10), new(40, 10)],
            Style = new InkStyle { LinePattern = pattern, Width = 2, PressureEnabled = false, Smoothing = 0 }
        }] });
        var exporter = new VectorExportService(store);
        var svgPath = Path.Combine(_root, "patterns.svg");
        Assert.Empty((await exporter.ExportAsync(document, svgPath, VectorExportFormat.Svg)).Warnings);
        var svg = XDocument.Load(svgPath);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal(count, svg.Root!.Elements(ns + element).Count());
        var pdfPath = Path.Combine(_root, "patterns.pdf");
        Assert.Empty((await exporter.ExportAsync(document, pdfPath, VectorExportFormat.Pdf)).Warnings);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        Assert.True(pdf.GetPage(1).Paths.Count >= count);
    }

    [Fact]
    public async Task PatternsSurviveSettingsAndDocumentSerialization()
    {
        var settings = new LocalUserSettingsStore(Path.Combine(_root, "settings.json"));
        var preferences = new UserPreferences
        {
            PenLinePattern = InkLinePattern.Dashed, HighlighterLinePattern = InkLinePattern.Dotted,
            ToolbarPresets = [new ToolbarPresetPreference { LinePattern = InkLinePattern.Dotted }]
        };
        await settings.SaveAsync(preferences);
        var loaded = await settings.LoadAsync();
        Assert.Equal(preferences.PenLinePattern, loaded.PenLinePattern);
        Assert.Equal(preferences.HighlighterLinePattern, loaded.HighlighterLinePattern);
        Assert.Equal(InkLinePattern.Dotted, loaded.ToolbarPresets.Single().LinePattern);
        CanvasObject stroke = new InkStrokeObject { Points = [new(1, 2)], Style = new InkStyle { LinePattern = InkLinePattern.Dashed } };
        var copy = JsonSerializer.Deserialize<CanvasObject>(JsonSerializer.Serialize(stroke, HoomNoteJson.Options), HoomNoteJson.Options);
        Assert.Equal(InkLinePattern.Dashed, Assert.IsType<InkStrokeObject>(copy).Style.LinePattern);
    }
}
