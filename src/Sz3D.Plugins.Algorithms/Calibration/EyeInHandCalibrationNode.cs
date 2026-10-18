using Sz3D.Core.Infrastructure;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Algorithms.Calibration;

[NodeType(Category = "标定", Name = "Eye-In-Hand 手眼标定")]
public sealed class EyeInHandCalibrationNode : NodeBase
{
    public override IReadOnlyList<PortDescriptor> InputPorts  => [new("Images", typeof(byte[][]), PortDirection.Input), new("Poses", typeof(Pose[]), PortDirection.Input)];
    public override IReadOnlyList<PortDescriptor> OutputPorts => [new("Result", typeof(CalibrationResult), PortDirection.Output)];
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // TODO: OpenCvSharp 手眼标定
        ctx.SetOutput("Result", new CalibrationResult());
        return Task.CompletedTask;
    }
}
