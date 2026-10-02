using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HobbiesClub.App;

public static class PhotoHelper
{
    /// <summary>Turns stored JPEG bytes into an image for WPF, or null if there is no photo.</summary>
    public static ImageSource? ToImage(byte[]? jpeg)
    {
        if (jpeg is null || jpeg.Length == 0)
            return null;

        var image = new BitmapImage();
        using var ms = new MemoryStream(jpeg);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
