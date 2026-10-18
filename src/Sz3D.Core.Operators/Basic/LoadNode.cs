using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Core.Operators.Basic;

/// <summary>加载节点——从文件系统读取图像或点云并输出。</summary>
[NodeType(Category = "基础", Name = "加载文件", Description = "从文件加载图像（PNG/JPG/BMP/TIFF）或点云（.pcd/.ply/.bin）")]
public sealed class LoadNode : NodeBase
{
    [NodeParameter] public string FilePath { get; set; } = "";

    public override IReadOnlyList<PortDescriptor> InputPorts  => [];
    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("Data", typeof(object), PortDirection.Output)
    ];

    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        if (!File.Exists(FilePath))
        {
            ctx.LogError($"File not found: {FilePath}");
            throw new FileNotFoundException(FilePath);
        }
        // TODO: 根据扩展名路由到图像/点云加载器
        ctx.LogInfo($"Loaded: {FilePath}");
        return Task.CompletedTask;
    }
}

/// <summary>节点参数特性（供属性面板反射读取）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NodeParameterAttribute : Attribute { }

