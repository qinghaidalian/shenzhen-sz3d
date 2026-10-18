using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Core.Operators.Basic;

/// <summary>触发器节点——向下游节点发出执行信号。</summary>
[NodeType(Category = "基础", Name = "触发器", Description = "产生执行触发信号启动节点图流程")]
public sealed class TriggerNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts  => [];
    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("Trigger", typeof(bool), PortDirection.Output)
    ];

    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        ctx.SetOutput("Trigger", true);
        ctx.LogInfo("Trigger fired.");
        return Task.CompletedTask;
    }
}
