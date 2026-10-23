using System.Text.Json;
using Sz3D.Core.Infrastructure.Models;
using Xunit;

namespace Sz3D.Core.Infrastructure.Tests;

/// <summary>
/// 项目文件（.rvsp）DTO 的 JSON 序列化契约测试。
/// 验证节点、连线、分组、节点参数经 System.Text.Json 往返后完整保留，
/// 对应 UI 层 ProjectSerializer 使用的同一套序列化选项。
/// </summary>
public sealed class ProjectDtoSerializationTests
{
    // 与 ProjectSerializer.Options 一致（缩进 + 宽松转义），保证测试覆盖真实行为
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static ProjectFile RoundTrip(ProjectFile p) =>
        JsonSerializer.Deserialize<ProjectFile>(JsonSerializer.Serialize(p, Options), Options)!;

    private static ProjectFile SampleProject()
    {
        var n1 = Guid.NewGuid();
        var n2 = Guid.NewGuid();
        return new ProjectFile
        {
            ProjectName = "样例项目",
            Graph = new GraphDto
            {
                Nodes =
                [
                    new NodeDto
                    {
                        Id = n1, TypeId = "Sz3D.Plugins.Algorithms:LoadImageNode",
                        DisplayName = "加载图片", X = 120, Y = 80,
                        Parameters = new() { ["FilePath"] = @"D:\pics\12.jpg", ["Enabled"] = "True" },
                    },
                    new NodeDto
                    {
                        Id = n2, TypeId = "Sz3D.Plugins.Algorithms:GaussianBlurNode",
                        DisplayName = "高斯滤波", X = 360, Y = 80,
                        Parameters = new() { ["KernelSize"] = "5" },
                    },
                ],
                Connections =
                [
                    new ConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = n1, SourcePort = "Output",
                        TargetNodeId = n2, TargetPort = "Input",
                    },
                ],
                Groups =
                [
                    new GroupDto { Id = Guid.NewGuid(), Title = "预处理", X = 100, Y = 40, Width = 400, Height = 200, Color = "#0E639C" },
                ],
            },
        };
    }

    [Fact]
    public void RoundTrip_PreservesProjectName()
    {
        var result = RoundTrip(SampleProject());
        Assert.Equal("样例项目", result.ProjectName);
    }

    [Fact]
    public void RoundTrip_PreservesNodesWithPositionsAndParameters()
    {
        var original = SampleProject();
        var result = RoundTrip(original);

        Assert.Equal(2, result.Graph.Nodes.Count);

        var load = result.Graph.Nodes[0];
        Assert.Equal(original.Graph.Nodes[0].Id, load.Id);
        Assert.Equal("Sz3D.Plugins.Algorithms:LoadImageNode", load.TypeId);
        Assert.Equal("加载图片", load.DisplayName);
        Assert.Equal(120, load.X);
        Assert.Equal(80, load.Y);
        Assert.Equal(@"D:\pics\12.jpg", load.Parameters["FilePath"]);
        Assert.Equal("True", load.Parameters["Enabled"]);
    }

    [Fact]
    public void RoundTrip_PreservesConnectionsByNodeIdAndPort()
    {
        var original = SampleProject();
        var result = RoundTrip(original);

        var c = Assert.Single(result.Graph.Connections);
        Assert.Equal(original.Graph.Nodes[0].Id, c.SourceNodeId);
        Assert.Equal("Output", c.SourcePort);
        Assert.Equal(original.Graph.Nodes[1].Id, c.TargetNodeId);
        Assert.Equal("Input", c.TargetPort);
    }

    [Fact]
    public void RoundTrip_PreservesGroups()
    {
        var result = RoundTrip(SampleProject());

        var g = Assert.Single(result.Graph.Groups);
        Assert.Equal("预处理", g.Title);
        Assert.Equal(400, g.Width);
        Assert.Equal(200, g.Height);
        Assert.Equal("#0E639C", g.Color);
    }

    [Fact]
    public void Serialize_ProducesHumanReadableUnescapedJson()
    {
        var json = JsonSerializer.Serialize(SampleProject(), Options);
        // 中文不应被转义为 \uXXXX（宽松转义），路径与字段名可读
        Assert.Contains("样例项目", json);
        Assert.Contains("加载图片", json);
        Assert.Contains("FilePath", json);
    }
}
