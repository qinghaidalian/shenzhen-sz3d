using Sz3D.Plugins.Algorithms.Image3D;
using Sz3D.Plugins.Algorithms.Calibration;
using Sz3D.Plugins.Algorithms.Communication;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Plugins.Algorithms;

public sealed class AlgorithmPlugin : INodePlugin
{
    public string PluginId   => "sz3d.plugin.algorithms";
    public string PluginName => "图像处理 & 标定 & 通讯节点库";
    public string Version    => "1.0.0";

    public IReadOnlyList<NodeTypeDescriptor> NodeTypes =>
    [
        new("sz3d.plugin.algorithms:GaussianBlur","图像处理 2D","高斯滤波",
            "2D 高斯滤波","1.0.0",false,() => new Image2D.GaussianBlurNode()),
        new("sz3d.plugin.algorithms:CannyEdge","图像处理 2D","Canny 边缘检测",
            "Canny 算子边缘检测","1.0.0",false,() => new Image2D.CannyEdgeNode()),
        new("sz3d.plugin.algorithms:IcpRegistration","ICP 点云配准","ICP 配准",
            "点云模板匹配与精配准","1.0.0",false,() => new Image3D.IcpRegistrationNode()),
        new("sz3d.plugin.algorithms:EyeInHand","标定","Eye-In-Hand 手眼标定",
            "相机固定于机器人末端的手眼标定","1.0.0",false,() => new Calibration.EyeInHandCalibrationNode()),
        new("sz3d.plugin.algorithms:TcpClient","通讯","TCP 客户端",
            "TCP/IP 客户端通讯节点","1.0.0",false,() => new Communication.TcpClientNode()),
    ];
}

