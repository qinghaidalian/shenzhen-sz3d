using System.Globalization;
using System.IO;
using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Algorithms.IO;

/// <summary>基础算子：从磁盘加载一张图片，输出图像字节。</summary>
[NodeType(Category = "基础算子", Name = "加载图片", Description = "读取图片文件，输出图像数据")]
public sealed class LoadImageNode : NodeBase
{
    [NodeParameter] public string FilePath { get; set; } = "";

    public override IReadOnlyList<PortDescriptor> InputPorts  => [];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Image", typeof(byte[]), PortDirection.Output)];

    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        if (!File.Exists(FilePath))
        {
            ctx.LogError($"图片文件不存在: {FilePath}");
            throw new FileNotFoundException("图片文件不存在", FilePath);
        }

        var bytes = File.ReadAllBytes(FilePath);
        ctx.SetOutput("Image", bytes);
        ctx.LogInfo($"已加载图片 {Path.GetFileName(FilePath)}（{bytes.Length} 字节）");
        return Task.CompletedTask;
    }
}

/// <summary>基础算子：从文本 XYZ/CSV 文件加载点云。</summary>
[NodeType(Category = "基础算子", Name = "加载点云", Description = "读取 XYZ/CSV 点云文件（每行 x y z），输出点云")]
public sealed class LoadPointCloudNode : NodeBase
{
    [NodeParameter] public string FilePath { get; set; } = "";

    public override IReadOnlyList<PortDescriptor> InputPorts  => [];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("PointCloud", typeof(PointCloud), PortDirection.Output)];

    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        if (!File.Exists(FilePath))
        {
            ctx.LogError($"点云文件不存在: {FilePath}");
            throw new FileNotFoundException("点云文件不存在", FilePath);
        }

        var cloud = LoadXyz(FilePath);
        ctx.SetOutput("PointCloud", cloud);
        ctx.LogInfo($"已加载点云 {Path.GetFileName(FilePath)}（{cloud.Count} 点）");
        return Task.CompletedTask;
    }

    private static PointCloud LoadXyz(string path)
    {
        var xs = new List<float>();
        var ys = new List<float>();
        var zs = new List<float>();

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//")) continue;

            // 支持空格 / 逗号 / 制表符分隔，取前三列作为 x y z
            var parts = line.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                continue;

            xs.Add(x); ys.Add(y); zs.Add(z);
        }

        return new PointCloud { X = xs.ToArray(), Y = ys.ToArray(), Z = zs.ToArray() };
    }
}
