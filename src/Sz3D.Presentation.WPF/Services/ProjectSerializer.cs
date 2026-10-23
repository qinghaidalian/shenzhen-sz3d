using System.IO;
using System.Text.Json;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Presentation.WPF.Services;

/// <summary>
/// 项目文件（<c>.rvsp</c>，JSON）读写服务。
/// 负责 <see cref="ProjectFile"/> DTO 与磁盘 JSON 文本之间的序列化/反序列化，
/// 不涉及图模型重建（由 <see cref="ViewModels.NodeEditorViewModel"/> 负责 DTO↔运行时转换）。
/// </summary>
public static class ProjectSerializer
{
    /// <summary>项目文件扩展名（含点）。</summary>
    public const string Extension = ".rvsp";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 中文标题/参数值不转义为 \uXXXX，保持 JSON 可读
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>把项目 DTO 以缩进 JSON 写入指定路径（覆盖）。目录不存在时自动创建。</summary>
    /// <param name="project">待保存的项目 DTO。</param>
    /// <param name="path">目标文件完整路径。</param>
    public static void Save(ProjectFile project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(project, Options);
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
    }

    /// <summary>从指定路径读取并反序列化项目 DTO。</summary>
    /// <param name="path">项目文件完整路径。</param>
    /// <returns>反序列化得到的 <see cref="ProjectFile"/>。</returns>
    /// <exception cref="InvalidDataException">文件内容为空或非法 JSON。</exception>
    public static ProjectFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var project = JsonSerializer.Deserialize<ProjectFile>(json, Options)
            ?? throw new InvalidDataException($"项目文件内容为空或无法解析：{path}");
        return project;
    }
}
