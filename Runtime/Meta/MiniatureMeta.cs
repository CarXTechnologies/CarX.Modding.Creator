using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Kind of a world map miniature layer: the game drapes the minimap image over the layers and shades the walls
	public enum MiniatureLayerKind
	{
		Track,
		Kerb,
		Ground,
		Walls,
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
		// Height of the lowest track point: the game puts it at the miniature base
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
