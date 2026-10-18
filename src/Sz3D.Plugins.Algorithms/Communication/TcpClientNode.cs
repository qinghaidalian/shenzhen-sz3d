using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Plugins.Algorithms.Communication;

[NodeType(Category = "通讯", Name = "TCP 客户端")]
public sealed class TcpClientNode : NodeBase
{
    [NodeParameter] public string Host { get; set; } = "127.0.0.1";
    [NodeParameter] public int Port    { get; set; } = 8080;
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Data", typeof(byte[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Response", typeof(byte[]), PortDirection.Output)];
    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO: System.Net.Sockets TcpClient
        ctx.SetOutput("Response", Array.Empty<byte>());
        await Task.CompletedTask;
    }
}
