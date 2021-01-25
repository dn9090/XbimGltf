using System;
using System.Collections.Generic;
using System.IO;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;

namespace Xbim.GLTF
{
	internal struct XbimMesh
	{
		public float[] positions;

		public float[] normals;

		public int[] indices;

		public XbimMesh(float[] positions, float[] normals, int[] indices)
		{
			this.positions = positions;
			this.normals = normals;
			this.indices = indices;
		}

		public static XbimMesh Empty() => new XbimMesh(Array.Empty<float>(), Array.Empty<float>(), Array.Empty<int>());

		public static XbimMesh Create(IXbimShapeGeometryData geometry, double modelFactor = 1) => Create(geometry.ShapeData, XbimMatrix3D.Identity, modelFactor);

		public static XbimMesh Create(byte[] bytes, XbimMatrix3D transform, double modelFactor = 1)
		{
			XbimQuaternion rotation = new XbimQuaternion(0.0, 0.0, 0.0, 1.0);
			bool skipTransformation = false;

			if(!transform.IsIdentity)
			{
				rotation = transform.GetRotationQuaternion();
				skipTransformation = rotation.IsIdentity();
			}

			using(var ms = new MemoryStream(bytes))
			using(var br = new BinaryReader(ms))
			{
				var triangulation = br.ReadShapeTriangulation();
			
				if(triangulation.Faces.Count == 0)
					return XbimMesh.Empty();

				// This method should be optimized...
				triangulation.ToPointsWithNormalsAndIndices(
					out List<float[]> triPoints,
					out List<int> triIndices);

				float[] positions = new float[triPoints.Count * 3];
				float[] normals = new float[triPoints.Count * 3];
				int[] indices = new int[triIndices.Count];

				// Copy triangulation positions and normals.
				// The first loop skips the calculation of the transformation
				// and rotation to save performance.
				if(skipTransformation)
				{
					for(int i = 0; i < triPoints.Count; ++i)
					{
						int index = i * 3;
						var points = triPoints[i];

						positions[index] = (float)(points[0] / modelFactor);
						positions[index + 1] = (float)(points[1] / modelFactor);
						positions[index + 2] = (float)(points[2] / modelFactor);

						Array.Copy(points, 3, normals, index, 3);
					}
				} else {
					for(int i = 0; i < triPoints.Count; ++i)
					{
						int index = i * 3;
						var points = triPoints[i];

						var position = new XbimPoint3D(points[0], points[1], points[2]);
						position = transform.Transform(position);

						positions[index] = (float)(position.X / modelFactor);
						positions[index + 1] = (float)(position.Y / modelFactor);
						positions[index + 2] = (float)(position.Z / modelFactor);

						var normal = new XbimVector3D(points[3], points[4], points[5]);
						XbimQuaternion.Transform(ref normal, ref rotation, out normal);

						normals[index] = (float)(normal.X);
						normals[index + 1] = (float)(normal.Y);
						normals[index + 2] = (float)(normal.Z);
					}
				}

				// Copy triangulation indices.
				triIndices.CopyTo(indices);

				return new XbimMesh(positions, normals, indices);
			}
		}

		public void Combine(XbimMesh mesh) => Combine(mesh.positions, mesh.normals, mesh.indices);

		public void Combine(float[] positions, float[] normals, int[] indices)
		{
			int length = this.positions.Length;
			Array.Resize(ref this.positions, length + positions.Length);
			Array.Copy(positions, 0, this.positions, length, positions.Length);

			length = this.normals.Length;
			Array.Resize(ref this.normals, length + normals.Length);
			Array.Copy(normals, 0, this.normals, length, normals.Length);

			length = this.indices.Length;
			Array.Resize(ref this.indices, length + indices.Length);
			Array.Copy(indices, 0, this.indices, length, indices.Length);
		}
	}
}