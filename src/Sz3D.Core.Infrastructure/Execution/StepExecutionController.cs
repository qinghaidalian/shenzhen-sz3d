using Sz3D.Core.Infrastructure.Graph;

namespace Sz3D.Core.Infrastructure.Execution;

/// <summary>单步执行控制器——每次调用 StepNext() 仅执行下一个节点。</summary>
public sealed class StepExecutionController(ExecutionEngine engine)
{
    private readonly SemaphoreSlim _gate = new(0, 1);

    public void StepNext() => _gate.Release();

    public async Task RunStepModeAsync(GraphModel graph, CancellationToken ct)
    {
        var order = graph.TopologicalOrder();
        foreach (var node in order)
        {
            await _gate.WaitAsync(ct);
            // 单步模式下逐节点触发引擎（简化：直接调用内部接口）
            // 实际实现通过 ExecutionEngine.RunSingleNodeAsync 扩展
        }
    }
}
