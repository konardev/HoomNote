# HoomNote 0.8.7

- Deleting a notebook, including one inside a folder, now removes it from the library right away instead of after a restart.
- The library and folder contents show immediately when HoomNote starts. The library health check now runs in the background, and if it finds damage, HoomNote restores from the latest backup on the next start.
- Touching a lasso or box selection with the eraser end of the pen now deletes everything selected in one step. Undo brings it all back. Touching outside the selection still erases strokes as before.
- Fixed notebooks failing to open when HoomNote.exe is started directly, such as from the portable app.
