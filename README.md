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
Please notice that new builder uses other default configurations than the old one.

### Create a GLTF file with the compability API
The compatibility API matches the original builder API
but uses the new builder under the hood.
Note that the original builder and the compatibility API is marked as `Obsolete`.
```CSharp
using(var model = IfcStore.Open("Files/House.xBIM"))
{
	var builder = new Builder();
	builder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs("Files/House.gltf");
}
```

### Merge Primitives
There are two different options to create the nodes from the IFC file.
The first one creates a hierarchy with separate child nodes for each mesh primitive:
```CSharp
using(var model = IfcStore.Open("Files/House.xBIM"))
{
	var builder = new Builder();
	builder.MergePrimitives = false; // This is the default value.
	builder.BuildInstancedScene(model, XbimMatrix3D.Identity).SaveAs("Files/House.gltf");
}
```
The resulting hierarchy should look similiar to this:
```
Root
| - Wall #10
| | - Shape #1001
| - Door #20
| | - Shape #2001
| - Window #30
| | - Shape #3001
| | - Shape #3002
| - Wall #40
| | - Shape #4001
```
If `MergePrimitives` is set to `true` the hierarchy will be flat:
```
Root
| - Wall #10
| - Door #20
| - Window #30
| - Wall #40
```
In the flat hierarchy a mesh can consist of multiple primitives.

## Tests
The ensure that the rework works as expected the ``Xbim.GLTF.IO.Tests` directory
contains compatibility tests of all implementations.
The compatibility is tested by comparing the output files with the output file of original
implementation.

The tests are using Xunit and can be run with:
```
dotnet test
```

## Benchmarks

Compared to the original implementation the reworks performs up to 35% better.
Also the pressure on the garbage collector is reduced.
The `Xbim.GLTF.IO.Benchmarks` directory contains the benchmarks of
the original implementation, the rework and the compatibility API.

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
