using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Kind of a world map miniature layer. The game shades both in grey by height
	public enum MiniatureLayerKind
	{
		// Roads, pads and other large flat drivable surfaces
		Ground,
		// Boxes in place of the map objects (buildings, barriers, stands...); the game colors each connected piece by its top
		Objects,
		// Coarse terrain under the roads, drawn as a dark backdrop
		Terrain,
	}

	[Serializable]
	public class MiniatureLayer
	{
		public MiniatureLayerKind kind;
		// World space positions of the exported map, the same space as the minimap bounds (x, z)
		public Vector3[] vertices;
		public int[] triangles;
	}

	// Simplified map geometry for the world map miniature, generated at export from the map meshes
	[Serializable]
	public class MiniatureMeta : IModResources, IModResourcesVersion
	{
		public string Id
		{
			get => id;
			set => id = value;
		}

		public string Version
		{
			get => version;
			set => version = value;
		}

		public string id;
		public string version;
		// Height of the lowest ground point: the game puts it at the miniature base
		public float baseHeight;
		public List<MiniatureLayer> layers = new();

		public MiniatureMeta(string id, string version, float baseHeight, List<MiniatureLayer> layers)
		{
			this.id = id;
			this.version = version;
			this.baseHeight = baseHeight;
			this.layers = layers;
		}

		// For BinaryModData: fields missing from the document get their initializer values, not default.
		private MiniatureMeta()
		{
		}
	}
}
