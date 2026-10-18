using System.IO;
using System.Reflection;
using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Presentation.WPF.Services;

/// <summary>算子目录中的一项（来自插件 [NodeType] 元数据）。</summary>
public sealed record OperatorDescriptor(
    string TypeId,
    string Category,
    string Name,
    string Description,
    bool IsPhase2,
    Func<NodeBase> Factory);

/// <summary>算子分组（如「图像处理 2D」「图像处理 3D」）。</summary>
public sealed record OperatorGroup(string Title, IReadOnlyList<OperatorDescriptor> Items);

/// <summary>
/// 算子库——通过反射扫描插件程序集中的 [NodeType] 节点，供右侧「算子列表」展示与拖拽建节点。
/// 与 Core.Infrastructure.Plugins.PluginLoader 的 plugins/ 目录隔离加载对齐；首版从输出目录加载插件 DLL。
/// </summary>
public static class NodeLibrary
{
    private static IReadOnlyList<OperatorDescriptor>? _operators;
    private static IReadOnlyList<OperatorGroup>? _groups;

    public static IReadOnlyList<OperatorDescriptor> Operators => _operators ??= ScanPlugins();

    public static IReadOnlyList<OperatorGroup> Groups => _groups ??= BuildGroups();

    private static IReadOnlyList<OperatorGroup> BuildGroups() =>
        Operators
            .GroupBy(o => o.Category)
            .OrderBy(g => g.Key)
            .Select(g => new OperatorGroup(g.Key, g.OrderBy(o => o.Name).ToList()))
            .ToList();

    private static IReadOnlyList<OperatorDescriptor> ScanPlugins()
    {
        // 1) 从输出目录加载插件 DLL，使反射可发现 NodeBase 子类
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "Sz3D.Plugins.*.dll"))
        {
            try { Assembly.LoadFrom(dll); }
            catch { /* 插件加载失败不影响主界面 */ }
        }

        // 2) 扫描所有已加载程序集中带 [NodeType] 的 NodeBase 子类
        var result = new List<OperatorDescriptor>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.OfType<Type>().ToArray(); }
            catch { continue; }

            foreach (var type in types)
            {
                if (type.IsAbstract || !typeof(NodeBase).IsAssignableFrom(type)) continue;
                var attr = type.GetCustomAttribute<NodeTypeAttribute>();
                if (attr is null) continue;

                result.Add(new OperatorDescriptor(
                    TypeId:      $"{asm.GetName().Name}:{type.Name}",
                    Category:    attr.Category,
                    Name:        attr.Name,
                    Description: attr.Description,
                    IsPhase2:    attr.IsPhase2,
                    Factory:     () => (NodeBase)Activator.CreateInstance(type)!));
            }
        }

        return result;
    }
}
