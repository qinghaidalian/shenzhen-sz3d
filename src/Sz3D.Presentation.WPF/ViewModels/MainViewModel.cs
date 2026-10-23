using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Sz3D.Core.Infrastructure.Execution;
using Sz3D.Presentation.WPF.Commands;
using Sz3D.Presentation.WPF.Services;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>日志视图中的一行。</summary>
public sealed record LogEntry(string Timestamp, string Channel, string Level, string Message);

/// <summary>
/// 「算子库」下拉的一项：<paramref name="Display"/> 为显示文本，
/// <paramref name="Category"/> 为对应算子种类；<c>Category == null</c> 表示「全部算子」（不按种类过滤）。
/// </summary>
/// <param name="Display">下拉中显示的文本。</param>
/// <param name="Category">对应的算子种类；<c>null</c> 代表「全部算子」哨兵。</param>
public sealed record CategoryOption(string Display, string? Category);

/// <summary>主窗口 DataContext。</summary>
public sealed class MainViewModel : ViewModelBase
{
    public NodeEditorViewModel Editor { get; } = new();

    /// <summary>经「关键字 + 种类」过滤后的算子分组，供「算子列表」面板绑定展示。</summary>
    public ObservableCollection<OperatorGroup> FilteredGroups { get; } = [];

    /// <summary>「算子库」下拉可选项：首项为「全部算子」，其后为各算子种类。</summary>
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];

    private string _searchKeyword = "";

    /// <summary>查找框关键字；变化时实时重算过滤结果（不区分大小写子串匹配 Name/Description）。</summary>
    public string SearchKeyword
    {
        get => _searchKeyword;
        set { if (Set(ref _searchKeyword, value)) RebuildFilteredGroups(); }
    }

    private CategoryOption? _selectedCategory;

    /// <summary>当前选中的算子种类；变化时重算过滤结果。「全部算子」时不按种类过滤。</summary>
    public CategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set { if (Set(ref _selectedCategory, value)) RebuildFilteredGroups(); }
    }

    public ObservableCollection<LogEntry> Logs { get; } =
    [
        new("2026-10-17 14:44:23.646028", "rvs_kernel", "info", "--Plugin 信息"),
        new("2026-10-17 14:44:23.646034", "rvs_kernel", "info", "名称=rvs_algorithms，版本=1.0.0，算子数量=6"),
        new("2026-10-17 14:44:23.646041", "rvs_kernel", "info", "名称=rvs_cameras，版本=1.0.0，算子数量=1"),
    ];

    private string _statusText = "已停止";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    private readonly IFileDialogService _dialogs;

    /// <summary>当前已保存项目的完整路径；尚未保存过（新建）时为 <c>null</c>。</summary>
    private string? _currentFilePath;

    public ICommand NewCommand     { get; }
    public ICommand LoadCommand    { get; }
    public ICommand SaveCommand    { get; }
    public ICommand SaveAsCommand  { get; }
    public ICommand RunCommand     { get; }
    public ICommand StopCommand    { get; }
    public ICommand RunOnceCommand { get; }
    public ICommand ExitCommand    { get; }

    public MainViewModel() : this(new FileDialogService()) { }

    /// <summary>可注入对话框服务的构造函数（便于测试）。</summary>
    public MainViewModel(IFileDialogService dialogs)
    {
        _dialogs = dialogs;

        NewCommand     = new RelayCommand<object>(_ => NewProject());
        LoadCommand    = new RelayCommand<object>(_ => LoadProject());
        SaveCommand    = new RelayCommand<object>(_ => SaveProject());
        SaveAsCommand  = new RelayCommand<object>(_ => SaveProjectAs());
        RunCommand     = new RelayCommand<object>(async _ => await RunGraphAsync());
        StopCommand    = new RelayCommand<object>(_ => StatusText = "已停止");
        RunOnceCommand = new RelayCommand<object>(async _ => await RunGraphAsync());
        ExitCommand    = new RelayCommand<object>(_ => System.Windows.Application.Current?.Shutdown());

        // 「算子库」选项：首项「全部算子」(Category=null)，其后按现有分组顺序取各种类
        CategoryOptions.Add(new CategoryOption("全部算子", null));
        foreach (var group in NodeLibrary.Groups)
            CategoryOptions.Add(new CategoryOption(group.Title, group.Title));

        _selectedCategory = CategoryOptions[0];
        RebuildFilteredGroups();
    }

    /// <summary>
    /// 依据当前 <see cref="SelectedCategory"/> 与 <see cref="SearchKeyword"/> 重算 <see cref="FilteredGroups"/>。
    /// 先按种类筛组（「全部算子」不筛），再对关键字做不区分大小写子串匹配（命中 Name 或 Description，
    /// 空关键字全通过），仅保留非空组；重建的 <see cref="OperatorGroup"/> 复用原 <see cref="OperatorDescriptor"/> 实例。
    /// 结果写回时先 <c>Clear()</c> 再逐项 <c>Add</c>，不替换集合引用以保持绑定。
    /// </summary>
    private void RebuildFilteredGroups()
    {
        var category = SelectedCategory?.Category;
        var keyword = SearchKeyword;

        var filtered = NodeLibrary.Groups
            .Where(g => category is null || g.Title == category)
            .Select(g => new OperatorGroup(
                g.Title,
                g.Items.Where(o =>
                    keyword.Length == 0 ||
                    o.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    o.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList()))
            .Where(g => g.Items.Count > 0);

        FilteredGroups.Clear();
        foreach (var group in filtered)
            FilteredGroups.Add(group);
    }

    // ── 项目：新建 / 保存 / 另存为 / 加载 ─────────────────────────

    /// <summary>新建空白项目：清空画布并复位当前文件路径。</summary>
    private void NewProject()
    {
        Editor.Clear();
        _currentFilePath = null;
        StatusText = "新建项目";
    }

    /// <summary>
    /// 保存项目：已有当前路径则直接写回该文件；否则转「另存为」。
    /// </summary>
    private void SaveProject()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveProjectAs();
            return;
        }
        WriteProject(_currentFilePath);
    }

    /// <summary>另存为：弹出对话框选路径后写入，并记为当前文件。</summary>
    private void SaveProjectAs()
    {
        var suggested = string.IsNullOrEmpty(_currentFilePath)
            ? "new" + ProjectSerializer.Extension
            : Path.GetFileName(_currentFilePath);

        var path = _dialogs.SaveProjectFileAs(suggested);
        if (string.IsNullOrEmpty(path)) return;   // 用户取消

        WriteProject(path);
    }

    /// <summary>把当前画布写入指定路径，并更新当前文件路径与状态。</summary>
    private void WriteProject(string path)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var project = Editor.ToProjectFile(name);
            ProjectSerializer.Save(project, path);
            _currentFilePath = path;
            StatusText = $"已保存：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            CrashLogger.Write(ex);
            StatusText = $"保存失败：{ex.Message}";
        }
    }

    /// <summary>加载项目：弹出对话框选文件后反序列化并重建画布。</summary>
    private void LoadProject()
    {
        var path = _dialogs.OpenProjectFile();
        if (string.IsNullOrEmpty(path)) return;   // 用户取消

        try
        {
            var project = ProjectSerializer.Load(path);
            Editor.LoadProjectFile(project, w => Logs.Add(new LogEntry(Now(), "rvs_kernel", "warning", w)));
            _currentFilePath = path;
            StatusText = $"已加载：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            CrashLogger.Write(ex);
            StatusText = $"加载失败：{ex.Message}";
        }
    }

    private async Task RunGraphAsync()
    {
        StatusText = "运行中";
        try
        {
            await Editor.ExecuteAsync((level, msg) =>
                Logs.Add(new LogEntry(Now(), "rvs_kernel", level, msg)));
        }
        catch (Exception ex)
        {
            Logs.Add(new LogEntry(Now(), "rvs_kernel", "error", ex.Message));
        }
        StatusText = "已停止";
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
}
