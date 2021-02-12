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
		private string m_XbimSmall;

		private string m_XbimLarge;

		public CompatibilityTests()
		{
			this.m_XbimSmall = XbimFileUtility.CreateXbimFile("Files/OneWallTwoWindows.ifc");
			this.m_XbimLarge = XbimFileUtility.CreateXbimFile("Files/231110AC11-FZK-Haus-IFC.ifc");
		}

		[Fact]
		public void Compatible_Default()
		{
			string deprecatedPath = XbimFileUtility.GetTempGltfFile();
			string compabilityPath = XbimFileUtility.GetTempGltfFile();
			string builderPath = XbimFileUtility.GetTempGltfFile();

			using(var model = IfcStore.Open(this.m_XbimSmall))
			{
				var deprecatedBuilder = new Xbim.GLTF.Deprecated.Builder();
				var compabilityBuilder = new Builder();
				var builder = new XbimGltfBuilder(model);
				builder.MergePrimitives = true;

				deprecatedBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(deprecatedPath);
				compabilityBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(compabilityPath);
				builder.Build().SaveAs(builderPath);
			}

			Assert.True(XbimFileUtility.FilesEqual(deprecatedPath, compabilityPath));
			Assert.True(XbimFileUtility.FilesEqual(deprecatedPath, builderPath));

			XbimFileUtility.CleanupFiles(deprecatedPath, compabilityPath, builderPath);
		}

		[Fact]
		public void Compatible_Default_Large()
		{
			string deprecatedPath = XbimFileUtility.GetTempGltfFile();
			string compabilityPath = XbimFileUtility.GetTempGltfFile();
			string builderPath = XbimFileUtility.GetTempGltfFile();

			using(var model = IfcStore.Open(this.m_XbimLarge))
			{
				var deprecatedBuilder = new Xbim.GLTF.Deprecated.Builder();
				var compabilityBuilder = new Builder();
				var builder = new XbimGltfBuilder(model);
				builder.MergePrimitives = true;

				deprecatedBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(deprecatedPath);
				compabilityBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs(compabilityPath);
				builder.Build().SaveAs(builderPath);
			}
		
			Assert.True(XbimFileUtility.FilesEqual(deprecatedPath, compabilityPath));
			Assert.True(XbimFileUtility.FilesEqual(deprecatedPath, builderPath));

			XbimFileUtility.CleanupFiles(deprecatedPath, compabilityPath, builderPath);
		}

		[Fact]
		public void Compatible_TypeFilter()
		{
			string deprecatedPath = XbimFileUtility.GetTempGltfFile();
			string compabilityPath = XbimFileUtility.GetTempGltfFile();
			string builderPath = XbimFileUtility.GetTempGltfFile();

			using(var model = IfcStore.Open(this.m_XbimSmall))
			{
				var deprecatedBuilder = new Xbim.GLTF.Deprecated.Builder();
				var compabilityBuilder = new Builder();
				var builder = new XbimGltfBuilder(model);
				builder.MergePrimitives = true;

				var exclude = new List<Type>() { typeof(IIfcWindow) };

				deprecatedBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity, exclude: exclude).SaveAs(deprecatedPath);
				compabilityBuilder.BuildInstancedScene(model, XbimMatrix3D.Identity, exclude: exclude).SaveAs(compabilityPath);
				builder.ExcludedTypes.UnionWith(exclude);
				builder.Build().SaveAs(builderPath);
			}
		
			Assert.True(XbimFileUtility.FilesEqual(deprecatedPath, compabilityPath));
			Assert.True(XbimFileUtility.FilesEqual(deprecatedPath, builderPath));

			XbimFileUtility.CleanupFiles(deprecatedPath, compabilityPath, builderPath);
		}

		public void Dispose()
		{
			XbimFileUtility.CleanupFiles(this.m_XbimSmall, this.m_XbimLarge);
		}
	}
}
