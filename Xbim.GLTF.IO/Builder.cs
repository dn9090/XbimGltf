using System;
using System.Collections.Generic;
using gltf = glTFLoader.Schema;
using Xbim.Common;
using Xbim.Common.Geometry;

namespace Xbim.GLTF
{
	[Obsolete("This class should only be used for compatibility reasons. Use the XbimGltfBuilder instead.")]
	public class Builder
	{
		public delegate bool MeshingFilter(int elementId, IModel model);

		/// <summary>
		/// A custom function to determine the behaviour and deflection associated with individual items in the mesher.
		/// Default properties can set in the Model.Modelfactors if the same deflection applies to all elements.
		/// </summary>
		public MeshingFilter CustomFilter { get; set; }

		/// <summary>
		/// This does nothing at moment because the buffer will always be base 64 encoded.
		/// </summary>
		public bool BufferInBase64 { get; set; }

		/// <summary>
		/// If true, this flat ensures that minimum 16 bits are used in the creation of indices.
		/// The flat is set to true by default.
		/// Rationale from feedback received is that: 
		/// "Despite the moderate size increase, it would preferable to use 16-bit indices rather than 8-bit indices. 
		/// Because modern APIs don’t actually support 8-bit vertex indices, these must be converted at runtime to 16-bits, 
		/// causing a net increase in runtime memory usage versus just storing them as 16 bits."
		/// </summary>
		public bool Prevent8bitIndices { get; set; }

		private XbimGltfBuilder m_Builder;

		private IModel m_Model;

		private HashSet<int> m_EntityLabels;

		public Builder()
		{
			this.Prevent8bitIndices = true;
		}

		public gltf.Gltf Build()
		{
			if(this.CustomFilter != null && this.m_EntityLabels == null) // Only use the custom filter.
				this.m_Builder.Filter = (shape) => CustomFilter(shape.IfcProductLabel, this.m_Model);
			else if(this.CustomFilter == null && this.m_EntityLabels != null) // Skip shapes that are not in the provided list.
				this.m_Builder.Filter = (shape) => !this.m_EntityLabels.Contains(shape.IfcProductLabel);
			else if(this.CustomFilter != null && this.m_EntityLabels != null) // Skip shapes that are not in the provided list and filter them.
				this.m_Builder.Filter = (shape) => !this.m_EntityLabels.Contains(shape.IfcProductLabel) && CustomFilter(shape.IfcProductLabel, this.m_Model);
		
			this.m_Builder.Prevent8bitIndices = this.Prevent8bitIndices;
			return this.m_Builder.Build();
		}

		/// <summary>
		/// Exports a gltf file from a meshed model
		/// </summary>
		/// <param name="model">The model needs to have the geometry meshes already cached</param>
		/// <param name="exclude">The types of elements that are going to be omitted (e.g. ifcSpaces).</param>
		/// <param name="EntityLebels">Only entities in the collection are exported; if null exports the whole model</param>
		/// <returns></returns>
		public gltf.Gltf BuildInstancedScene(IModel model, XbimMatrix3D overallTransform, List<Type> exclude = null, HashSet<int> EntityLebels = null)
		{
			this.m_Model = model;
			this.m_EntityLabels = EntityLebels;
			this.m_Builder = new XbimGltfBuilder(this.m_Model, overallTransform);
			this.m_Builder.MergePrimitives = true;

			// Overwrite the default exclusions with the provided list.
			if(exclude != null)
			{
				this.m_Builder.ExcludedTypes.Clear();
				this.m_Builder.ExcludedTypes.UnionWith(exclude);
			}

			return Build();
		}
	}
}
