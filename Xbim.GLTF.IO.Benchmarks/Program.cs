using System;
using System.IO;
using BenchmarkDotNet.Running;

namespace Xbim.GLTF.IO.Benchmarks
{
	class Program
	{
		static void Main(string[] args)
		{
			var summary = BenchmarkRunner.Run<BuilderBenchmark>();
		}
	}
}
