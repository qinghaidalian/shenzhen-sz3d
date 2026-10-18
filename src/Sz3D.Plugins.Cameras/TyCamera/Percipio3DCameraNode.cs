using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Cameras.TyCamera;

/// <summary>
/// 图漾 3D 相机节点——通过 P/Invoke 封装 camport3 C API。
/// 无硬件时自动降级为 SimulatedCameraNode。
/// </summary>
[NodeType(Category = "相机 SDK", Name = "图漾 3D 相机",
    Description = "设备枚举/连接/参数配置/彩色深度图采集")]
public sealed class Percipio3DCameraNode : NodeBase
{
    [NodeParameter] public int    ExposureMs { get; set; } = 33;
    [NodeParameter] public float  Gain       { get; set; } = 1.0f;
    [NodeParameter] public bool   SimMode    { get; set; } = true; // 默认模拟模式

    public override IReadOnlyList<PortDescriptor> InputPorts  => [];
    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("ColorImage", typeof(byte[]),     PortDirection.Output),
        new("PointCloud", typeof(PointCloud), PortDirection.Output)
    ];

    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        if (SimMode)
        {
            ctx.LogWarning("相机 SDK 不可用，使用模拟数据。");
            ctx.SetOutput("ColorImage", GenerateSimulatedImage());
            ctx.SetOutput("PointCloud", GenerateSimulatedCloud());
        }
        else
        {
            // TODO: P/Invoke 调用 camport3 SDK
            // TyCameraWrapper.OpenDevice(ExposureMs, Gain);
            // var frame = TyCameraWrapper.CaptureFrame();
            throw new NotImplementedException("真实相机 SDK 封装待实现。");
        }
        await Task.CompletedTask;
    }

    private static PointCloud GenerateSimulatedCloud()
    {
        // 生成 1000 点随机点云用于测试
        var rng = new Random(42);
        int n = 1000;
        return new PointCloud
        {
            X = Enumerable.Range(0, n).Select(_ => (float)rng.NextDouble()).ToArray(),
            Y = Enumerable.Range(0, n).Select(_ => (float)rng.NextDouble()).ToArray(),
            Z = Enumerable.Range(0, n).Select(_ => (float)rng.NextDouble()).ToArray()
        };
    }

    /// <summary>生成一张 256×256 渐变 24-bit BMP，作为模拟彩色图像。</summary>
    private static byte[] GenerateSimulatedImage(int w = 256, int h = 256)
    {
        int rowSize  = (w * 3 + 3) & ~3;   // 4 字节对齐
        int dataSize = rowSize * h;
        var bmp = new byte[54 + dataSize];

        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        WriteInt(bmp, 2, bmp.Length);      // 文件大小
        WriteInt(bmp, 10, 54);             // 像素数据偏移
        WriteInt(bmp, 14, 40);             // DIB 头大小
        WriteInt(bmp, 18, w);              // 宽
        WriteInt(bmp, 22, h);              // 高
        bmp[26] = 1;                       // 平面数
        bmp[28] = 24;                      // 每像素位数
        WriteInt(bmp, 34, dataSize);       // 图像数据大小

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = 54 + y * rowSize + x * 3;
            bmp[i]     = (byte)(x * 255 / (w - 1));   // B
            bmp[i + 1] = (byte)(y * 255 / (h - 1));   // G
            bmp[i + 2] = 128;                         // R
        }
        return bmp;
    }

    private static void WriteInt(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}

