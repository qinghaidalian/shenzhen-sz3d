using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Plugins.Robots;

public sealed class RobotPlugin : INodePlugin
{
    public string PluginId   => "sz3d.plugin.robots";
    public string PluginName => "机器人接口节点库";
    public string Version    => "1.0.0";

    public IReadOnlyList<NodeTypeDescriptor> NodeTypes =>
    [
        new("sz3d.plugin.robots:ModbusTcpRobot", "机器人接口", "Modbus TCP 机器人",
            "通用 Modbus TCP 机器人控制节点（基础验证协议）",
            "1.0.0", false, () => new Modbus.ModbusTcpRobotNode())
    ];
}
