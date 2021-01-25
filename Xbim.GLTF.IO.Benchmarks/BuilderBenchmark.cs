using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.GLTF;
using Xbim.ModelGeometry.Scene;

namespace Xbim.GLTF.IO.Benchmarks
{
	[SimpleJob(RunStrategy.ColdStart, launchCount: 1)]
	public class BuilderBenchmark
	{
		private string m_XbimPath;

		private IModel m_Model;

		[GlobalSetup]
		public void Setup()
		{
			string tempPath = Path.GetTempFileName();
			this.m_XbimPath = Path.ChangeExtension(tempPath, ".xBIM");

			File.Move(tempPath, this.m_XbimPath);

			IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
			using(var model = IfcStore.Open("Files/231110AC11-FZK-Haus-IFC.ifc"))
			{
				var context = new Xbim3DModelContext(model);
				context.CreateContext(null, false);
				model.SaveAs(this.m_XbimPath);
			}

			this.m_Model = IfcStore.Open(this.m_XbimPath);
		}

		[GlobalCleanup]
		public void Cleanup()
		{
			try
			{
				File.Delete(Path.ChangeExtension(this.m_XbimPath, ".jfm"));
				File.Delete(this.m_XbimPath);
			} catch {}
		}

		[Benchmark]
		public void Convert()
		{
			var builder = new XbimGltfBuilder(this.m_Model);
			builder.MergePrimitives = true;
			builder.Build();
		}

		[Benchmark]
		public void Convert_Compatibility()
		{
			var builder = new Xbim.GLTF.Builder();
			builder.BuildInstancedScene(this.m_Model, XbimMatrix3D.Identity);
		}

		[Benchmark]
		public void Convert_Deprecated()
		{
			var builder = new Xbim.GLTF.Deprecated.Builder();
			builder.BuildInstancedScene(this.m_Model, XbimMatrix3D.Identity);
		}
	}
}