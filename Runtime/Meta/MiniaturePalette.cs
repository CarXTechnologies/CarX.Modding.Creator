using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Linear colors of the world map miniature, matched by eye to the stock track miniatures: terrain a little lighter than
	// the game backdrop, roads and pads grey, objects lighter grey by the height step of their top. The game shades
	// the 3D miniature with them, the uploader draws the minimap image of a map with the same colors
	public static class MiniaturePalette
	{
		public static readonly Color TerrainColor = new(0.06f, 0.066f, 0.078f, 1f);
		public static readonly Color GroundColor = new(0.19f, 0.21f, 0.225f, 1f);
		private static readonly Color s_objectColor = new(0.25f, 0.25f, 0.25f, 1f);
		private const float ObjectStepLightness = 0.04f;
		private const int ObjectMaxSteps = 5;
		// Height of one color step above the miniature base, meters
		private const float HeightStep = 4f;

		public static Color GetObjectColor(float heightAboveBase)
		{
			int step = Mathf.Clamp(Mathf.FloorToInt(heightAboveBase / HeightStep), 0, ObjectMaxSteps);
			float lightness = step * ObjectStepLightness;

			return new Color(s_objectColor.r + lightness, s_objectColor.g + lightness, s_objectColor.b + lightness, 1f);
		}

		// One color per vertex of an objects layer: each connected piece (a box, or a connected part of the author's own mesh)
		// gets the color of the height of its top
		public static Color[] GetObjectColors(MiniatureLayer layer, float baseHeight)
		{
			Vector3[] vertices = layer.vertices;
			int[] triangles = layer.triangles;
			var parents = new int[vertices.Length];

			for (int i = 0; i < parents.Length; i++)
			{
				parents[i] = i;
			}

			for (int i = 0; i + 2 < triangles.Length; i += 3)
			{
				Union(parents, triangles[i], triangles[i + 1]);
				Union(parents, triangles[i], triangles[i + 2]);
			}

			var tops = new Dictionary<int, float>();

			for (int i = 0; i < vertices.Length; i++)
			{
				int root = Find(parents, i);
				float y = vertices[i].y;
				tops[root] = tops.TryGetValue(root, out float top) ? Mathf.Max(top, y) : y;
			}

			var colors = new Color[vertices.Length];

			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = GetObjectColor(tops[Find(parents, i)] - baseHeight);
			}

			return colors;
		}

		private static int Find(int[] parents, int index)
		{
			while (parents[index] != index)
			{
				parents[index] = parents[parents[index]];
				index = parents[index];
			}

			return index;
		}

		private static void Union(int[] parents, int a, int b)
		{
			parents[Find(parents, a)] = Find(parents, b);
		}
	}
}
