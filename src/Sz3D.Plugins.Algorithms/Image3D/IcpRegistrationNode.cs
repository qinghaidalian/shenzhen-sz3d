using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Algorithms.Image3D;

[NodeType(Category = "图像处理 3D", Name = "ICP 配准", Description = "点云模板匹配与精配准")]
public sealed class IcpRegistrationNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Source", typeof(PointCloud), PortDirection.Input), new("Target", typeof(PointCloud), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Aligned", typeof(PointCloud), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO: ICP 实现
        ctx.SetOutput("Aligned", ctx.GetInput<PointCloud>("Source"));
        return Task.CompletedTask;
    }
}
