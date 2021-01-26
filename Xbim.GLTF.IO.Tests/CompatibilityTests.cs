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
	public class CompatibilityTests : IDisposable
	{
		private string m_XbimPath;

		public CompatibilityTests()
		{
			this.m_XbimPath = Path.GetTempFileName();
		}

		[Fact]
		public void Compatible_Default()
		{
			string deprecatedPath = Path.GetTempFileName();
			string compabilityPath = Path.GetTempFileName();
			string builderPath = Path.GetTempFileName();

			using(var model = IfcStore.Open(CreateXbimFile("Files/OneWallTwoWindows.ifc")))
			{
				var deprecatedBuilder = new Xbim.GLTF.Deprecated.Builder();
				var compabilityBuilder = new Builder();
				var builder = new XbimGltfBuilder(model);

				deprecatedBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(deprecatedPath);
				compabilityBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(compabilityPath);
				builder.Build().SaveAs(builderPath);
			}

			Assert.True(FilesEqual(deprecatedPath, compabilityPath));
			Assert.True(FilesEqual(deprecatedPath, builderPath));

			CleanupFiles(deprecatedPath, compabilityPath, builderPath);
		}

		[Fact]
		public void Compatible_Default_Large()
		{
			string deprecatedPath = Path.GetTempFileName();
			string compabilityPath = Path.GetTempFileName();
			string builderPath = Path.GetTempFileName();

			using(var model = IfcStore.Open(CreateXbimFile("Files/231110AC11-FZK-Haus-IFC.ifc")))
			{
				var deprecatedBuilder = new Xbim.GLTF.Deprecated.Builder();
				var compabilityBuilder = new Builder();
				var builder = new XbimGltfBuilder(model);

				deprecatedBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(deprecatedPath);
				compabilityBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(compabilityPath);
				builder.Build().SaveAs(builderPath);
			}
		
			Assert.True(FilesEqual(deprecatedPath, compabilityPath));
			Assert.True(FilesEqual(deprecatedPath, builderPath));

			CleanupFiles(deprecatedPath, compabilityPath, builderPath);
		}

		[Fact]
		public void Compatible_TypeFilter()
		{
			string deprecatedPath = Path.GetTempFileName();
			string compabilityPath = Path.GetTempFileName();
			string builderPath = Path.GetTempFileName();

			using(var model = IfcStore.Open(CreateXbimFile("Files/OneWallTwoWindows.ifc")))
			{
				var deprecatedBuilder = new Xbim.GLTF.Deprecated.Builder();
				var compabilityBuilder = new Builder();
				var builder = new XbimGltfBuilder(model);

				var exclude = new List<Type>() { typeof(IIfcWindow) };

				deprecatedBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity, exclude: exclude).SaveAs(deprecatedPath);
				compabilityBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity, exclude: exclude).SaveAs(compabilityPath);
				builder.ExcludedTypes.UnionWith(exclude);
				builder.Build().SaveAs(builderPath);
			}
		
			Assert.True(FilesEqual(deprecatedPath, compabilityPath));
			Assert.True(FilesEqual(deprecatedPath, builderPath));

			CleanupFiles(deprecatedPath, compabilityPath, builderPath);
		}

		public void Dispose()
		{
			try
			{
				File.Delete(Path.ChangeExtension(this.m_XbimPath, ".jfm"));
				File.Delete(this.m_XbimPath);
			} catch {}
		}

		private string CreateXbimFile(string filePath)
		{
			string path = Path.ChangeExtension(this.m_XbimPath, ".xBIM");
			File.Move(this.m_XbimPath, path);
			this.m_XbimPath = path;

			IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
			using(var model = IfcStore.Open(filePath))
			{
				var context = new Xbim3DModelContext(model);
				context.CreateContext(null, false);
				model.SaveAs(this.m_XbimPath);
			}

			return this.m_XbimPath;
		}

		private void CleanupFiles(params string[] filePaths)
		{
			try
			{
				foreach(string filePath in filePaths)
				{
					File.Delete(filePath);
					File.Delete(Path.ChangeExtension(filePath, ".jfm"));
				}
			} catch {}
		}

		public static bool FilesEqual(string firstPath, string secondPath) =>
			File.ReadAllBytes(firstPath).SequenceEqual(File.ReadAllBytes(secondPath));
	}
}
