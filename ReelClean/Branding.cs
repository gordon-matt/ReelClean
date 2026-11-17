using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ReelClean;

static class Branding
{
    public const string LogoUri = "avares://ReelClean/Assets/Logo.png";

    private static WindowIcon? _icon;

    public static WindowIcon Icon => _icon ??= CreateIcon();

    private static WindowIcon CreateIcon()
    {
        using var stream = AssetLoader.Open(new Uri(LogoUri));
        var bitmap = new Bitmap(stream);
        int side = Math.Min(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var crop = new CroppedBitmap
        {
            Source = bitmap,
            SourceRect = new PixelRect(0, 0, side, side),
        };
        var rendered = new RenderTargetBitmap(new PixelSize(side, side));
        using (var context = rendered.CreateDrawingContext())
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse("#151D2E")), new Rect(0, 0, side, side));
            context.DrawImage(crop, new Rect(0, 0, side, side));
        }

        return new WindowIcon(rendered);
    }
}
