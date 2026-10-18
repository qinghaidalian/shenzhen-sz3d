namespace Sz3D.Core.Infrastructure.Models;

/// <summary>点云数据——SOA 布局以优化 SIMD 处理。</summary>
public sealed class PointCloud
{
    public float[] X  { get; init; } = [];
    public float[] Y  { get; init; } = [];
    public float[] Z  { get; init; } = [];
    public byte[]? R  { get; init; }   // 可选颜色
    public byte[]? G  { get; init; }
    public byte[]? B  { get; init; }
    public float[]? NX { get; init; }  // 可选法向量
    public float[]? NY { get; init; }
    public float[]? NZ { get; init; }
    public int Count   => X.Length;
}

/// <summary>6-DOF 位姿——位置（mm）+ 欧拉角（度）。</summary>
public readonly record struct Pose(
    double X, double Y, double Z,
    double Roll, double Pitch, double Yaw);

/// <summary>标定结果。</summary>
public sealed class CalibrationResult
{
    public double[,] IntrinsicMatrix  { get; init; } = new double[3, 3];
    public double[]  DistortionCoeffs { get; init; } = new double[5];
    public double[,] HandEyeMatrix    { get; init; } = new double[4, 4];
    public double    ReprojectionError { get; init; }
}

/// <summary>不可变端口数据载体。</summary>
public sealed class PortValue(Type dataType, object? data)
{
    public Type    DataType { get; } = dataType;
    public object? Data     { get; } = data;

    public static PortValue Create<T>(T value) => new(typeof(T), value);
    public T As<T>() => (T)(Data ?? throw new InvalidOperationException("PortValue is null."));
}
