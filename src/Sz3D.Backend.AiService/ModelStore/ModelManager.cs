using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;

namespace Sz3D.Backend.AiService.ModelStore;

/// <summary>模型类型，决定推理输出的解析方式。</summary>
public enum ModelType { Unspecified, MaskRcnn, Yolo, KeyPoint }

/// <summary>推理设备类型。</summary>
public enum DeviceProvider { Cpu, Cuda }

/// <summary>模型配置——服务端注册表条目与加载参数的统一载体。</summary>
public sealed record ModelConfig(
    string ModelId,
    ModelType ModelType,
    string ModelPath,
    DeviceProvider Device,
    int GpuIndex,
    float ScoreThreshold,
    float IouThreshold,
    float KeypointThreshold);

/// <summary>已加载模型——ONNX 会话 + 配置，供推理时按类型解析输出。</summary>
public sealed class LoadedModel(InferenceSession session, ModelConfig config) : IDisposable
{
    public InferenceSession Session { get; } = session;
    public ModelConfig Config { get; } = config;

    public void Dispose() => Session.Dispose();
}

/// <summary>注册表条目——模型配置 + 是否已加载到内存。</summary>
public sealed record ModelRegistryItem(ModelConfig Config, bool Loaded);

/// <summary>
/// 管理 ONNX 模型的生命周期：注册表、加载、缓存、卸载。线程安全。
/// 启动时从 aicondfig.json 加载模型注册表；客户端可按 model_id 引用，
/// 也可显式传入模型路径按需加载。设备（CPU/CUDA）与阈值均在此配置。
/// </summary>
public sealed class ModelManager : IDisposable
{
    private const string DefaultRegistryFile = "aicondfig.json";
    private const float DefaultScoreThreshold = 0.8f;
    private const float DefaultIouThreshold = 0.5f;
    private const float DefaultKeypointThreshold = 0.5f;

    private readonly Dictionary<string, ModelConfig> _registry = [];
    private readonly ConcurrentDictionary<string, LoadedModel> _loaded = new();

    public ModelManager()
        : this(Path.Combine(AppContext.BaseDirectory, DefaultRegistryFile)) { }

    /// <summary>从 aicondfig.json 加载模型注册表；文件缺失时注册表为空。</summary>
    public ModelManager(string registryPath) => LoadRegistry(registryPath);

    public ModelConfig? FindRegistryConfig(string modelId) =>
        _registry.GetValueOrDefault(modelId);

    public IReadOnlyList<ModelRegistryItem> ListModels() =>
        _registry.Values
            .Select(c => new ModelRegistryItem(c, _loaded.ContainsKey(c.ModelId)))
            .ToList();

    /// <summary>加载模型（幂等，已加载则跳过）。</summary>
    public Task LoadAsync(ModelConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ModelPath))
            throw new FileNotFoundException($"Model '{config.ModelId}' has no model path (not in registry).");

        if (_loaded.ContainsKey(config.ModelId)) return Task.CompletedTask;  // 幂等

        return Task.Run(() =>
        {
            if (!File.Exists(config.ModelPath))
                throw new FileNotFoundException($"Model not found: {config.ModelPath}");

            var options = CreateSessionOptions(config);
            var session = new InferenceSession(config.ModelPath, options);
            if (!_loaded.TryAdd(config.ModelId, new LoadedModel(session, config)))
                session.Dispose();  // 并发加载同一模型时丢弃多余会话
        }, ct);
    }

    public LoadedModel? Get(string modelId) =>
        _loaded.GetValueOrDefault(modelId);

    public Task UnloadAsync(string modelId) => Task.Run(() =>
    {
        if (_loaded.TryRemove(modelId, out var loaded))
            loaded.Dispose();
    });

    /// <summary>根据设备配置创建 ONNX SessionOptions。</summary>
    private static Microsoft.ML.OnnxRuntime.SessionOptions CreateSessionOptions(ModelConfig config)
    {
        var options = new Microsoft.ML.OnnxRuntime.SessionOptions();

        if (config.Device == DeviceProvider.Cuda)
        {
            try
            {
                // 字符串形式调用，编译期不依赖 Gpu 包；运行时需安装
                // Microsoft.ML.OnnxRuntime.Gpu 及对应的 CUDA/cuDNN 依赖。
                options.AppendExecutionProvider("CUDA",
                    new Dictionary<string, string> { ["device_id"] = config.GpuIndex.ToString() });
            }
            catch (Exception)
            {
                // CUDA provider 不可用时回退 CPU。
                options.AppendExecutionProvider_CPU();
            }
        }
        else
        {
            options.AppendExecutionProvider_CPU();
        }

        return options;
    }

    public void Dispose()
    {
        foreach (var m in _loaded.Values) m.Dispose();
        _loaded.Clear();
    }

    // ── 注册表反序列化 ─────────────────────────────────────────

    private void LoadRegistry(string registryPath)
    {
        if (!File.Exists(registryPath)) return;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
            };
            var file = JsonSerializer.Deserialize<RegistryFile>(File.ReadAllText(registryPath), options);
            foreach (var e in file?.Models ?? [])
            {
                if (string.IsNullOrWhiteSpace(e.ModelId)) continue;
                _registry[e.ModelId] = new ModelConfig(
                    e.ModelId,
                    ParseModelType(e.ModelType),
                    e.ModelPath,
                    ParseDevice(e.Device, e.GpuIndex),
                    e.GpuIndex,
                    e.ScoreThreshold > 0 ? e.ScoreThreshold : DefaultScoreThreshold,
                    e.IouThreshold > 0 ? e.IouThreshold : DefaultIouThreshold,
                    e.KeypointThreshold > 0 ? e.KeypointThreshold : DefaultKeypointThreshold);
            }
        }
        catch (JsonException)
        {
            // 注册表损坏时静默忽略，服务仍可运行（客户端可显式传路径加载）。
        }
    }

    private static ModelType ParseModelType(string? type) =>
        (type ?? "").Trim().ToLowerInvariant() switch
        {
            "maskrcnn" or "mask_rcnn" => ModelType.MaskRcnn,
            "yolo"                    => ModelType.Yolo,
            "keypoint" or "key_point" => ModelType.KeyPoint,
            _                          => ModelType.Unspecified,
        };

    private static DeviceProvider ParseDevice(string? device, int gpuIndex) =>
        (device ?? "cpu").Trim().ToLowerInvariant() == "cuda" && gpuIndex >= 0
            ? DeviceProvider.Cuda
            : DeviceProvider.Cpu;

    private sealed record RegistryEntry(
        string ModelId = "",
        string ModelType = "",
        string ModelPath = "",
        string Device = "cpu",
        int GpuIndex = 0,
        float ScoreThreshold = 0,
        float IouThreshold = 0,
        float KeypointThreshold = 0);

    private sealed record RegistryFile(RegistryEntry[] Models);
}
