using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Cameras.TcpCamera;

/// <summary>
/// 通用 TCP/IP 3D 相机采集节点——通过 TCP socket 连接相机、触发采集、接收并解析点云。
/// 无相机或连接失败时自动降级为模拟数据，保证工作流可跑通。
///
/// 真机对接时只需替换 <see cref="TcpCameraClient"/> 内的协议解析，本节点无需改动。
/// </summary>
[NodeType(Category = "相机 SDK", Name = "TCP 3D 相机",
    Description = "通过 TCP/IP 连接 3D 相机采集点云（通用协议骨架，解析逻辑待按厂商协议替换）")]
public sealed class TcpCameraNode : NodeBase
{
    /// <summary>相机 IP 或主机名。</summary>
    [NodeParameter] public string Host { get; set; } = "192.168.1.100";

    /// <summary>相机数据端口。</summary>
    [NodeParameter] public int Port { get; set; } = 8000;

    /// <summary>连接与读写超时（毫秒）。</summary>
    [NodeParameter] public int TimeoutMs { get; set; } = 5000;

    /// <summary>模拟模式：为 true 时不连真机，直接输出模拟点云（默认开启）。</summary>
    [NodeParameter] public bool SimMode { get; set; } = true;

    public override IReadOnlyList<PortDescriptor> InputPorts => [];

    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("PointCloud", typeof(PointCloud), PortDirection.Output)
    ];

    /// <summary>参数校验：非模拟模式下校验主机与端口。</summary>
    public override ValidationResult Validate()
    {
        if (SimMode)
            return ValidationResult.Success;

        if (string.IsNullOrWhiteSpace(Host))
            return ValidationResult.Fail("Host 不能为空。");
        if (Port is <= 0 or > 65535)
            return ValidationResult.Fail($"Port 非法：{Port}（应在 1~65535）。");

        return ValidationResult.Success;
    }

    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken cancellationToken)
    {
        if (SimMode)
        {
            ctx.LogWarning("TCP 相机处于模拟模式，输出模拟点云。");
            ctx.SetOutput("PointCloud", GenerateSimulatedCloud());
            return;
        }

        try
        {
            ctx.LogInfo($"连接 TCP 相机 {Host}:{Port} ...");
            using var client = new TcpCameraClient(Host, Port, TimeoutMs);

            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            ctx.LogInfo("连接成功，开始采集一帧点云。");

            var cloud = await client.CaptureAsync(cancellationToken).ConfigureAwait(false);
            ctx.LogInfo($"采集完成，共 {cloud.Count} 个点。");

            ctx.SetOutput("PointCloud", cloud);
        }
        catch (OperationCanceledException)
        {
            // 取消由引擎统一处理，向上抛出即可
            throw;
        }
        catch (Exception ex)
        {
            // 采集失败不抛异常中断整条工作流，记录错误并降级为模拟数据
            ctx.LogError($"TCP 相机采集失败，降级为模拟数据：{ex.Message}");
            ctx.SetOutput("PointCloud", GenerateSimulatedCloud());
        }
    }

    /// <summary>生成 1000 点随机点云，用于模拟模式或采集失败降级。</summary>
    private static PointCloud GenerateSimulatedCloud()
    {
        var rng = new Random(42);
        const int n = 1000;
        return new PointCloud
        {
            X = Enumerable.Range(0, n).Select(_ => (float)rng.NextDouble()).ToArray(),
            Y = Enumerable.Range(0, n).Select(_ => (float)rng.NextDouble()).ToArray(),
            Z = Enumerable.Range(0, n).Select(_ => (float)rng.NextDouble()).ToArray()
        };
    }
}
