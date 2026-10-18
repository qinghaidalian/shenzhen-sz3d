using System.IO;
using System.Windows.Media.Imaging;

namespace Sz3D.Presentation.WPF.Services;

public static class ImageHelper
{
    /// <summary>把图像字节（PNG/JPEG/BMP 等）解码为可显示的 BitmapImage。</summary>
    public static BitmapImage? BytesToBitmap(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(bytes);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }
}
