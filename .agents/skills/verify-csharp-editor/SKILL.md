---
name: verify-csharp-editor
description: Verify C# HoomNote image cropping and pen line styles with isolated data, regression tests, and native Windows UI checks. Use after changes to these editor paths.
---

# C# editor verification

Run PowerShell from C:\Users\konar\Documents\OpenNote. This workspace also contains unrelated Rust work. The current build-release.ps1 delegates to Rust, so use the C# project directly.

## Automated checks

```powershell
dotnet test HoomNote.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
dotnet build src/HoomNote.App/HoomNote.App.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -p:OutDir=C:/Users/konar/Documents/OpenNote/artifacts/CSharp-Crop-0.8.6/
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Whitespace check failed' }
```

Use the versioned output folder for this feature build. Update its version when preparing a subsequent release. Restore normally if dependencies are missing. Testhost hung inside the restricted execution sandbox in this session; the same suite completed outside it. Do not mistake a started testhost for a passing run.

## Native UI checks

The user stopped Computer Use during the first feature pass. Do not resume desktop input until the user permits it. These crop-anchor steps are pending, not verified.

When desktop testing is permitted, use the computer-use skill and its supported sky API. Launch a disposable library, retaining the PID. Do not open the normal user library for tests.

```powershell
$env:HOOMNOTE_DATA_DIR = Join-Path (Get-Location) 'artifacts/csharp-feature-qa/data'
$qaProcess = Start-Process -FilePath 'artifacts/CSharp-Crop-0.8.6/HoomNote.exe' -WindowStyle Hidden -PassThru
$qaProcess.Id | Set-Content artifacts/csharp-feature-qa-pid.txt
```

Readiness means exactly one returned HoomNote window for that executable, displaying its library. A PID alone is insufficient. Select that returned window before any input. Confirm the disposable database and settings files are under HOOMNOTE_DATA_DIR. The environment override isolates library data and preferences; existing diagnostic logging may still use the normal application log location.

| Path | Actions and expected result |
| --- | --- |
| Crop entry | Add a test image to a disposable notebook. Right-click it and choose Crop. No dialog opens. The image stays fixed, edge handles become bars, corner handles become L-shaped bars, and a confirm checkmark replaces the lock button. A cancel button is also visible. |
| Adjust | Drag each edge and corner inward, then outward. The excluded area dims. The crop cannot leave the original image or shrink below one source pixel. Check a rotated image and different zoom levels. |
| Confirm | Click the checkmark or press Enter. Only the retained pixels remain in the same page position. The lock button returns. Undo restores the full image; Redo reapplies the crop. Save, close, and reopen the test notebook and confirm the result persists. |
| Cancel | Adjust a crop, then click Cancel or press Escape. The original image and history remain unchanged. Switching tools or pages also exits the pending crop. |
| Input | Exercise mouse and pen. Check native touch separately where supported. A canceled pointer restores the crop boundary from before that drag. Confirm cannot submit an active drag. |
| Pen styles | In pen options choose Solid, Dotted, and Dashed and draw each. Repeat with the highlighter and saved presets. Compare live strokes, committed strokes, thumbnails, and reopened notes. Export PDF and SVG and visually confirm their gaps and colors. |

Keep screenshots and any exported verification files under artifacts/csharp-feature-qa. Record which interaction and renderer produced each capture. Automated geometry tests do not prove handle visibility or native input routing.

Close only the test instance when desktop control is permitted. Preserve the disposable data and evidence for follow-up. Do not publish or change the Rust implementation as part of verification.

## Evidence from 2026-10-08

- 160 Core and 64 Infrastructure tests passed. See artifacts/crop-anchor-tests.log.
- The C# Release build and its warning/error counts are in artifacts/crop-anchor-build.log.
- The earlier dotted-pen path was exercised in a separate C# test library before Computer Use was stopped.
- The revised canvas crop handles, confirm/cancel controls, dashed/highlighter interaction, and native-touch path have not been visually verified.
