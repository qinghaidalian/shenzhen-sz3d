using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Core.Infrastructure;

/// <summary>
/// 所有节点的抽象基类。子类通过 [NodeType] 特性声明元数据，
/// 覆写 ExecuteAsync 实现处理逻辑。
/// </summary>
public abstract class NodeBase
{
    /// <summary>节点执行模式，Thread 节点覆写返回 Parallel。</summary>
    public virtual NodeExecutionMode ExecutionMode => NodeExecutionMode.Sequential;

    /// <summary>输入端口定义列表。</summary>
    public abstract IReadOnlyList<PortDescriptor> InputPorts  { get; }

    /// <summary>输出端口定义列表。</summary>
    public abstract IReadOnlyList<PortDescriptor> OutputPorts { get; }

    /// <summary>主执行入口——引擎调度时调用。</summary>
    public abstract Task ExecuteAsync(
        INodeExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>参数验证（可选覆写），在执行前由引擎调用。</summary>
    public virtual ValidationResult Validate() => ValidationResult.Success;
}

/// <summary>参数校验结果。</summary>
public sealed record ValidationResult(bool IsValid, string Message = "")
{
    public static readonly ValidationResult Success = new(true);
    public static ValidationResult Fail(string message) => new(false, message);
}

/// <summary>Phase 2 占位基类——执行时仅输出警告，不执行任何逻辑。</summary>
public abstract class Phase2PlaceholderNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts  => [];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [];

    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        var attr = GetType().GetCustomAttributes(typeof(NodeTypeAttribute), false)
                            .OfType<NodeTypeAttribute>()
                            .FirstOrDefault();
        ctx.LogWarning($"{attr?.Name ?? GetType().Name} 是 Phase 2 功能，当前版本暂不可用。");
        return Task.CompletedTask;
    }
}
