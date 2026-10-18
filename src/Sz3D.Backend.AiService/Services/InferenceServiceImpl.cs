using Google.Protobuf;
using Grpc.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Sz3D.Backend.AiService.Grpc;
using Store = Sz3D.Backend.AiService.ModelStore;

namespace Sz3D.Backend.AiService.Services;

/// <summary>
/// gRPC InferenceService 实现。
/// 接收图像字节，通过 ONNX Runtime 推理后返回结构化结果，
/// 按模型类型（MaskRCNN / YOLO / KeyPoint）解析输出。
/// </summary>
public sealed class InferenceServiceImpl(
    Store.ModelManager modelManager,
    ILogger<InferenceServiceImpl> logger)
    : InferenceService.InferenceServiceBase
{
    // ── LoadModel ────────────────────────────────────────────

    public override async Task<LoadModelResponse> LoadModel(
        LoadModelRequest request, ServerCallContext context)
    {
        logger.LogInformation("Loading model {ModelId}", request.ModelId);
        try
        {
            await modelManager.LoadAsync(BuildConfig(request), context.CancellationToken);
            return new LoadModelResponse { Success = true, ModelId = request.ModelId };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load model {ModelId}", request.ModelId);
            return new LoadModelResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── ListModels ───────────────────────────────────────────

    public override Task<ListModelsResponse> ListModels(
        ListModelsRequest request, ServerCallContext context)
    {
        var response = new ListModelsResponse();
        foreach (var item in modelManager.ListModels())
        {
            response.Models.Add(new ModelInfo
            {
                ModelId           = item.Config.ModelId,
                ModelType         = ToGrpcModelType(item.Config.ModelType),
                ModelPath         = item.Config.ModelPath,
                Device            = ToDeviceSpec(item.Config.Device, item.Config.GpuIndex),
                ScoreThreshold    = item.Config.ScoreThreshold,
                IouThreshold      = item.Config.IouThreshold,
                KeypointThreshold = item.Config.KeypointThreshold,
                Loaded            = item.Loaded,
            });
        }
        return Task.FromResult(response);
    }

    // ── Infer ────────────────────────────────────────────────

    public override Task<InferResponse> Infer(InferRequest request, ServerCallContext context)
    {
        var loaded = modelManager.Get(request.ModelId);
        if (loaded is null)
            return Task.FromResult(new InferResponse
            {
                Success = false,
                ErrorMessage = $"Model '{request.ModelId}' not loaded. Call LoadModel first."
            });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = RunInference(loaded, request);
            sw.Stop();
            response.InferenceMs = sw.ElapsedMilliseconds;
            response.Success = true;
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inference failed for model {ModelId}", request.ModelId);
            return Task.FromResult(new InferResponse { Success = false, ErrorMessage = ex.Message });
        }
    }

    // ── InferStream (双向流) ──────────────────────────────────

    public override async Task InferStream(
        IAsyncStreamReader<InferRequest>  requestStream,
        IServerStreamWriter<InferResponse> responseStream,
        ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(await Infer(request, context));
        }
    }

    // ── UnloadModel / HealthCheck ─────────────────────────────

    public override async Task<UnloadModelResponse> UnloadModel(
        UnloadModelRequest request, ServerCallContext context)
    {
        await modelManager.UnloadAsync(request.ModelId);
        return new UnloadModelResponse { Success = true };
    }

    public override Task<HealthCheckResponse> HealthCheck(
        HealthCheckRequest request, ServerCallContext context) =>
        Task.FromResult(new HealthCheckResponse
        {
            Healthy = true,
            Version = typeof(InferenceServiceImpl).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });

    // ── 配置合并：请求显式参数覆盖服务端注册表 ────────────────

    private Store.ModelConfig BuildConfig(LoadModelRequest request)
    {
        var reg = modelManager.FindRegistryConfig(request.ModelId);

        var device = request.Device is not null
            ? ToDeviceProvider(request.Device)
            : reg?.Device ?? Store.DeviceProvider.Cpu;

        var modelType = request.ModelType != ModelType.Unspecified
            ? ToModelType(request.ModelType)
            : reg?.ModelType ?? Store.ModelType.Unspecified;

        return new Store.ModelConfig(
            request.ModelId,
            modelType,
            string.IsNullOrWhiteSpace(request.ModelPath) ? reg?.ModelPath ?? "" : request.ModelPath,
            device,
            request.Device?.GpuIndex ?? reg?.GpuIndex ?? 0,
            request.ScoreThreshold > 0 ? request.ScoreThreshold : reg?.ScoreThreshold ?? 0.8f,
            request.IouThreshold > 0 ? request.IouThreshold : reg?.IouThreshold ?? 0.5f,
            request.KeypointThreshold > 0 ? request.KeypointThreshold : reg?.KeypointThreshold ?? 0.5f);
    }

    // ── 推理核心：按模型类型解析输出 ──────────────────────────

    private static InferResponse RunInference(Store.LoadedModel loaded, InferRequest request)
    {
        var session = loaded.Session;
        var config  = loaded.Config;

        // TODO(预处理): 接入 SixLabors.ImageSharp，将 request.ImageData 解码并
        // 缩放/归一化为模型输入张量。当前用占位张量保证端到端流程可跑通。
        var inputMeta = session.InputMetadata.First();
        var inputName = inputMeta.Key;
        var dims      = inputMeta.Value.Dimensions;
        var tensor    = new DenseTensor<float>(dims.Select(d => d < 0 ? 1 : d).ToArray());
        var inputs    = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var results = session.Run(inputs);
        var response = new InferResponse();

        switch (config.ModelType)
        {
            case Store.ModelType.Yolo:     ParseYolo(results, response, config.ScoreThreshold); break;
            case Store.ModelType.MaskRcnn: ParseMaskRcnn(results, response, config.ScoreThreshold); break;
            case Store.ModelType.KeyPoint: ParseKeyPoint(results, response, config.ScoreThreshold, config.KeypointThreshold); break;
            default:                       ParseGeneric(results, response); break;
        }

        return response;
    }

    // ── 输出解析（best-effort，适配常见 ONNX 导出约定）────────

    /// <summary>通用解析：取所有浮点输出作为得分（向后兼容）。</summary>
    private static void ParseGeneric(IEnumerable<DisposableNamedOnnxValue> results, InferResponse resp)
    {
        foreach (var result in results)
        {
            if (result.Value is DenseTensor<float> scores)
                resp.Scores.AddRange(scores.ToArray());
        }
    }

    /// <summary>YOLO：单张 [N, D] 输出，D≥6 时按 [cx,cy,w,h,obj,cls...] 解析。</summary>
    private static void ParseYolo(IEnumerable<DisposableNamedOnnxValue> results, InferResponse resp, float threshold)
    {
        foreach (var result in results)
        {
            if (result.Value is not DenseTensor<float> t || t.Rank != 2) continue;

            int n = t.Dimensions[0], d = t.Dimensions[1];
            if (d < 6) continue;

            var flat = t.ToArray();
            for (int i = 0; i < n; i++)
            {
                int row = i * d;
                float cx = flat[row], cy = flat[row + 1], w = flat[row + 2], h = flat[row + 3];
                float obj = flat[row + 4];

                float clsConf = 0; int cls = 0;
                for (int c = 5; c < d; c++)
                    if (flat[row + c] > clsConf) { clsConf = flat[row + c]; cls = c - 5; }

                float score = obj * clsConf;
                if (score < threshold) continue;

                resp.Detections.Add(new BoundingBox
                {
                    X = cx - w / 2, Y = cy - h / 2, Width = w, Height = h,
                    Score = score, Label = cls.ToString(),
                });
                resp.Scores.Add(score);
                resp.Labels.Add(cls.ToString());
            }
        }
    }

    /// <summary>MaskRCNN：按输出名解析 boxes/labels/scores/masks。</summary>
    private static void ParseMaskRcnn(IEnumerable<DisposableNamedOnnxValue> results, InferResponse resp, float threshold)
    {
        var boxes  = FindFloat(results, "box");
        var scores = FindFloat(results, "score");
        var masks  = FindFloat(results, "mask");
        var labels = results.FirstOrDefault(x => x.Name.Contains("label", StringComparison.OrdinalIgnoreCase))?.Value;

        if (boxes is null || boxes.Rank != 2) return;

        var boxesFlat = boxes.ToArray();
        for (int i = 0; i < boxes.Dimensions[0]; i++)
        {
            int row = i * 4;
            float score = scores is not null && scores.Length > i ? scores.ToArray()[i] : 1f;
            if (score < threshold) continue;

            resp.Detections.Add(new BoundingBox
            {
                X = boxesFlat[row], Y = boxesFlat[row + 1],
                Width  = boxesFlat[row + 2] - boxesFlat[row],
                Height = boxesFlat[row + 3] - boxesFlat[row + 1],
                Score  = score,
                Label  = LabelToString(labels, i),
            });
            resp.Scores.Add(score);
            resp.Labels.Add(resp.Detections[^1].Label);
        }

        // 掩码 [N, H, W] 浮点 → 0.5 阈值二值化
        if (masks is not null && masks.Rank == 3)
        {
            int n = masks.Dimensions[0], h = masks.Dimensions[1], w = masks.Dimensions[2];
            var maskFlat = masks.ToArray();
            for (int i = 0; i < n; i++)
            {
                var data = new byte[w * h];
                int plane = i * h * w;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        data[y * w + x] = maskFlat[plane + y * w + x] >= 0.5f ? (byte)1 : (byte)0;
                resp.Masks.Add(new Mask { Width = w, Height = h, Data = ByteString.CopyFrom(data) });
            }
        }
    }

    /// <summary>KeyPoint：解析 keypoints 输出 [N, K, 3]（x, y, score）。</summary>
    private static void ParseKeyPoint(
        IEnumerable<DisposableNamedOnnxValue> results, InferResponse resp,
        float threshold, float keypointThreshold)
    {
        var keypoints = FindFloat(results, "keypoint");
        if (keypoints is not null)
        {
            var flat = keypoints.ToArray();
            if (keypoints.Rank == 3)  // [N, K, 3]
            {
                int n = keypoints.Dimensions[0], k = keypoints.Dimensions[1], c = keypoints.Dimensions[2];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < k; j++)
                    {
                        int idx = (i * k + j) * c;
                        float score = c >= 3 ? flat[idx + 2] : 1f;
                        if (score < keypointThreshold) continue;
                        resp.Keypoints.Add(new KeyPoint
                        {
                            X = flat[idx], Y = flat[idx + 1], Score = score, Label = j,
                        });
                    }
            }
            else if (keypoints.Rank == 2)  // [K, 3]
            {
                int k = keypoints.Dimensions[0], c = keypoints.Dimensions[1];
                for (int j = 0; j < k; j++)
                {
                    float score = c >= 3 ? flat[j * c + 2] : 1f;
                    if (score < keypointThreshold) continue;
                    resp.Keypoints.Add(new KeyPoint
                    {
                        X = flat[j * c], Y = flat[j * c + 1], Score = score, Label = j,
                    });
                }
            }
        }

        // 检测框/掩码复用 MaskRCNN 解析。
        ParseMaskRcnn(results, resp, threshold);
    }

    private static DenseTensor<float>? FindFloat(
        IEnumerable<DisposableNamedOnnxValue> results, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            var r = results.FirstOrDefault(x => x.Name.Contains(kw, StringComparison.OrdinalIgnoreCase));
            if (r?.Value is DenseTensor<float> t) return t;
        }
        return null;
    }

    private static string LabelToString(object? labels, int i)
    {
        if (labels is DenseTensor<long> l)
        {
            var arr = l.ToArray();
            return arr.Length > i ? arr[i].ToString() : i.ToString();
        }
        if (labels is DenseTensor<int> n)
        {
            var arr = n.ToArray();
            return arr.Length > i ? arr[i].ToString() : i.ToString();
        }
        return i.ToString();
    }

    // ── gRPC ↔ ModelStore 类型映射 ────────────────────────────

    private static Store.ModelType ToModelType(ModelType t) => t switch
    {
        ModelType.MaskRcnn => Store.ModelType.MaskRcnn,
        ModelType.Yolo     => Store.ModelType.Yolo,
        ModelType.KeyPoint => Store.ModelType.KeyPoint,
        _                   => Store.ModelType.Unspecified,
    };

    private static ModelType ToGrpcModelType(Store.ModelType t) => t switch
    {
        Store.ModelType.MaskRcnn => ModelType.MaskRcnn,
        Store.ModelType.Yolo     => ModelType.Yolo,
        Store.ModelType.KeyPoint => ModelType.KeyPoint,
        _                         => ModelType.Unspecified,
    };

    private static Store.DeviceProvider ToDeviceProvider(DeviceSpec d) =>
        (d.Provider ?? "").Trim().ToLowerInvariant() == "cuda"
            ? Store.DeviceProvider.Cuda
            : Store.DeviceProvider.Cpu;

    private static DeviceSpec ToDeviceSpec(Store.DeviceProvider device, int gpuIndex) => new()
    {
        Provider = device == Store.DeviceProvider.Cuda ? "cuda" : "cpu",
        GpuIndex = gpuIndex,
    };
}
