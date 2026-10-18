using System.Windows.Media;
using System.Windows.Media.Media3D;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Presentation.WPF.Services;

/// <summary>把点云转为 WPF 3D 模型（每个点渲染成一个小四边形）。</summary>
public static class PointCloudRenderer
{
    public static GeometryModel3D BuildModel(PointCloud cloud)
    {
        var mesh = new MeshGeometry3D();
        var material = new DiffuseMaterial(new SolidColorBrush(Colors.Cyan));

        if (cloud.Count == 0)
            return new GeometryModel3D { Geometry = mesh, Material = material, BackMaterial = material };

        // 归一化到 [0.05, 0.95] 立方体，使任意坐标范围的点云都能落在固定相机视野内
        float minX = cloud.X.Min(), maxX = cloud.X.Max();
        float minY = cloud.Y.Min(), maxY = cloud.Y.Max();
        float minZ = cloud.Z.Min(), maxZ = cloud.Z.Max();
        float span = MathF.Max(MathF.Max(maxX - minX, maxY - minY), maxZ - minZ);
        if (span <= 1e-6f) span = 1f;
        float scale = 0.9f / span;

        const float size = 0.008f;
        for (int i = 0; i < cloud.Count; i++)
        {
            float x = (cloud.X[i] - minX) * scale + 0.05f;
            float y = (cloud.Y[i] - minY) * scale + 0.05f;
            float z = (cloud.Z[i] - minZ) * scale + 0.05f;
            int b = mesh.Positions.Count;

            mesh.Positions.Add(new Point3D(x - size, y - size, z));
            mesh.Positions.Add(new Point3D(x + size, y - size, z));
            mesh.Positions.Add(new Point3D(x + size, y + size, z));
            mesh.Positions.Add(new Point3D(x - size, y + size, z));

            mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(b + 1);
            mesh.TriangleIndices.Add(b + 2);

            mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(b + 2);
            mesh.TriangleIndices.Add(b + 3);
        }

        return new GeometryModel3D
        {
            Geometry     = mesh,
            Material     = material,
            BackMaterial = material,   // 双面渲染，避免绕序导致点不可见
        };
    }
}
