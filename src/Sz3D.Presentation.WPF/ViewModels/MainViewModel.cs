using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Sz3D.Core.Infrastructure.Execution;
using Sz3D.Presentation.WPF.Commands;
using Sz3D.Presentation.WPF.Services;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>日志视图中的一行。</summary>
public sealed record LogEntry(string Timestamp, string Channel, string Level, string Message);

/// <summary>主窗口 DataContext。</summary>
public sealed class MainViewModel : ViewModelBase
{
    public NodeEditorViewModel Editor { get; } = new();
    public IReadOnlyList<OperatorGroup> OperatorGroups => NodeLibrary.Groups;

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
