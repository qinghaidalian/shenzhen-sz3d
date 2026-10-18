namespace Sz3D.Core.Infrastructure.Graph;

/// <summary>
/// 分组整体平移的纯几何逻辑——不依赖 WPF。
/// 给定一个坐标点与位移增量（delta），计算平移后的新坐标；
/// 并可把同一 delta 就地施加到一组成员节点的 <see cref="NodeModel.X"/>/<see cref="NodeModel.Y"/>。
/// 供分组块（GroupingNode）拖拽时带动内部成员节点一起移动使用。
/// </summary>
public static class GroupTranslation
{
    /// <summary>
    /// 对单个坐标点施加位移增量，返回平移后的新坐标。纯函数，不产生副作用。
    /// </summary>
    /// <param name="x">点的原始 X 坐标。</param>
    /// <param name="y">点的原始 Y 坐标。</param>
    /// <param name="dx">X 方向的位移增量。</param>
    /// <param name="dy">Y 方向的位移增量。</param>
    /// <returns>平移后的坐标 <c>(X, Y)</c>。</returns>
    public static (double X, double Y) Translate(double x, double y, double dx, double dy) =>
        (x + dx, y + dy);

    /// <summary>
    /// 把同一位移增量就地施加到每个成员节点，更新其 <see cref="NodeModel.X"/>/<see cref="NodeModel.Y"/>。
    /// 成员集为空时为无操作（不抛异常）。
    /// </summary>
    /// <param name="members">待平移的成员节点集合。</param>
    /// <param name="dx">X 方向的位移增量。</param>
    /// <param name="dy">Y 方向的位移增量。</param>
    public static void ApplyDelta(IEnumerable<NodeModel> members, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(members);
        foreach (var member in members)
            (member.X, member.Y) = Translate(member.X, member.Y, dx, dy);
    }
}
