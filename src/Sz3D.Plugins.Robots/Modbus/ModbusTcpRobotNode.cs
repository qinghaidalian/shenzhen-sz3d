using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Robots.Modbus;

/// <summary>Modbus TCP 机器人接口节点（基础验证协议，优先实现）。</summary>
[NodeType(Category = "机器人接口", Name = "Modbus TCP 机器人",
    Description = "通过 Modbus TCP 发送运动指令或读取关节状态")]
public sealed class ModbusTcpRobotNode : NodeBase
{
    [NodeParameter] public string Host    { get; set; } = "192.168.1.100";
    [NodeParameter] public int    Port    { get; set; } = 502;
    [NodeParameter] public int    TimeoutS { get; set; } = 5;

    public override IReadOnlyList<PortDescriptor> InputPorts =>
    [
        new("TargetPose", typeof(Pose), PortDirection.Input)
    ];
    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("JointState", typeof(float[]), PortDirection.Output)
    ];

    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO: NModbus 实现
        ctx.LogInfo($"Connecting to robot at {Host}:{Port}...");
        await Task.Delay(10, ct); // 模拟
        ctx.SetOutput("JointState", new float[6]);
    }
}

