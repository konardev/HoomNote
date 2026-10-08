using HoomNote.Canvas.Geometry;
using HoomNote.Canvas.Interaction;
using HoomNote.Core.Documents;
using HoomNote.Core.Editing;
using HoomNote_App.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace HoomNote_App;

public sealed partial class MainPage
{
    private ImageCropSelection? _imageCrop;
    private ImageCropSelection? _cropDragStart;
    private PointD _cropPointerStart;
    private TransformHandle _cropHandle;
    private uint? _cropPointerId;
    private int _cropRequestVersion;
    private bool _applyingImageCrop;

    private async void OnCanvasCropClick(object sender, RoutedEventArgs e)
    {
        if (_readMode || _document is null || _page is null || _assetStore is null ||
            _cropContextImage is not { IsLocked: false } original) return;
        CancelImageCrop();
        ActivateTool(EditorTool.Select);
        SelectSingleObject(original);
        var request = ++_cropRequestVersion;
        var document = _document;
        var page = _page;
        try
        {
            await using var input = await _assetStore.OpenReadAsync(original.AssetHash);
            using var randomInput = input.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomInput);
            if (request != _cropRequestVersion || _isPointerDown || _readMode || !ReferenceEquals(document, _document) ||
                !ReferenceEquals(page, _page) || !ReferenceEquals(_selectedObject, original) ||
                !page.Objects.Any(item => ReferenceEquals(item, original))) return;
            _imageCrop = new ImageCropSelection(original, checked((int)decoder.OrientedPixelWidth),
                checked((int)decoder.OrientedPixelHeight));
            StopTouchInertia(resumeBackgroundWork: false);
            CancelTouchGestureForPen();
            UpdateSelectionUi();
            InvalidateInteractionOverlay();
            DrawingSurface.Focus(FocusState.Programmatic);
            StatusText.Text = "Drag the crop bars, then confirm. Escape cancels.";
        }
        catch (Exception exception)
        {
            ShowError("The image could not be prepared for cropping.", exception);
        }
    }

    private void OnCancelImageCropClick(object sender, RoutedEventArgs e) => CancelImageCrop();

    private void CancelImageCrop()
    {
        _cropRequestVersion++;
        var active = _imageCrop is not null;
        _imageCrop = null;
        _cropPointerId = null;
        _cropDragStart = null;
        _cropHandle = TransformHandle.None;
        if (!active) return;
        DrawingSurface.ReleasePointerCaptures();
        UpdateSelectionUi();
        InvalidateInteractionOverlay();
    }

    private bool TryStartCropDrag(PointerRoutedEventArgs e)
    {
        if (_imageCrop is not { } crop) return false;
        var point = e.GetCurrentPoint(DrawingSurface);
        if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed) return false;
        e.Handled = true;
        if (_applyingImageCrop || _cropPointerId is not null) return true;
        if (point.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed) return true;
        DrawingSurface.Focus(FocusState.Pointer);
        _cropPointerStart = ScreenToPage(point.Position);
        _cropHandle = crop.HitHandle(_cropPointerStart, _zoom);
        if (_cropHandle == TransformHandle.None) return true;
        StopTouchInertia(resumeBackgroundWork: false);
        _cropPointerId = e.Pointer.PointerId;
        _cropDragStart = crop;
        DrawingSurface.CapturePointer(e.Pointer);
        return true;
    }

    private bool TryMoveCropDrag(PointerRoutedEventArgs e)
    {
        if (_cropPointerId != e.Pointer.PointerId || _cropDragStart is not { } start) return false;
        _imageCrop = start.Drag(_cropHandle, _cropPointerStart, ScreenToPage(e.GetCurrentPoint(DrawingSurface).Position));
        UpdateSelectionLockOverlay();
        InvalidateInteractionOverlay();
        e.Handled = true;
        return true;
    }

    private void HandleNativeCropFrame(NativeTouchFrameEventArgs e)
    {
        if (_imageCrop is not { } crop || _applyingImageCrop) return;
        var origin = DrawingSurface.TransformToVisual(null).TransformPoint(new Point(0, 0));
        foreach (var contact in e.Contacts)
        {
            var id = contact.Id | 0x80000000u;
            var position = new Point(contact.ClientX - origin.X, contact.ClientY - origin.Y);
            var world = ScreenToPage(position);
            if (contact.Action == NativeTouchAction.Down && _cropPointerId is null)
            {
                if (position.X < 0 || position.Y < 0 || position.X > DrawingSurface.ActualWidth ||
                    position.Y > DrawingSurface.ActualHeight) continue;
                _cropHandle = crop.HitHandle(world, _zoom);
                if (_cropHandle == TransformHandle.None) continue;
                _cropPointerId = id;
                _cropPointerStart = world;
                _cropDragStart = crop;
            }
            else if (_cropPointerId == id && _cropDragStart is { } start)
            {
                _imageCrop = start.Drag(_cropHandle, _cropPointerStart, world);
                if (contact.Action == NativeTouchAction.Up)
                {
                    _cropPointerId = null;
                    _cropDragStart = null;
                    _cropHandle = TransformHandle.None;
                }
                UpdateSelectionLockOverlay();
                InvalidateInteractionOverlay();
            }
        }
    }

    private bool TryEndCropDrag(PointerRoutedEventArgs e, bool canceled = false)
    {
        if (_cropPointerId != e.Pointer.PointerId) return false;
        if (canceled) _imageCrop = _cropDragStart;
        else TryMoveCropDrag(e);
        _cropPointerId = null;
        _cropDragStart = null;
        _cropHandle = TransformHandle.None;
        DrawingSurface.ReleasePointerCapture(e.Pointer);
        UpdateSelectionLockOverlay();
        InvalidateInteractionOverlay();
        e.Handled = true;
        return true;
    }

    private void DrawImageCrop(CanvasDrawingSession session, ImageCropSelection crop)
    {
        var full = crop.FullBounds;
        var keep = crop.Bounds;
        var shade = Color.FromArgb(150, 0, 0, 0);
        RectD[] excluded =
        [
            new(full.Left, full.Top, full.Width, keep.Top - full.Top),
            new(full.Left, keep.Bottom, full.Width, full.Bottom - keep.Bottom),
            new(full.Left, keep.Top, keep.Left - full.Left, keep.Height),
            new(keep.Right, keep.Top, full.Right - keep.Right, keep.Height)
        ];
        foreach (var rect in excluded)
        {
            if (rect.Width <= 0 || rect.Height <= 0) continue;
            using var geometry = CreateWindingGeometry(session, rect.Corners().Select(crop.Image.Transform.Apply).ToArray());
            session.FillGeometry(geometry, shade);
        }
        var outline = keep.Corners().Select(crop.Image.Transform.Apply).ToArray();
        var scale = (float)Math.Max(_zoom, 0.01);
        var white = Color.FromArgb(255, 255, 255, 255);
        var halo = Color.FromArgb(255, 15, 20, 26);
        for (var index = 0; index < outline.Length; index++)
            session.DrawLine(outline[index].ToVector2(), outline[(index + 1) % outline.Length].ToVector2(), white, 1 / scale);
        foreach (var bar in crop.Bars(_zoom))
        {
            session.DrawLine(bar.Start.ToVector2(), bar.End.ToVector2(), halo, 7 / scale);
            session.DrawLine(bar.Start.ToVector2(), bar.End.ToVector2(), white, 4 / scale);
        }
    }

    private async Task ConfirmImageCropAsync()
    {
        if (_applyingImageCrop || _cropPointerId is not null || _imageCrop is not { } crop ||
            _readMode || _document is null || _page is null || _assetStore is null) return;
        var original = crop.Image;
        var document = _document;
        var page = _page;
        var assets = _assetStore;
        var pixelsToKeep = crop.Pixels;
        if (pixelsToKeep == new RectD(0, 0, crop.PixelWidth, crop.PixelHeight))
        {
            CancelImageCrop();
            return;
        }
        _applyingImageCrop = true;
        UpdateSelectionLockOverlay();
        try
        {
            await using var input = await assets.OpenReadAsync(original.AssetHash);
            using var randomInput = input.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomInput);
            var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                new BitmapTransform { Bounds = new BitmapBounds
                {
                    X = (uint)pixelsToKeep.X, Y = (uint)pixelsToKeep.Y,
                    Width = (uint)pixelsToKeep.Width, Height = (uint)pixelsToKeep.Height
                } }, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                (uint)pixelsToKeep.Width, (uint)pixelsToKeep.Height, 96, 96, pixels.DetachPixelData());
            await encoder.FlushAsync();
            output.Seek(0);
            var hash = await assets.AddAsync(output.AsStreamForRead(), ".png");
            if (!ReferenceEquals(crop, _imageCrop) || !ReferenceEquals(document, _document) ||
                !ReferenceEquals(page, _page) || !page.Objects.Any(item => ReferenceEquals(item, original))) return;
            var cropped = ImageCrop.Apply(original, pixelsToKeep, crop.PixelWidth, crop.PixelHeight, hash);
            CancelImageCrop();
            _history.Execute(new ReplaceObjectsCommand(page.Id, [original], [cropped], "Crop image"), document);
            SelectSingleObject(cropped);
            OnDocumentChanged(recognizeInk: false, dirtyRegions: DirtyRegionsForObjects([original, cropped]));
            StatusText.Text = "Image cropped";
        }
        catch (Exception exception)
        {
            ShowError("The image could not be cropped.", exception);
        }
        finally
        {
            _applyingImageCrop = false;
            UpdateSelectionLockOverlay();
        }
    }
}
