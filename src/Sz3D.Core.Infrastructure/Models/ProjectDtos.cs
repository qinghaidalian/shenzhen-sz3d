using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sz3D.Core.Infrastructure.Models;

public sealed record ProjectFile
{
    public string FormatVersion { get; init; } = "1.0";
    public string ProjectName   { get; init; } = "";
    public GraphDto  Graph      { get; init; } = new();
    public CanvasLayoutDto Layout { get; init; } = new();
    public Dictionary<string, JsonElement> NodeParameters { get; init; } = [];
}

public sealed record GraphDto
{
    public List<NodeDto>       Nodes       { get; init; } = [];
    public List<ConnectionDto> Connections { get; init; } = [];
    public List<GroupDto>      Groups      { get; init; } = [];
}

/// <summary>分组矩形的持久化 DTO（.rvsp JSON）。</summary>
public sealed record GroupDto
{
    public Guid   Id     { get; init; }
    public string Title  { get; init; } = "";
    public double X      { get; init; }
    public double Y      { get; init; }
    public double Width  { get; init; }
    public double Height { get; init; }
    public string Color  { get; init; } = "";
}

public sealed record NodeDto
{
    public Guid   Id          { get; init; }
    public string TypeId      { get; init; } = "";  // "{PluginId}:{TypeName}"
    public string DisplayName { get; init; } = "";
    public double X           { get; init; }
    public double Y           { get; init; }
}

public sealed record ConnectionDto
{
    public Guid   Id           { get; init; }
    public Guid   SourceNodeId { get; init; }
    public string SourcePort   { get; init; } = "";
    public Guid   TargetNodeId { get; init; }
    public string TargetPort   { get; init; } = "";
}

public sealed record CanvasLayoutDto
{
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double Scale   { get; init; } = 1.0;
}
