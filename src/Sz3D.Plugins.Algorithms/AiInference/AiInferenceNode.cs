using Sz3D.Backend.AiService.Grpc;
using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Google.Protobuf;

namespace Sz3D.Plugins.Algorithms.AiInference;

/// <summary>
/// AI 推理节点（gRPC 客户端）。
/// 连接 Sz3D.Backend.AiService gRPC 服务端执行 ONNX 推理，
/// 服务端与 WPF 主进程完全隔离。
/// </summary>
[NodeType(
    Category    = "深度学习",
    Name        = "AI 推理（gRPC）",
    Description = "通过 gRPC 调用 AiService 进行 ONNX 模型推理，支持 GPU 加速与大模型")]
public sealed class AiInferenceNode : NodeBase
{
    [NodeParameter] public string ModelId      { get; set; } = "";
    [NodeParameter] public string ModelPath    { get; set; } = "";  // 服务端路径
    [NodeParameter] public string ServerAddress { get; set; } = "http://localhost:50051";

    private AiServiceClientFactory? _factory;

    public override IReadOnlyList<PortDescriptor> InputPorts =>
    [
        new("Image",       typeof(byte[]), PortDirection.Input),
        new("ImageWidth",  typeof(int),    PortDirection.Input),
        new("ImageHeight", typeof(int),    PortDirection.Input)
    ];

    public override IReadOnlyList<PortDescriptor> OutputPorts =>
    [
        new("Detections",  typeof(IList<BoundingBox>), PortDirection.Output),
        new("Scores",      typeof(IList<float>),       PortDirection.Output),
        new("Labels",      typeof(IList<string>),      PortDirection.Output),
        new("InferenceMs", typeof(long),               PortDirection.Output)
    ];

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(ModelId))
            return ValidationResult.Fail("请填写 ModelId。");
        if (string.IsNullOrWhiteSpace(ModelPath))
            return ValidationResult.Fail("请填写服务端 ModelPath。");
        return ValidationResult.Success;
    }

    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        var valid = Validate();
        if (!valid.IsValid) throw new InvalidOperationException(valid.Message);

        // 延迟初始化 Channel（地址可能在运行时从属性面板更新）
        if (_factory is null || _factory.IsDisposed())
            _factory = new AiServiceClientFactory(ServerAddress);

        var client = _factory.CreateClient();

        // 确保模型已加载（幂等操作）
        ctx.LogInfo($"Ensuring model '{ModelId}' is loaded on AiService...");
        var loadResp = await client.LoadModelAsync(
            new LoadModelRequest { ModelId = ModelId, ModelPath = ModelPath },
            cancellationToken: ct);

        if (!loadResp.Success)
            throw new InvalidOperationException($"Model load failed: {loadResp.ErrorMessage}");

        // 执行推理
        var imageBytes  = ctx.GetInput<byte[]>("Image");
        var imageWidth  = ctx.GetInput<int>("ImageWidth");
        var imageHeight = ctx.GetInput<int>("ImageHeight");

        ctx.LogInfo($"Sending inference request to AiService (model: {ModelId})...");
        var inferResp = await client.InferAsync(
            new InferRequest
            {
                ModelId     = ModelId,
                ImageData   = ByteString.CopyFrom(imageBytes),
                ImageWidth  = imageWidth,
                ImageHeight = imageHeight
            },
            cancellationToken: ct);

        if (!inferResp.Success)
            throw new InvalidOperationException($"Inference failed: {inferResp.ErrorMessage}");

        ctx.SetOutput("Detections",  inferResp.Detections);
        ctx.SetOutput("Scores",      inferResp.Scores.ToList());
        ctx.SetOutput("Labels",      inferResp.Labels.ToList());
        ctx.SetOutput("InferenceMs", inferResp.InferenceMs);

        ctx.LogInfo($"Inference completed in {inferResp.InferenceMs} ms.");
    }
}

// 扩展方法：检查 factory 是否已释放
file static class FactoryExtensions
{
    public static bool IsDisposed(this AiServiceClientFactory f)
    {
        try { f.CreateClient(); return false; }
        catch (ObjectDisposedException) { return true; }
    }
}
