using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Gltf = glTFLoader.Schema;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Metadata;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Xbim.GLTF
{
	public class XbimGltfBuilder
	{
		/// <summary>
		/// If a shape matches the filter it will be excluded from the GLTF.
		/// </summary>
		public Func<XbimShapeInstance, bool> Filter { get; set; }

		/// <summary>
		/// All types included in this set and the IFC model will be excluded from the GLTF.
		/// </summary>
		public ISet<Type> ExcludedTypes => this.m_ExcludedTypes;

		/// <summary>
		/// If true, this flat ensures that minimum 16 bits are used in the creation of indices.
		/// The flat is set to true by default.
		/// Rationale from feedback received is that: 
		/// "Despite the moderate size increase, it would preferable to use 16-bit indices rather
		/// than 8-bit indices. 
		/// Because modern APIs don’t actually support 8-bit vertex indices, these must be
		/// converted at runtime to 16-bits, 
		/// causing a net increase in runtime memory usage versus just storing them as 16 bits."
		/// </summary>
		public bool Prevent8bitIndices { get; set; }

		/// <summary>
		/// Does nothing at the moment. In the future this will indicate if
		/// the GLTF should contain a node with a separate mesh for every primitive
		/// or if the primitives can be merged together to form a single mesh.
		/// </summary>
		public bool MergePrimitives { get; set; }

		private IModel m_Model;

		private XbimMatrix3D m_Transform;

		private HashSet<Type> m_ExcludedTypes;

		public XbimGltfBuilder(IModel model, XbimMatrix3D transform)
		{
			if(model == null)
				throw new ArgumentNullException(nameof(model));

			if(model.GeometryStore.IsEmpty)
				throw new ArgumentException("The " + nameof(model.GeometryStore) + " is empty. Create a model context first.");
				
			this.m_Model = model;
			this.m_Transform = transform;
			this.m_ExcludedTypes = new HashSet<Type>() { typeof(IIfcSpace), typeof(IIfcFeatureElement) };
			this.Prevent8bitIndices = true;
			this.MergePrimitives = false;
		}

		public XbimGltfBuilder(IModel model) : this(model, XbimMatrix3D.Identity)
		{
		}

		public Gltf.Gltf Build()
		{
			var writer = new GltfWriter();
			var shapeQueue = new BlockingCollection<ShapeData>();
			var geometryQueue = new BlockingCollection<GeometryData>();
			var shapeWorker = this.MergePrimitives
				? new Thread(() => ShapeWorker(shapeQueue, geometryQueue, writer))
				: new Thread(() => ShapeHierarchyWorker(shapeQueue, geometryQueue, writer));
			var meshWorker = new Thread(() => MeshBuilderWorker(geometryQueue, writer));
			shapeWorker.Start();
			meshWorker.Start();

			using(var geometryStore = this.m_Model.GeometryStore)
			using(var geometryReader = geometryStore.BeginRead())
			{
				var styleMap = new Dictionary<int, int>();

				foreach(var styleId in geometryReader.StyleIds)
				{
					var color = GetColorFromSurfaceStyle(styleId, out string styleName);
					var material = -1;

					material = writer.WriteMaterial(styleName, color.Red, color.Green, color.Blue, color.Alpha);
					
					/* This check should be implemented. But for some reason
					   it doesn't match the original implementation.
					   If no name is found the default material should be used.

					if(string.IsNullOrEmpty(styleName))
						styleMap.Add(styleId, 0);
					*/

					styleMap.Add(styleId, material);
				}

				var excludedTypes = GetDefaultExclusions();
				var shapeInstances = GetShapeInstances(geometryReader, excludedTypes);
				
				foreach(var shapeInstance in shapeInstances.OrderBy(x => x.IfcProductLabel))
				{
					if(Filter != null && this.Filter.Invoke(shapeInstance))
						continue;

					IXbimShapeGeometryData shapeGeometry = geometryReader.ShapeGeometry(shapeInstance.ShapeGeometryLabel);

					if(shapeGeometry.Format != (byte)XbimGeometryType.PolyhedronBinary)
						continue;

					// Prepare the material that gets assigned to the mesh.
					// If the color id is not in the known styles create a new material.
					int material = -1, colorId = GetColorId(shapeInstance);

					if(!styleMap.TryGetValue(colorId, out material))
					{
						var color = GetColorFromType(shapeInstance.IfcTypeId, out string colorName);
						material = writer.WriteMaterial(colorName, color.Red, color.Green, color.Blue, color.Alpha);
						styleMap.Add(colorId, material);
					}

					writer.WriteDoubleSided(material, CheckDoubleSidedSurface(shapeGeometry));

					shapeQueue.Add(new ShapeData(shapeInstance, shapeGeometry, material));
				}
			}

			shapeQueue.CompleteAdding();
			shapeWorker.Join();
			meshWorker.Join();
			shapeQueue.Dispose();
			geometryQueue.Dispose();

			return writer.ToGltf();
		}

		private struct ShapeData
		{
			public XbimShapeInstance shapeInstance;

			public IXbimShapeGeometryData shapeGeometry;
		
			public int material;

			public ShapeData(XbimShapeInstance shapeInstance, IXbimShapeGeometryData shapeGeometry, int material)
			{
				this.shapeInstance = shapeInstance;
				this.shapeGeometry = shapeGeometry;
				this.material = material;
			}
		}

		private void ShapeWorker(BlockingCollection<ShapeData> shapeQueue, BlockingCollection<GeometryData> geometryQueue, GltfWriter writer)
		{
			int currentProductLabel = -1, targetMesh = -1;

			while(!shapeQueue.IsCompleted)
			{
				if(!shapeQueue.TryTake(out ShapeData shapeData, 100))
					continue;

				var shapeInstance = shapeData.shapeInstance;
				var shapeGeometry = shapeData.shapeGeometry;
			
				// Check if the product label changed. If that is the case
				// create a new node and target mesh which will contain all
				// primitives from all shapes that match the product label.
				if(currentProductLabel != shapeInstance.IfcProductLabel)
				{
					var entity = this.m_Model.Instances[shapeInstance.IfcProductLabel] as IIfcProduct;
					var transform = TransformToMeters(shapeInstance.Transformation);
					var mesh = writer.WriteMesh($"Instance {shapeInstance.IfcProductLabel}"); // @Separate: This needs to be created for every shape.
					var node = writer.WriteNode($"{entity.Name} #{entity.EntityLabel}", transform.ToFloatArray(), mesh); // @Separate: This needs to be created for every shape. Also this needs to be added to a parent.
				
					currentProductLabel = shapeInstance.IfcProductLabel;
					targetMesh = mesh;
				}

				geometryQueue.Add(new GeometryData(shapeGeometry, targetMesh, shapeData.material));
			}

			geometryQueue.CompleteAdding();
		}

		private void ShapeHierarchyWorker(BlockingCollection<ShapeData> shapeQueue, BlockingCollection<GeometryData> geometryQueue, GltfWriter writer)
		{
			int currentProductLabel = -1;
			IIfcProduct currentProduct = null;
			var children = new List<int>();

			while(!shapeQueue.IsCompleted)
			{
				if(!shapeQueue.TryTake(out ShapeData shapeData, 100))
					continue;
			
				var shapeInstance = shapeData.shapeInstance;
				var shapeGeometry = shapeData.shapeGeometry;

				// Check if the product label changed.
				// If there are child nodes 
				if(currentProductLabel != shapeInstance.IfcProductLabel)
				{
					if(children.Count > 0)
					{
						writer.WriteNode($"{currentProduct.Name} #{currentProduct.EntityLabel}", XbimMatrix3D.Identity.ToFloatArray(), children.ToArray());
						children.Clear();
					}

					currentProductLabel = shapeInstance.IfcProductLabel;
					currentProduct = this.m_Model.Instances[currentProductLabel] as IIfcProduct;
				}

				var transform = TransformToMeters(shapeInstance.Transformation);
				var mesh = writer.WriteMesh($"Instance {shapeInstance.IfcProductLabel}");
				var node = writer.WriteSubNode($"Shape #{shapeGeometry.IfcShapeLabel}", transform.ToFloatArray(), mesh);

				children.Add(node);

				geometryQueue.Add(new GeometryData(shapeGeometry, mesh, shapeData.material));
			}

			geometryQueue.CompleteAdding();
		}

		private struct GeometryData
		{
			public IXbimShapeGeometryData shapeGeometry;

			public int mesh;

			public int material;

			public GeometryData(IXbimShapeGeometryData shapeGeometry, int mesh, int material)
			{
				this.shapeGeometry = shapeGeometry;
				this.mesh = mesh;
				this.material = material;
			}
		}

		private void MeshBuilderWorker(BlockingCollection<GeometryData> geometryQueue, GltfWriter writer)
		{
			var geometries = new Dictionary<int, int[]>();
			
			while(!geometryQueue.IsCompleted)
			{
				if(!geometryQueue.TryTake(out GeometryData geometryData, 100))
					continue;

				var shapeGeometry = geometryData.shapeGeometry;
				int indexAccessor = -1, normalsAccessor = -1, vertexAccessor = -1;

				if(shapeGeometry.ReferenceCount > 1)
				{
					// If the geometry is referenced multiple times check if it's already in the cache.
					if(geometries.TryGetValue(shapeGeometry.ShapeLabel, out int[] accessors))
					{
						indexAccessor = accessors[0];
						normalsAccessor = accessors[1];
						vertexAccessor = accessors[2];
					} else {
						var xbimMesh = XbimMesh.Create(shapeGeometry, this.m_Model.ModelFactors.OneMeter);
						indexAccessor = writer.WriteIndexAccessor(xbimMesh.indices, prevent8Bit: this.Prevent8bitIndices);
						normalsAccessor = writer.WriteVertexAccessor(xbimMesh.normals);
						vertexAccessor = writer.WriteVertexAccessor(xbimMesh.positions);

						geometries.Add(shapeGeometry.ShapeLabel, new int[] { indexAccessor, normalsAccessor, vertexAccessor });
					}
				} else {
					var xbimMesh = XbimMesh.Create(shapeGeometry, this.m_Model.ModelFactors.OneMeter);
					indexAccessor = writer.WriteIndexAccessor(xbimMesh.indices, prevent8Bit: this.Prevent8bitIndices);
					normalsAccessor = writer.WriteVertexAccessor(xbimMesh.normals);
					vertexAccessor = writer.WriteVertexAccessor(xbimMesh.positions);
				}

				writer.WritePrimitive(geometryData.mesh, indexAccessor, vertexAccessor, normalsAccessor, geometryData.material);
			}
		}

		private IEnumerable<XbimShapeInstance> GetShapeInstances(IGeometryStoreReader geometryReader, HashSet<short> excludedTypes) =>
			geometryReader.ShapeInstances.Where(shape =>
				shape.RepresentationType == XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded
				&& !excludedTypes.Contains(shape.IfcTypeId));

		private HashSet<short> GetDefaultExclusions()
		{
			var excluded = new HashSet<short>();

			if(this.m_ExcludedTypes == null)
				return excluded;

			foreach(var type in this.m_ExcludedTypes)
			{
				ExpressType expressType;

				if(type.IsInterface && type.Name.StartsWith("IIfc"))
					expressType = this.m_Model.Metadata.ExpressType(type.Name.Substring(1).ToUpper());
				else
					expressType = this.m_Model.Metadata.ExpressType(type);
				
				if(expressType == null)
					continue;
				
				foreach(var subType in expressType.NonAbstractSubTypes)
					excluded.Add(subType.TypeId);
			}
			
			return excluded;
		}

		private XbimColour GetColorFromSurfaceStyle(int styleId, out string name)
		{
			var surfaceStyle = this.m_Model.Instances[styleId] as IIfcSurfaceStyle;
			var colorMap = XbimTexture.Create(surfaceStyle).ColourMap;
			name = surfaceStyle.Name;

			if(colorMap.Any())
				return colorMap[0];
			
			return new XbimColour(name, 0.8, 0.8, 0.8);
		}

		private static readonly XbimColourMap s_ColorMap = new XbimColourMap();

		private XbimColour GetColorFromType(short typeId, out string name)
		{
			var expressType = this.m_Model.Metadata.ExpressType(typeId);
			name = expressType.Name;
			
			return s_ColorMap[expressType.Name]; // I don't know what this does...
		}

		private bool CheckDoubleSidedSurface(IXbimShapeGeometryData shapeGeometry)
		{
			var representationItem = this.m_Model.Instances[shapeGeometry.IfcShapeLabel];

			if(representationItem == null) // REVIEW: Is this check necessary?
				return false;

			return representationItem is IIfcFaceBasedSurfaceModel
				|| representationItem is IIfcShellBasedSurfaceModel;
		}

		private XbimMatrix3D TransformToMeters(XbimMatrix3D matrix)
		{
			matrix.OffsetX /= this.m_Model.ModelFactors.OneMeter;
			matrix.OffsetY /= this.m_Model.ModelFactors.OneMeter;
			matrix.OffsetZ /= this.m_Model.ModelFactors.OneMeter;
			return XbimMatrix3D.Multiply(matrix, this.m_Transform);
		}

		private static int GetColorId(IXbimShapeInstanceData shapeInstance) =>
			shapeInstance.StyleLabel > 0
			? shapeInstance.StyleLabel
			: shapeInstance.IfcTypeId * -1;
	}
}
