using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sz3D.Plugins.Algorithms.Image2D;

/// <summary>
/// 2D 图像算子共用的编解码辅助类。
/// 画布数据流中的图像以「编码后的文件字节」（PNG/JPEG/BMP 等）形式在端口间传递，
/// 本类负责在编码字节与 <see cref="BitmapSource"/> 之间转换，并把处理结果重新编码为 PNG 字节。
/// 使用 WPF 成像（System.Windows.Media.Imaging），无需额外第三方依赖。
/// </summary>
internal static class ImageCodec
{
    /// <summary>把编码图像字节（PNG/JPEG/BMP 等）解码为冻结的 <see cref="BitmapSource"/>。</summary>
    /// <param name="bytes">编码后的图像文件字节。</param>
    /// <returns>解码得到的位图；解码失败或输入为空时返回 <c>null</c>。</returns>
    public static BitmapSource? Decode(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;

        var decoder = BitmapDecoder.Create(
            new MemoryStream(bytes),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    /// <summary>把 <see cref="BitmapSource"/> 编码为 PNG 字节。</summary>
    /// <param name="image">待编码的位图。</param>
    /// <returns>PNG 格式的图像字节。</returns>
    public static byte[] EncodePng(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>把位图转换为 8 位灰度（<see cref="PixelFormats.Gray8"/>）。</summary>
    /// <param name="source">源位图。</param>
    /// <returns>冻结的灰度位图。</returns>
    public static BitmapSource ToGray8(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, destinationPalette: null, alphaThreshold: 0);
        gray.Freeze();
        return gray;
    }
}
