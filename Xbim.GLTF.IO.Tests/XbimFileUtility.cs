using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.GLTF;
using Xbim.ModelGeometry.Scene;

namespace Xbim.GLTF.IO.Tests
{
	internal static class XbimFileUtility
	{
		public static string GetTempGltfFile()
		{
			string sourcePath = Path.GetTempFileName();
			string destPath = Path.ChangeExtension(sourcePath, ".gltf");
			File.Move(sourcePath, destPath);

			return destPath;
		}

		public static string CreateXbimFile(string filePath)
		{
			string sourcePath = Path.GetTempFileName();
			string destPath = Path.ChangeExtension(sourcePath, ".xBIM");
			File.Move(sourcePath, destPath);

			IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
			using(var model = IfcStore.Open(filePath))
			{
				var context = new Xbim3DModelContext(model);
				context.CreateContext(null, false);
				model.SaveAs(destPath);
			}

			return destPath;
		}

		public static void CleanupFiles(params string[] filePaths)
		{
			try
			{
				foreach(string filePath in filePaths)
				{
					File.Delete(filePath);

					if(filePath.EndsWith(".xBIM"))
						File.Delete(Path.ChangeExtension(filePath, ".jfm"));
				}
			} catch {}
		}

		public static bool FilesEqual(string firstPath, string secondPath) =>
			File.ReadAllBytes(firstPath).SequenceEqual(File.ReadAllBytes(secondPath));
	}
}
