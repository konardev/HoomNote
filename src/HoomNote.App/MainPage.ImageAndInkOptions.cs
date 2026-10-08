using HoomNote.Core.Documents;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HoomNote_App;

public sealed partial class MainPage
{
    private MenuFlyoutItem _canvasCropMenuItem = null!;
    private ImageObject? _cropContextImage;
    private bool _syncingInkPattern;

    private InkLinePattern CurrentLinePattern(EditorTool tool)
    {
        var pattern = tool == EditorTool.Highlighter
            ? _userPreferences.HighlighterLinePattern : _userPreferences.PenLinePattern;
        return Enum.IsDefined(pattern) ? pattern : InkLinePattern.Solid;
    }

    private void SyncInkLinePatternPicker()
    {
        if (InkLinePatternPicker is null) return;
        _syncingInkPattern = true;
        InkLinePatternPicker.SelectedIndex = (int)CurrentLinePattern(_colorTool);
        _syncingInkPattern = false;
    }

    private void SetInkLinePattern(InkLinePattern pattern)
    {
        if (!Enum.IsDefined(pattern)) pattern = InkLinePattern.Solid;
        _userPreferences = _colorTool == EditorTool.Highlighter
            ? _userPreferences with { HighlighterLinePattern = pattern }
            : _userPreferences with { PenLinePattern = pattern };
        SyncInkLinePatternPicker();
    }

    private void OnInkLinePatternChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _syncingInkPattern || InkLinePatternPicker is null || InkLinePatternPicker.SelectedIndex < 0) return;
        SetInkLinePattern((InkLinePattern)InkLinePatternPicker.SelectedIndex);
        if (!_applyingToolbarPreset && _activeToolbarPresetId is { } id)
        {
            var index = _userPreferences.ToolbarPresets.FindIndex(preset => preset.Id == id);
            if (index >= 0)
                _userPreferences.ToolbarPresets[index] = _userPreferences.ToolbarPresets[index] with
                {
                    LinePattern = CurrentLinePattern(_colorTool)
                };
        }
        ScheduleUserPreferencesSave();
    }

}
