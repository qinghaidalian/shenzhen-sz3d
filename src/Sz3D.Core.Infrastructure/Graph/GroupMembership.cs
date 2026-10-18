namespace Sz3D.Core.Infrastructure.Graph;

/// <summary>
/// 分组成员判定的纯几何逻辑——不依赖 WPF。
/// 判定全部基于分组矩形与节点左上角坐标的几何包含关系。
/// </summary>
public static class GroupMembership
{
    /// <summary>
    /// 判断给定点是否落在分组矩形内。
    /// 采用闭区间含边界：当且仅当 <paramref name="nodeX"/> ∈ [X, X+Width]
    /// 且 <paramref name="nodeY"/> ∈ [Y, Y+Height] 时返回 <c>true</c>（四条边与四个角视为命中）。
    /// </summary>
    /// <param name="group">目标分组。</param>
    /// <param name="nodeX">待判定点的画布 X 坐标。</param>
    /// <param name="nodeY">待判定点的画布 Y 坐标。</param>
    /// <returns>点落在矩形（含边界）内返回 <c>true</c>，否则 <c>false</c>。</returns>
    public static bool Contains(GroupModel group, double nodeX, double nodeY)
    {
        ArgumentNullException.ThrowIfNull(group);
        return nodeX >= group.X && nodeX <= group.X + group.Width
            && nodeY >= group.Y && nodeY <= group.Y + group.Height;
    }

    /// <summary>
    /// 从节点集合中筛出位置（左上角坐标）落在分组内的成员节点。
    /// </summary>
    /// <param name="group">目标分组。</param>
    /// <param name="nodes">候选节点集合。</param>
    /// <returns>落在分组矩形（含边界）内的节点列表。</returns>
    public static IReadOnlyList<NodeModel> MembersOf(GroupModel group, IEnumerable<NodeModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(nodes);
        return [.. nodes.Where(n => Contains(group, n.X, n.Y))];
    }

    /// <summary>
    /// 返回包含给定点的分组中面积（Width*Height）最小的那个。
    /// 判定规则：先用 <see cref="Contains"/> 过滤出所有包含该点的分组，
    /// 再取面积最小者返回；若无任何分组包含该点则返回 <c>null</c>。
    /// 当最内层分组嵌套时，面积最小者即为视觉上最靠内的分组。
    /// </summary>
    /// <param name="groups">候选分组集合。</param>
    /// <param name="nodeX">待判定点的画布 X 坐标。</param>
    /// <param name="nodeY">待判定点的画布 Y 坐标。</param>
    /// <returns>面积最小的包含分组，无则 <c>null</c>。</returns>
    public static GroupModel? GroupContaining(IEnumerable<GroupModel> groups, double nodeX, double nodeY)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups
            .Where(g => Contains(g, nodeX, nodeY))
            .OrderBy(g => g.Width * g.Height)
            .FirstOrDefault();
    }
}
