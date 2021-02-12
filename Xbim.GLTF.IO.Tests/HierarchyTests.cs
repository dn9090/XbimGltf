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
	public class HierarchyTests : IDisposable
	{
		private string m_XbimPath;

		public HierarchyTests()
		{
			this.m_XbimPath = XbimFileUtility.CreateXbimFile("Files/231110AC11-FZK-Haus-IFC.ifc");
		}

		[Fact]
		public void CreatesHierarchy()
		{
			using(var model = IfcStore.Open(this.m_XbimPath))
			{
				var builder = new XbimGltfBuilder(model);
				builder.MergePrimitives = false;

				var gltf = builder.Build();
				Assert.True(gltf.Nodes.Any(node => node.Children != null && node.Children.Length > 1));
			}
		}

		public void Dispose()
		{
			XbimFileUtility.CleanupFiles(this.m_XbimPath);
		}
	}
}
