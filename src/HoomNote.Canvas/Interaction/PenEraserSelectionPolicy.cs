namespace HoomNote.Canvas.Interaction;

public static class PenEraserSelectionPolicy
{
    // Touching a selection with the pen's eraser end deletes the whole selection, like the
    // Delete key. Touching anywhere else keeps the normal stroke eraser.
    public static bool ShouldDeleteSelection(
        bool isPen,
        bool isEraser,
        bool readMode,
        int deletableSelectedCount,
        bool touchesSelection) =>
        isPen && isEraser && !readMode && deletableSelectedCount > 0 && touchesSelection;
}
