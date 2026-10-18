namespace Sz3D.Core.Infrastructure.Contracts;

/// <summary>节点执行模式：顺序（主线程）或并行（Thread 节点）。</summary>
public enum NodeExecutionMode { Sequential, Parallel }

/// <summary>节点元数据特性，标注节点类别、名称与描述。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NodeTypeAttribute : Attribute
{
    public string Category { get; set; } = "Custom";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    /// <summary>Phase 2 功能标记，NodeLibrary 中以禁用样式显示。</summary>
    public bool IsPhase2 { get; set; }
}

/// <summary>端口方向。</summary>
public enum PortDirection { Input, Output }

/// <summary>描述节点的一个数据端口。</summary>
public sealed record PortDescriptor(
    string Name,
    Type DataType,
    PortDirection Direction,
    bool IsList = false);

/// <summary>节点类型描述符，用于注册到 NodeLibrary。</summary>
public sealed record NodeTypeDescriptor(
    string TypeId,          // "{PluginId}:{TypeName}"
    string Category,
    string Name,
    string Description,
    string Version,
    bool IsPhase2,
    Func<NodeBase> Factory);

/// <summary>插件包接口——每个 NodePlugin DLL 实现此接口。</summary>
public interface INodePlugin
{
    string PluginId   { get; }
    string PluginName { get; }
    string Version    { get; }
    IReadOnlyList<NodeTypeDescriptor> NodeTypes { get; }
}

/// <summary>标记节点属性面板可编辑的参数字段。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NodeParameterAttribute : Attribute { }
