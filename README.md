# XbimGltf

## Requirements
[Microsoft Visual C++ Redistributable 2015, 2017, 2019](https://support.microsoft.com/de-de/help/2977003/the-latest-supported-visual-c-downloads)

[Microsoft .NET Framework 4.7.2](https://support.microsoft.com/de-de/help/4054531/microsoft-net-framework-4-7-2-web-installer-for-windows)

## Quick Start
Before you can use the GLTF builder you need to convert
the original IFC file to a meshed xBIM file:
```CSharp
IfcStore.ModelProviderFactory.UseHeuristicModelProvider();

using(var model = IfcStore.Open("Files/House.ifc"))
{
	var context = new Xbim3DModelContext(model);
	context.CreateContext(null, false);
	model.SaveAs("Files/House.xBIM");
}
```

### Create a GLTF file with the new builder
```CSharp
using(var model = IfcStore.Open("Files/House.xBIM"))
{
	var builder = new XbimGltfBuilder(model);
	builder.Build().SaveAs("Files/House.gltf");
}

```

### Create a GLTF file with the compability API
The compability API matches the original builder API
but uses the new builder under the hood.
Note that the original builder and the compability API is marked as `Obsolete`.
```CSharp
using(var model = IfcStore.Open("Files/House.xBIM"))
{
	var builder = new Builder();
	builder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs("Files/House.gltf");
}
```

## Tests
The ensure that the rework works as expected the ``Xbim.GLTF.IO.Tests` directory
contains compability tests of all implementations.
The compability is tested by comparing the output files with the output file of original
implementation.

The tests are using Xunit and can be run with:
```
dotnet test
```

## Benchmarks

Compared to the original implementation the reworks performs up to 35% better.
Also the pressure on the garbage collector is reduced.
The `Xbim.GLTF.IO.Benchmarks` directory contains the benchmarks of
the original implementation, the rework and the compability API.

Run it with
```
dotnet run -c Release
```

The result should look like this:
|                Method |     Mean |   Error |   StdDev |    Median |
|---------------------- |---------:|--------:|---------:|----------:|
|               Convert | 100.7 ms | 5.81 ms | 17.13 ms |  97.26 ms |
| Convert_Compatibility | 100.7 ms | 4.74 ms | 13.97 ms |  97.76 ms |
|    Convert_Deprecated | 156.5 ms | 6.01 ms | 17.72 ms | 151.38 ms |

## Authors

* @CBenghi - author of the original implementation

* @dn9090 - author of the rework
