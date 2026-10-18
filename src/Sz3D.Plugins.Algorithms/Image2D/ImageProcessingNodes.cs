using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Plugins.Algorithms.Image2D;

[NodeType(Category = "图像处理 2D", Name = "高斯滤波")]
public sealed class GaussianBlurNode : NodeBase
{
    [NodeParameter] public int KernelSize { get; set; } = 5;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO: OpenCvSharp GaussianBlur
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

[NodeType(Category = "图像处理 2D", Name = "Canny 边缘检测")]
public sealed class CannyEdgeNode : NodeBase
{
    [NodeParameter] public double Threshold1 { get; set; } = 100;
    [NodeParameter] public double Threshold2 { get; set; } = 200;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Edges", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO: OpenCvSharp Canny
        ctx.SetOutput("Edges", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}


// ───────────────────────────────────────────────────────────────────────────
// 以下为新增的 2D 图像处理算子（Phase 1）。
// 当前均为占位实现（直通输入），后续接入 OpenCvSharp 实际算法。
// ───────────────────────────────────────────────────────────────────────────

/// <summary>灰度转换——将彩色图像转换为单通道灰度图。</summary>
[NodeType(Category = "图像处理 2D", Name = "灰度转换")]
public sealed class GrayscaleNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        var input = ctx.GetInput<byte[]>("Image");
        try
        {
            var source = ImageCodec.Decode(input);
            if (source is null)
            {
                ctx.LogError("灰度转换：输入图像为空或无法解码，已原样输出。");
                ctx.SetOutput("Image", input);
                return Task.CompletedTask;
            }

            var gray = ImageCodec.ToGray8(source);
            ctx.SetOutput("Image", ImageCodec.EncodePng(gray));
        }
        catch (Exception ex)
        {
            // 处理失败不中断流程：记录日志并回退为原图
            ctx.LogError($"灰度转换失败: {ex.Message}");
            ctx.SetOutput("Image", input);
        }
        return Task.CompletedTask;
    }
}

/// <summary>二值化——按阈值将灰度图转为黑白二值图。</summary>
[NodeType(Category = "图像处理 2D", Name = "二值化")]
public sealed class ThresholdNode : NodeBase
{
    [NodeParameter] public double Threshold { get; set; } = 128;
    [NodeParameter] public double MaxValue  { get; set; } = 255;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp Threshold
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>均值滤波——使用归一化方框核做邻域平均，平滑噪声。</summary>
[NodeType(Category = "图像处理 2D", Name = "均值滤波")]
public sealed class MeanBlurNode : NodeBase
{
    [NodeParameter] public int KernelSize { get; set; } = 3;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp Blur
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>中值滤波——取邻域中值，适合去除椒盐噪声。</summary>
[NodeType(Category = "图像处理 2D", Name = "中值滤波")]
public sealed class MedianBlurNode : NodeBase
{
    [NodeParameter] public int KernelSize { get; set; } = 3;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp MedianBlur
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>图像锐化——通过拉普拉斯/锐化核增强边缘细节。</summary>
[NodeType(Category = "图像处理 2D", Name = "图像锐化")]
public sealed class SharpenNode : NodeBase
{
    [NodeParameter] public double Strength { get; set; } = 1.0;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp Filter2D 锐化核
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>直方图均衡化——拉伸灰度分布以增强对比度。</summary>
[NodeType(Category = "图像处理 2D", Name = "直方图均衡化")]
public sealed class HistogramEqualizeNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp EqualizeHist
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>形态学膨胀——扩张前景区域，填补细小空洞。</summary>
[NodeType(Category = "图像处理 2D", Name = "形态学膨胀")]
public sealed class DilateNode : NodeBase
{
    [NodeParameter] public int KernelSize { get; set; } = 3;
    [NodeParameter] public int Iterations { get; set; } = 1;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp Dilate
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>形态学腐蚀——收缩前景区域，去除细小噪点。</summary>
[NodeType(Category = "图像处理 2D", Name = "形态学腐蚀")]
public sealed class ErodeNode : NodeBase
{
    [NodeParameter] public int KernelSize { get; set; } = 3;
    [NodeParameter] public int Iterations { get; set; } = 1;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp Erode
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>仿射变换——对图像做缩放与旋转。</summary>
[NodeType(Category = "图像处理 2D", Name = "仿射变换")]
public sealed class AffineTransformNode : NodeBase
{
    [NodeParameter] public double Scale { get; set; } = 1.0;
    [NodeParameter] public double RotationDegrees { get; set; } = 0.0;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp GetRotationMatrix2D + WarpAffine
        ctx.SetOutput("Image", ctx.GetInput<byte[]>("Image"));
        return Task.CompletedTask;
    }
}

/// <summary>轮廓检测——在二值图上查找并输出轮廓。</summary>
[NodeType(Category = "图像处理 2D", Name = "轮廓检测")]
public sealed class FindContoursNode : NodeBase
{
    [NodeParameter] public double MinArea { get; set; } = 100;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Image", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("Image",    typeof(byte[]), PortDirection.Output),
        new("Contours", typeof(byte[]), PortDirection.Output),
    ];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO(图像处理 2D): OpenCvSharp FindContours + DrawContours
        var image = ctx.GetInput<byte[]>("Image");
        ctx.SetOutput("Image", image);
        ctx.SetOutput("Contours", image);
        return Task.CompletedTask;
    }
}
