// Based on code from ColorMesh.cs, copyright (c) Microsoft 
// Based on code from https://github.com/baSSiLL/BodyScanner

using Microsoft.Kinect.Fusion;
using System.Diagnostics.Contracts;
using System.Windows.Media.Media3D;

namespace BodyScanner
{
    /// <summary>
    /// A class for converting mesh.
    /// Provides access to normals, triangle indexes.
    /// </summary>
    static class MeshConverter
    {
        /// <summary>
        /// Converts the mesh.
        /// </summary>
        /// <param name="mesh">The mesh</param>
        public static MeshGeometry3D Convert(Mesh mesh)
        {
          
            Contract.Requires(mesh != null);
            Contract.Ensures(Contract.Result<MeshGeometry3D>() != null);

            var result = new MeshGeometry3D();
            try
            {
                var vertices = mesh.GetVertices();
                foreach (var v in vertices)
                {
                    result.Positions.Add(ConvertToPoint(v));
                }

                var normals = mesh.GetNormals();
                foreach (var normal in normals)
                {
                    result.Normals.Add(ConvertToVector(normal));
                }

                var triangles = mesh.GetTriangleIndexes();
                foreach (var index in triangles)
                {
                    result.TriangleIndices.Add(index);
                }
            }
            catch
            {

            }

            result.Freeze();
            return result;
        }

        /// <summary>
        /// Converts 3d vectors to points.
        /// </summary>
        /// <param name="v">3 element Vector</param>
        private static Point3D ConvertToPoint(Vector3 v)
        {
            return new Point3D(v.X, v.Y, v.Z);
        }

        /// <summary>
        /// Converts normals to 3d vectors.
        /// </summary>
        /// <param name="v">3 element Vector</param>
        private static Vector3D ConvertToVector(Vector3 v)
        {
            return new Vector3D(v.X, v.Y, v.Z);
        }
    }
}
