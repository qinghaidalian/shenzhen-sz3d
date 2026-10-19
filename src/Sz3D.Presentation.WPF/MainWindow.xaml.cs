using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;
using AvalonDock.Core.Serialization;
using AvalonDock.Core.Serialization.Dto;
using AvalonDock.Layout;
using AvalonDock.Serialization;
using Sz3D.Core.Infrastructure.Execution;
using Sz3D.Presentation.WPF.Services;
using Sz3D.Presentation.WPF.ViewModels;

namespace Sz3D.Presentation.WPF;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point _dragStart;

    // ── 布局持久化：ContentId 常量（供 XAML ContentId 与回调共用，绝不本地化） ──
    private const string IdView3D = "panel.view3d";
    private const string IdView2D = "panel.view2d";
    private const string IdGraph = "panel.graph";
    private const string IdToolbox = "panel.toolbox";
    private const string IdLog = "panel.log";

    // 必需面板集合（§5：五块全部必需，任一缺失即整体回退默认布局）
    private static readonly string[] RequiredIds = { IdView3D, IdView2D, IdGraph, IdToolbox, IdLog };

    // ── 布局文件路径（基路径统一用 AppContext.BaseDirectory，与 CrashLogger 一致） ──
    private const string LayoutConfigDir = "config";
    private const string LayoutFileName = "layout.xml";
    private static string LayoutPath =>
        Path.Combine(AppContext.BaseDirectory, LayoutConfigDir, LayoutFileName);

    // ── 五块 live 内容缓存（反序列化后据此重新挂载） ──
    private object? _view3dHost;
    private object? _view2dHost;
    private object? _graphHost;
    private object? _toolboxHost;
    private object? _logHost;

    // 默认布局 XML 缓存（损坏/缺失/中途失败时的回退单一真源）
    private string? _defaultLayoutXml;

    // DTO ↔ XML 的序列化器（Dirkster.AvalonDock 5.0.1 的实际公开序列化面：
    // DockingManager.Layout 是 ISerializableLayoutRoot；LayoutDtoMapper 在
    // ISerializableLayoutRoot 与 [XmlRoot] 标注的 LayoutRootDto 间互转，
    // 由 System.Xml.Serialization.XmlSerializer 负责 DTO↔XML 文本。
    // 注：5.0.1 不再提供经典的 XmlLayoutSerializer / LayoutSerializationCallback，
    // 故内容重挂采用「反序列化后按 ContentId 走查、把缓存 live 内容写回 .Content」。）
    private static readonly XmlSerializer DtoSerializer = new(typeof(LayoutRootDto));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeEditorViewModel.CloudOutput))
                UpdateCloudVisual();
        };

        // 恢复：默认布局已 inflate、live 内容已建好之后（Loaded）
        Loaded += (_, _) => LoadLayout();
        // 保存：此刻 DockingManager 仍存活、布局树完整可序列化（Closing）
        Closing += (_, _) => SaveLayout();
    }

    private void UpdateCloudVisual()
    {
        var cloud = _viewModel.Editor.CloudOutput;
        CloudVisual.Content = cloud is not null && cloud.Count > 0
            ? PointCloudRenderer.BuildModel(cloud)
            : null;
    }

    // ── 算子列表 → 画布拖拽 ─────────────────────────────

    private void OnOperatorMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
    }

    private void OnOperatorMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is FrameworkElement el && el.DataContext is OperatorDescriptor op)
            DragDrop.DoDragDrop(el, new DataObject(typeof(OperatorDescriptor), op), DragDropEffects.Copy);
    }

    private void OnDropOperator(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(OperatorDescriptor)) is OperatorDescriptor op)
        {
            var position = Editor.GetLocationInsideEditor(e);
            _viewModel.Editor.AddOperator(op, position);
        }
    }

    // ── 布局持久化 ─────────────────────────────

    /// <summary>
    /// 把当前 DockingManager 布局序列化到 <c>./config/layout.xml</c>。
    /// 失败仅记日志，绝不阻断窗口关闭（非致命）。
    /// </summary>
    private void SaveLayout()
    {
        try
        {
            var dir = Path.GetDirectoryName(LayoutPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(LayoutPath, SerializeLayout(DockManager.Layout), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // 布局落盘失败非致命：记日志，窗口照常关闭
            CrashLogger.Write(ex);
        }
    }

    /// <summary>
    /// 在 Loaded 时恢复磁盘布局。步骤顺序钉死，详见设计 §4.4：
    /// ①抓取五块 live 缓存（必须在任何反序列化之前）→ ①a 缓存完整性断言
    /// → ②生成默认布局 XML 缓存 → ③无磁盘文件则结束
    /// → ④反序列化磁盘布局并重挂内容 → ⑤必需面板校验。整体包 try/catch，绝不崩溃。
    /// </summary>
    private void LoadLayout()
    {
        // ① 抓取五块 live 缓存（Loaded 时默认 XAML 布局已 inflate、五块 .Content 已建好）
        foreach (var lc in EnumerateContents(DockManager.Layout))
        {
            switch (lc.ContentId)
            {
                case IdView3D: _view3dHost = lc.Content; break;
                case IdView2D: _view2dHost = lc.Content; break;
                case IdGraph: _graphHost = lc.Content; break;
                case IdToolbox: _toolboxHost = lc.Content; break;
                case IdLog: _logHost = lc.Content; break;
            }
        }

        // ①a 缓存完整性断言：任一为 null 属实现期 bug，放弃持久化、保留默认布局
        if (_view3dHost is null || _view2dHost is null || _graphHost is null || _toolboxHost is null || _logHost is null)
        {
            CrashLogger.Write(new InvalidOperationException(
                "布局缓存抓取不完整（视图3D/视图2D/算子图/工具箱/日志 五块应齐备）：" +
                "默认 XAML 布局未按预期 inflate 或 ContentId 不匹配，" +
                "保留默认布局、不进行反序列化。"));
            return;
        }

        try
        {
            // ② 生成默认布局 XML 缓存（供损坏/中途失败时回退）
            _defaultLayoutXml = SerializeLayout(DockManager.Layout);

            // ③ 无磁盘文件则结束，保留 XAML 默认布局
            if (!File.Exists(LayoutPath))
                return;

            // ④ 反序列化磁盘布局并重挂 live 内容（以结果整体替换 DockingManager.Layout）
            ApplyLayoutXml(File.ReadAllText(LayoutPath, Encoding.UTF8));

            // ⑤ 必需面板校验：任一缺失即整体回退默认布局
            if (!AllRequiredPresent())
            {
                CrashLogger.Write(new InvalidOperationException(
                    "恢复后必需面板缺失（视图3D/视图2D/算子图/工具箱/日志），回退默认布局。"));
                ResetToDefaultLayout();
            }
        }
        catch (Exception ex)
        {
            // 反序列化中途失败：记日志并回退默认布局，绝不崩溃、不弹窗
            CrashLogger.Write(ex);
            ResetToDefaultLayout();
        }
    }

    /// <summary>
    /// 把布局树序列化为 XML 文本：<see cref="LayoutDtoMapper"/> 转 DTO，再由
    /// <see cref="XmlSerializer"/> 写出。只序列化结构 / <c>ContentId</c>，不含 live 内容。
    /// </summary>
    private static string SerializeLayout(LayoutRoot layout)
    {
        var dto = new LayoutDtoMapper().ToDto(layout);
        using var sw = new StringWriter();
        DtoSerializer.Serialize(sw, dto);
        return sw.ToString();
    }

    /// <summary>
    /// 从 XML 文本重建布局并整体替换 <c>DockingManager.Layout</c>，随后按 <c>ContentId</c>
    /// 把缓存的 live 内容重新挂回。未知/过时 <c>ContentId</c> 的面板保持空内容（不崩溃）。
    /// </summary>
    private void ApplyLayoutXml(string xml)
    {
        LayoutRootDto dto;
        using (var sr = new StringReader(xml))
            dto = (LayoutRootDto)DtoSerializer.Deserialize(sr)!;

        // FromDto 产出的具体类型即 AvalonDock.Layout.LayoutRoot，可直接赋给 Layout
        var newRoot = (LayoutRoot)new LayoutDtoMapper().FromDto(dto);

        // 先重挂内容再替换 Layout：按 ContentId 把缓存 live 内容写回对应面板
        ReattachContent(newRoot);

        DockManager.Layout = newRoot;
    }

    /// <summary>
    /// 走查新布局树，按 <c>ContentId</c> 把缓存的 live 内容写回每个面板的 <c>Content</c>。
    /// 等价于经典 AvalonDock 的 <c>LayoutSerializationCallback</c>：XML 只决定摆放结构，
    /// 真实 UI 子树只在 XAML 里写一遍、反序列化后按 Id 重新挂载。
    /// 未知/过时 <c>ContentId</c>（白名单外）保持空内容。
    /// </summary>
    private void ReattachContent(LayoutRoot root)
    {
        foreach (var lc in EnumerateContents(root))
        {
            var host = lc.ContentId switch
            {
                IdView3D => _view3dHost,
                IdView2D => _view2dHost,
                IdGraph => _graphHost,
                IdToolbox => _toolboxHost,
                IdLog => _logHost,
                _ => null,
            };
            if (host is null) continue;

            DetachFromCurrentParent(host, root);
            lc.Content = host;
        }
    }

    /// <summary>
    /// 把缓存内容元素从它在<paramref name="root"/>中当前残留的面板父上摘下，
    /// 避免把同一个 UIElement 二次挂到另一个父时抛「元素已是另一元素的子级」异常。
    /// </summary>
    private static void DetachFromCurrentParent(object host, LayoutRoot root)
    {
        foreach (var lc in EnumerateContents(root))
        {
            if (ReferenceEquals(lc.Content, host))
                lc.Content = null;
        }
    }

    /// <summary>
    /// 当 XML 损坏 / 反序列化中途失败 / 必需面板缺失时的回退。
    /// 单一真源为内存缓存的默认布局 XML（<see cref="_defaultLayoutXml"/>）。
    /// 步骤顺序钉死（防止在半成品布局上叠加导致重挂载冲突）：
    /// ①整体重置 Layout 解绑所有缓存内容 → ②从默认 XML 回放 → ③回放后校验五面板齐备。
    /// 全程 try/catch，连默认布局都失败时保留空布局并记 error（不崩溃、不递归回退）。
    /// </summary>
    private void ResetToDefaultLayout()
    {
        if (_defaultLayoutXml is null)
        {
            // 极端：默认缓存未就绪（①a 已拦截多数情况），无从回放，保留现状
            CrashLogger.Write(new InvalidOperationException("默认布局缓存未就绪，无法回退。"));
            return;
        }

        try
        {
            // ① 整体重置：使所有缓存内容元素从任何残留父节点解绑。
            //    Theme 设在 DockingManager 上、不是 Layout 子树属性，替换 Layout 不影响主题。
            DockManager.Layout = new LayoutRoot();

            // 显式摘除五块 live 内容残留的逻辑父，消除跨树残留父冲突：
            // 旧树（含上一次失败回退的 newRoot）卸下后，WPF 元素可能仍挂在已游离的
            // 内容宿主上；重挂到默认布局前先置空其父 ContentControl.Content，
            // 避免触发「元素已是另一元素的子级」异常。
            foreach (var host in new[] { _view3dHost, _view2dHost, _graphHost, _toolboxHost, _logHost })
            {
                if (host is FrameworkElement fe && fe.Parent is ContentControl cc)
                    cc.Content = null;
            }

            // ② 从默认 XML 回放，并重挂内容（整体替换 DockingManager.Layout）
            ApplyLayoutXml(_defaultLayoutXml);

            // ③ 回放后校验：五面板齐备才算回退成功，否则保留空布局并记 error
            if (!AllRequiredPresent())
            {
                CrashLogger.Write(new InvalidOperationException(
                    "回退默认布局后仍缺失必需面板（五块），保留空布局（不崩溃）。"));
            }
        }
        catch (Exception ex)
        {
            // 连默认布局都反序列化失败：记 error，保留空布局、不递归回退
            CrashLogger.Write(ex);
        }
    }

    /// <summary>
    /// 遍历布局树中所有承载内容的面板（<see cref="ISerializableLayoutContent"/>，
    /// 即 LayoutAnchorable / LayoutDocument），覆盖主停靠树、浮动窗口、被隐藏(自动隐藏)的面板。
    /// 基于 AvalonDock 5.x 的 <c>Descendents()</c>（<see cref="LayoutRoot"/> 实现
    /// <see cref="ISerializableLayoutRoot"/>）。由抓缓存①、<see cref="CollectPresentIds"/>、
    /// <see cref="ReattachContent"/>、<see cref="DetachFromCurrentParent"/> 共用。
    /// </summary>
    private static IEnumerable<ISerializableLayoutContent> EnumerateContents(LayoutRoot? root)
    {
        if (root is null) yield break;

        foreach (var e in root.Descendents())
            if (e is ISerializableLayoutContent lc)
                yield return lc;
    }

    /// <summary>
    /// 枚举当前布局树中已挂载的全部非空 <c>ContentId</c>。
    /// </summary>
    private HashSet<string> CollectPresentIds() =>
        EnumerateContents(DockManager.Layout)
            .Select(c => c.ContentId)
            .Where(id => id is not null)
            .ToHashSet()!;

    /// <summary>
    /// 必需面板（五块全部）是否都已挂载。
    /// </summary>
    private bool AllRequiredPresent()
    {
        var present = CollectPresentIds();
        return RequiredIds.All(present.Contains);
    }
}
