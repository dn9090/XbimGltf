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
	internal static class XbimShapeUtility
	{
		public static HashSet<short> GetDefaultExclusions(IModel model, HashSet<Type> excludedTypes)
		{
			var excluded = new HashSet<short>();

			if(excludedTypes == null)
				return excluded;

			foreach(var type in excludedTypes)
			{
				ExpressType expressType = type.IsInterface && type.Name.StartsWith("IIfc")
					? model.Metadata.ExpressType(type.Name.Substring(1).ToUpper())
					: model.Metadata.ExpressType(type);

				if(expressType == null)
					continue;
				
				foreach(var subType in expressType.NonAbstractSubTypes)
					excluded.Add(subType.TypeId);
			}
			
			return excluded;
		}

		public static IEnumerable<XbimShapeInstance> GetShapeInstances(IGeometryStoreReader geometryReader, HashSet<short> excludedTypes) =>
			geometryReader.ShapeInstances.Where(shape =>
				shape.RepresentationType == XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded &&
				!excludedTypes.Contains(shape.IfcTypeId));

		public static XbimShapeInstance FixShapeInstance(IGeometryStoreReader geometryReader, int ifcProductLabel) =>
			geometryReader.ShapeInstances.FirstOrDefault(shape =>
				shape.RepresentationType != XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded &&
				shape.IfcProductLabel == ifcProductLabel);

		public static bool IsSurfaceDoubleSided(IModel model, IXbimShapeGeometryData shapeGeometry)
		{
			var representationItem = model.Instances[shapeGeometry.IfcShapeLabel];

			if(representationItem == null) // REVIEW: Is this check necessary?
				return false;

			return representationItem is IIfcFaceBasedSurfaceModel
				|| representationItem is IIfcShellBasedSurfaceModel;
		}

		public static int GetColorId(IXbimShapeInstanceData shapeInstance) =>
			shapeInstance.StyleLabel > 0 ? shapeInstance.StyleLabel : -shapeInstance.IfcTypeId;
	}
}
