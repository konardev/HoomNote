namespace HoomNote.Core.Documents;

public static class ImageCrop
{
    public static RectD PixelBounds(RectD selection, int width, int height)
    {
        if (width <= 0 || height <= 0 || !selection.IsFinite || selection.Width <= 0 || selection.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(selection));
        var left = Math.Clamp(Math.Floor(selection.Left), 0, width - 1);
        var top = Math.Clamp(Math.Floor(selection.Top), 0, height - 1);
        var right = Math.Clamp(Math.Ceiling(selection.Right), left + 1, width);
        var bottom = Math.Clamp(Math.Ceiling(selection.Bottom), top + 1, height);
        return new RectD(left, top, right - left, bottom - top);
    }

    public static ImageObject Apply(ImageObject image, RectD pixels, int width, int height, string assetHash)
    {
        var crop = PixelBounds(pixels, width, height);
        var visible = ImageLayout.Destination(image.Bounds, width, height, image.PreserveAspectRatio);
        return image with
        {
            AssetHash = assetHash,
            Bounds = new RectD(visible.X + crop.X / width * visible.Width,
                visible.Y + crop.Y / height * visible.Height,
                crop.Width / width * visible.Width, crop.Height / height * visible.Height)
        };
    }
}
