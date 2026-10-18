using Grpc.Net.Client;
using Sz3D.Backend.AiService.Grpc;

namespace Sz3D.Plugins.Algorithms.AiInference;

/// <summary>
/// gRPC 客户端工厂——管理到 AiService 的 Channel 复用。
/// 整个插件进程共享一个 GrpcChannel（线程安全）。
/// </summary>
public sealed class AiServiceClientFactory : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly string _serverAddress;

    public AiServiceClientFactory(string serverAddress = "http://localhost:50051")
    {
        _serverAddress = serverAddress;
        _channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 64 * 1024 * 1024,  // 64MB
            MaxSendMessageSize    = 64 * 1024 * 1024,
        });
    }

    /// <summary>创建 InferenceService 客户端 stub（轻量，可频繁创建）。</summary>
    public InferenceService.InferenceServiceClient CreateClient() =>
        new(_channel);

    /// <summary>检查服务是否可达。</summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var client   = CreateClient();
            var response = await client.HealthCheckAsync(new HealthCheckRequest(), cancellationToken: ct);
            return response.Healthy;
        }
        catch { return false; }
    }

    public void Dispose() => _channel.Dispose();
}
