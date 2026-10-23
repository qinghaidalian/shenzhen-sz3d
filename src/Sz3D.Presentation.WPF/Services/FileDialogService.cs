using Microsoft.Win32;

namespace Sz3D.Presentation.WPF.Services;

/// <summary>基于 <see cref="Microsoft.Win32"/> 通用对话框的 <see cref="IFileDialogService"/> 实现。</summary>
public sealed class FileDialogService : IFileDialogService
{
    // RVS 项目文件过滤器（.rvsp，JSON 格式）
    private const string ProjectFilter = "Sz3D 项目文件 (*.rvsp)|*.rvsp|所有文件 (*.*)|*.*";

    /// <summary>默认起始目录：应用当前工作目录（即程序安装/运行目录）。</summary>
    private static string DefaultDirectory => AppContext.BaseDirectory;

    /// <inheritdoc/>
    public string? OpenProjectFile()
    {
        var dlg = new OpenFileDialog
        {
            Title            = "加载项目",
            Filter           = ProjectFilter,
            DefaultExt       = ProjectSerializer.Extension,
            InitialDirectory = DefaultDirectory,
            CheckFileExists  = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    /// <inheritdoc/>
    public string? SaveProjectFileAs(string suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Title            = "保存项目",
            Filter           = ProjectFilter,
            DefaultExt       = ProjectSerializer.Extension,
            AddExtension     = true,
            InitialDirectory = DefaultDirectory,
            FileName         = suggestedFileName,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
