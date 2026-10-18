using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Core.Operators.Basic;

/// <summary>条件分支节点——根据布尔输入路由数据至 True 或 False 端口。</summary>
[NodeType(Category = "基础", Name = "条件分支", Description = "IF/ELSE 数据路由")]
public sealed class ConditionalNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts =>
    [
        new("Condition", typeof(bool),   PortDirection.Input),
        new("Data",      typeof(object), PortDirection.Input)
    ];
    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("True",  typeof(object), PortDirection.Output),
        new("False", typeof(object), PortDirection.Output)
    ];

    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        var condition = ctx.GetInput<bool>("Condition");
        var data      = ctx.GetInput<object>("Data");
        if (condition) ctx.SetOutput("True",  data);
        else           ctx.SetOutput("False", data);
        return Task.CompletedTask;
    }
}
