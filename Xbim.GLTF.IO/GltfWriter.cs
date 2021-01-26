using System;
using System.Collections.Generic;
using System.Text;
using Gltf = glTFLoader.Schema;

namespace Xbim.GLTF
{
	internal class GltfWriter
	{
		public static readonly string Generator = "Xbim.GLTF.IO";
		
		public static readonly string Version = "2.0";

		private List<Gltf.Node> m_Nodes;

		private List<Gltf.Accessor> m_Accessors;

		private List<Gltf.Material> m_Materials;

		private List<Gltf.Mesh> m_Meshes;

		private Gltf.Node m_Root;

		private List<byte> m_IndexBuffer;

		private List<byte> m_VertexBuffer;
	
		public GltfWriter()
		{
			this.m_Accessors = new List<Gltf.Accessor>();
			this.m_Materials = new List<Gltf.Material>();
			this.m_Meshes = new List<Gltf.Mesh>();
			this.m_Nodes = new List<Gltf.Node>();
			this.m_IndexBuffer = new List<byte>();
			this.m_VertexBuffer = new List<byte>();

			WriteMaterial("Default material", 0.8f, 0.8f, 0.8f, 1.0f);

			this.m_Root = new Gltf.Node()
			{
				Name = "Z_UP",
				Matrix = new float[]
				{
					1.0f,  0.0f,  0.0f,  0.0f,
					0.0f,  0.0f, -1.0f,  0.0f,
					0.0f,  1.0f,  0.0f,  0.0f,
					0.0f,  0.0f,  0.0f,  1.0f
				}
			};

			this.m_Nodes.Add(this.m_Root);
		}

		public int WriteNode(string name, float[] matrix)
		{
			this.m_Nodes.Add(new Gltf.Node()
			{
				Name = name,
				Matrix = matrix
			});

			return this.m_Nodes.Count - 1;
		}

		public int WriteNode(string name, float[] matrix, int mesh)
		{
			this.m_Nodes.Add(new Gltf.Node()
			{
				Name = name,
				Matrix = matrix,
				Mesh = mesh
			});

			return this.m_Nodes.Count - 1;
		}

		public int WriteMaterial(string name, float red, float green, float blue, float alpha)
		{
			var material = new Gltf.Material();
			material.Name = name;
			material.PbrMetallicRoughness = new Gltf.MaterialPbrMetallicRoughness()
			{
				BaseColorFactor = new float[] { red, green, blue, alpha },
				MetallicFactor = 0f,
				RoughnessFactor = 1f
			};

			material.EmissiveFactor = new float[] { 0, 0, 0 }; // TODO: Move array allocation to static property.
			material.AlphaMode = (alpha < 1.0f)
				? Gltf.Material.AlphaModeEnum.BLEND
				: Gltf.Material.AlphaModeEnum.OPAQUE;
			material.AlphaCutoff = 0.5f;

			this.m_Materials.Add(material);

			return this.m_Materials.Count - 1;
		}

		public void WriteDoubleSided(int material, bool doubleSided)
		{
			this.m_Materials[material].DoubleSided = doubleSided;
		}

		public int WriteMesh(string name)
		{
			this.m_Meshes.Add(new Gltf.Mesh() { Name = name});
			return this.m_Meshes.Count - 1;
		}

		public int WritePrimitive(int mesh, int indexAccessor, int vertexAccessor, int normalsAcessor, int material)
		{
			var attributes = new Dictionary<string, int> {
				{ "NORMAL", normalsAcessor },
				{ "POSITION", vertexAccessor }
			};

			var primitive = new Gltf.MeshPrimitive()
			{
				Attributes = attributes,
				Indices = indexAccessor,
				Material = material,
				Mode = Gltf.MeshPrimitive.ModeEnum.TRIANGLES
			};

			var count = 1;

			if(this.m_Meshes[mesh].Primitives != null)
				count += this.m_Meshes[mesh].Primitives.Length;

			var primitives = this.m_Meshes[mesh].Primitives;
			Array.Resize(ref primitives, count);
			primitives[count - 1] = primitive;
			this.m_Meshes[mesh].Primitives = primitives;

			return count - 1;
		}

		public int WriteVertexAccessor(float[] values)
		{
			float[] min = new float[] { float.MaxValue, float.MaxValue, float.MaxValue };
			float[] max = new float[] { float.MinValue, float.MinValue, float.MinValue };

			for(int i = 0; i < values.Length; ++i)
			{
				int k = i % 3;
				if(values[i] < min[k])
					min[k] = values[i];
				if(values[i] > max[k])
					max[k] = values[i];
			}

			int offset = this.m_VertexBuffer.Count;
			var bytes = new byte[values.Length * sizeof(float)];
			Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
			this.m_VertexBuffer.AddRange(bytes);

			var accessor = new Gltf.Accessor()
			{
				BufferView = 1,
				ByteOffset = offset,
				ComponentType = Gltf.Accessor.ComponentTypeEnum.FLOAT,
				Normalized = false,
				Count = values.Length / 3,
				Type = Gltf.Accessor.TypeEnum.VEC3,
				Min = min,
				Max = max
			};

			this.m_Accessors.Add(accessor);

			return this.m_Accessors.Count - 1;
		}

		public int WriteIndexAccessor(int[] values, bool prevent8Bit = true)
		{
			int min = int.MaxValue;
			int max = int.MinValue;

			for(int i = 0; i < values.Length; ++i)
			{
				if(values[i] < min)
					min = values[i];
				if(values[i] > max)
					max = values[i];
			}

			// Find the best type to pack the indices together.
			var componentType = Gltf.Accessor.ComponentTypeEnum.BYTE;
			byte[] byteBuffer;
			int byteCount = 0;
			int size = 0;

			// UNSIGNED_ is used here but the values are casted to
			// signed types...
			if(!prevent8Bit && max < byte.MaxValue) // REVIEW: Should min be checked here too?
			{
				componentType = Gltf.Accessor.ComponentTypeEnum.UNSIGNED_BYTE;
				size = sizeof(byte);
				byteBuffer = new byte[values.Length * size];

				foreach(int value in values)
					byteBuffer[byteCount++] = (byte)value;
			} else if(max < ushort.MaxValue) {
				componentType = Gltf.Accessor.ComponentTypeEnum.UNSIGNED_SHORT;
				size = sizeof(ushort);
				byteBuffer = new byte[values.Length * size];

				foreach(int value in values)
					byteCount += CopyBytes((short)value, byteBuffer, byteCount);
			} else {
				componentType = Gltf.Accessor.ComponentTypeEnum.UNSIGNED_INT;
				size = sizeof(uint);
				byteBuffer = new byte[values.Length * size];

				foreach(int value in values)
					byteCount += CopyBytes((int)value, byteBuffer, byteCount);
			}

			// Add padding if neccessary.
			var padding = this.m_IndexBuffer.Count % size;
			for (int i = 0; i < padding; ++i)
				this.m_IndexBuffer.Add(0);

			var accessor = new Gltf.Accessor
			{
				BufferView = 0,
				ComponentType = componentType, 
				ByteOffset = this.m_IndexBuffer.Count,
				Normalized = false,
				Type = Gltf.Accessor.TypeEnum.SCALAR,
				Count = values.Length,
				Min = new float[] { min },
				Max = new float[] { max }
			};

			this.m_IndexBuffer.AddRange(byteBuffer);
			this.m_Accessors.Add(accessor);

			return this.m_Accessors.Count - 1;
		}

		public Gltf.Gltf ToGltf()
		{
			var gltf = new Gltf.Gltf();
			gltf.Asset = new Gltf.Asset()
			{
				Generator = Generator,
				Version = Version
			};

			if(this.m_VertexBuffer.Count == 0)
				return gltf;

			var vertexBufferView = new Gltf.BufferView()
			{
				Buffer = 0,
				Target = Gltf.BufferView.TargetEnum.ARRAY_BUFFER,
				ByteStride = 12,
				ByteLength = this.m_VertexBuffer.Count,
				ByteOffset = 0
			};

			var indexBufferView = new Gltf.BufferView()
			{
				Buffer = 0,
				Target = Gltf.BufferView.TargetEnum.ELEMENT_ARRAY_BUFFER,
				ByteLength = this.m_IndexBuffer.Count,
				ByteOffset = this.m_VertexBuffer.Count
			};
			
			var buffer = new Gltf.Buffer()
			{
				ByteLength = this.m_IndexBuffer.Count + this.m_VertexBuffer.Count
			};

			var scene = new Gltf.Scene()
			{
				Nodes = new int[] { 0 }
			};

			// Build buffer with raw binary data from the
			// index and vertex buffer.
			byte[] bytes = new byte[this.m_VertexBuffer.Count + this.m_IndexBuffer.Count];
			this.m_VertexBuffer.CopyTo(bytes, 0);
			this.m_IndexBuffer.CopyTo(bytes, this.m_VertexBuffer.Count);

			var sb = new StringBuilder();
			sb.Append("data:application/octet-stream;base64,");
			sb.Append(Convert.ToBase64String(bytes));
			buffer.Uri = sb.ToString();

			int[] nodes = new int[this.m_Nodes.Count - 1];
			for(int i = 1; i < this.m_Nodes.Count; ++i)
				nodes[i - 1] = i;

			this.m_Root.Children = nodes;

			gltf.BufferViews = new Gltf.BufferView[] { indexBufferView, vertexBufferView };
			gltf.Buffers = new Gltf.Buffer[] { buffer };
			gltf.Materials = this.m_Materials.ToArray();
			gltf.Accessors = this.m_Accessors.ToArray();
			gltf.Meshes = this.m_Meshes.ToArray();
			gltf.Nodes = this.m_Nodes.ToArray();
			gltf.Scenes = new Gltf.Scene[] { scene };
			gltf.Scene = 0;

			return gltf;
		}

		private static int CopyBytes(short value, byte[] buffer, int startIndex)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Array.Copy(bytes, 0, buffer, startIndex, bytes.Length);
			return bytes.Length;
		}

		private static int CopyBytes(int value, byte[] buffer, int startIndex)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Array.Copy(bytes, 0, buffer, startIndex, bytes.Length);
			return bytes.Length;
		}
	}
}