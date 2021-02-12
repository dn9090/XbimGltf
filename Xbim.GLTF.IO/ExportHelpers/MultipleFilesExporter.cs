using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.GLTF.SemanticExport;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace Xbim.GLTF.ExportHelpers
{
	public class MultipleFilesExporter
	{
		public void ExportByStorey(string fileName, bool exportSemantic = true)
		{			
			var ifcName = Path.ChangeExtension(fileName, "ifc");
			using(var store = IfcStore.Open(ifcName))
				ExportByStorey(store, exportSemantic);
		}

		public void ExportByStorey(IfcStore store, bool exportSemantic = true)
		{
			var directoryName = Path.GetDirectoryName(store.FileName);

			// Create the model context.
			if(store.GeometryStore.IsEmpty)
			{
				var context = new Xbim3DModelContext(store);
				context.CreateContext();
			}

			var elements = new HashSet<int>();

			var builder = new XbimGltfBuilder(store);
			builder.Filter = (shape) => !elements.Contains(shape.IfcProductLabel);

			foreach(var storey in store.Instances.OfType<IIfcBuildingStorey>())
			{
				var rels = store.Instances.OfType<IIfcRelContainedInSpatialStructure>()
					.Where(x => x.RelatingStructure.EntityLabel == storey.EntityLabel);
				
				elements.Clear();

				// Find the elements of the current storey.
				foreach(var rel in rels)
				{
					var entitiesInStorey = rel.RelatedElements.Select(x => x.EntityLabel).ToHashSet();
					elements.UnionWith(entitiesInStorey);

					var relsToComposingEntities = store.Instances.OfType<IIfcRelAggregates>()
						.Where(x => entitiesInStorey.Contains(x.RelatingObject.EntityLabel));

					foreach(var relToComposingEntities in relsToComposingEntities)
						elements.UnionWith(relToComposingEntities.RelatedObjects.Select(x => x.EntityLabel));  
				}

				var storeyName = string.Concat(storey.Name.ToString().Split(Path.GetInvalidFileNameChars()));
				var fileName = Path.GetFileNameWithoutExtension(store.FileName) + "." + storeyName + ".gltf";
				var filePath = Path.Combine(directoryName, fileName);

				builder.Build().SaveAs(filePath);
				
				if(exportSemantic)
				{
					var bme = new BuildingModelExtractor();
					bme.CustomFilter = (elementId, model) => !elements.Contains(elementId);
					var buildingModel = bme.GetModel(store);
					buildingModel.Export(Path.ChangeExtension(filePath, "json"));
				}
			}
		}
	}
}
