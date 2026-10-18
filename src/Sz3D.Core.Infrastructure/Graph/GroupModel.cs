namespace Sz3D.Core.Infrastructure.Graph;

/// <summary>
/// 画布上的分组矩形框——纯 C# 数据结构，不依赖 WPF。
/// 以左上角画布坐标 <see cref="X"/>/<see cref="Y"/> 加 <see cref="Width"/>/<see cref="Height"/>
/// 描述一块矩形区域，落在该区域内的节点视为该分组的成员。
/// </summary>
public sealed class GroupModel
{
    /// <summary>分组唯一标识符。</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>分组标题，显示在 header 上。</summary>
    public string Title { get; set; } = "Group";

    /// <summary>矩形左上角的画布 X 坐标。</summary>
    public double X { get; set; }

    /// <summary>矩形左上角的画布 Y 坐标。</summary>
    public double Y { get; set; }

    /// <summary>矩形宽度。</summary>
    public double Width { get; set; }

    /// <summary>矩形高度。</summary>
    public double Height { get; set; }

    /// <summary>header 颜色（十六进制字符串）。</summary>
    public string Color { get; set; } = "#0E639C";
}
