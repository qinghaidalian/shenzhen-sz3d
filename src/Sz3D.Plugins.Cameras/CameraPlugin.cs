using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Plugins.Cameras;

/// <summary>图漾相机插件包——注册 TyCamera 类所有节点。</summary>
public sealed class CameraPlugin : INodePlugin
{
    public string PluginId   => "sz3d.plugin.cameras";
    public string PluginName => "图漾相机节点库";
    public string Version    => "1.0.0";

    public IReadOnlyList<NodeTypeDescriptor> NodeTypes =>
    [
        new NodeTypeDescriptor(
            TypeId: $"{PluginId}:Percipio3DCamera",
            Category: "相机 SDK",
            Name: "图漾 3D 相机",
            Description: "图漾 Percipio 3D 相机采集节点（camport3 P/Invoke）",
            Version: "1.0.0",
            IsPhase2: false,
            Factory: () => new TyCamera.Percipio3DCameraNode()),

        new NodeTypeDescriptor(
            TypeId: $"{PluginId}:TcpCamera",
            Category: "相机 SDK",
            Name: "TCP 3D 相机",
            Description: "通过 TCP/IP 连接 3D 相机采集点云（通用协议骨架，解析逻辑待按厂商协议替换）",
            Version: "1.0.0",
            IsPhase2: false,
            Factory: () => new TcpCamera.TcpCameraNode())
    ];
}
