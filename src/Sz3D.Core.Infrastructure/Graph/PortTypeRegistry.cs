namespace Sz3D.Core.Infrastructure.Graph;

/// <summary>
/// 端口类型兼容性注册表。
/// 规则：精确类型匹配 > 继承链检查 > 显式注册的隐式转换。
/// </summary>
public sealed class PortTypeRegistry
{
    private readonly HashSet<(Type From, Type To)> _explicit = [];

    /// <summary>注册显式兼容类型对，例如 Image → object。</summary>
    public void RegisterCompatibility(Type from, Type to) =>
        _explicit.Add((from, to));

    public bool AreCompatible(Type outputType, Type inputType)
    {
        if (outputType == inputType) return true;
        if (inputType.IsAssignableFrom(outputType)) return true;
        return _explicit.Contains((outputType, inputType));
    }
}
