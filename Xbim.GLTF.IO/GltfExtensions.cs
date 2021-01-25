using System;
using Gltf = glTFLoader.Schema;

namespace Xbim.GLTF
{
	public static class GltfExtensions
	{
		public static void SaveAs(this Gltf.Gltf gltf, string filePath) =>
			glTFLoader.Interface.SaveModel(gltf, filePath);
	}
}