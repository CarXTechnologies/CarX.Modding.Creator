using System.Collections.Generic;
using CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Draws the miniature from above into a minimap image: the same rectangle as the minimap bounds, the colors the game gives
	// the 3D miniature, a transparent background. A software rasterizer: no camera, shader or render pipeline is involved
	public static class MiniatureTextureRenderer
	{
		public const int MaxSize = 2048;
		private const int Supersampling = 2;

		// The miniature palette is linear, the image colors are gamma
		private static readonly Color s_groundColor = MiniaturePalette.GroundColor.gamma;
		private static readonly Color s_terrainColor = MiniaturePalette.TerrainColor.gamma;
		// Seen from above, a road is never under the terrain and an object is never under a road, whatever the simplified heights
		// say: the layers are stacked this far apart, meters
		private const float LayerLift = 100_000f;
		// Seams between the simplified layers narrower than twice this are painted over, buffer pixels
		private const int SeamRadius = 8;

		/// <param name="boundsCenter">Minimap Bounds Center: world x and z</param>
		/// <param name="boundsSize">Minimap Bounds Size: world x and z</param>
		public static Texture2D Render(MiniatureMeta miniature, Vector2 boundsCenter, Vector2 boundsSize)
		{
			float longSide = Mathf.Max(boundsSize.x, boundsSize.y);
			int width = Mathf.Max(Mathf.RoundToInt(MaxSize * boundsSize.x / longSide), 1);
			int height = Mathf.Max(Mathf.RoundToInt(MaxSize * boundsSize.y / longSide), 1);
			int bufferWidth = width * Supersampling;
			int bufferHeight = height * Supersampling;

			var colors = new Color32[bufferWidth * bufferHeight];
			var heights = new float[bufferWidth * bufferHeight];

			for (int i = 0; i < heights.Length; i++)
			{
				heights[i] = float.NegativeInfinity;
			}

			Vector2 min = boundsCenter - boundsSize * 0.5f;
			var scale = new Vector2(bufferWidth / boundsSize.x, bufferHeight / boundsSize.y);

			foreach (MiniatureLayer layer in miniature.layers)
			{
				if (layer.vertices == null || layer.triangles == null)
				{
					continue;
				}

				Color32[] vertexColors = GetVertexColors(layer, miniature.baseHeight);
				float lift = layer.kind switch
				{
					MiniatureLayerKind.Terrain => -LayerLift,
					MiniatureLayerKind.Objects => LayerLift,
					_ => 0f,
				};

				for (int i = 0; i + 2 < layer.triangles.Length; i += 3)
				{
					Vector3 a = layer.vertices[layer.triangles[i]];
					Vector3 b = layer.vertices[layer.triangles[i + 1]];
					Vector3 c = layer.vertices[layer.triangles[i + 2]];

					DrawTriangle(
						new Vector3((a.x - min.x) * scale.x, (a.z - min.y) * scale.y, a.y + lift),
						new Vector3((b.x - min.x) * scale.x, (b.z - min.y) * scale.y, b.y + lift),
						new Vector3((c.x - min.x) * scale.x, (c.z - min.y) * scale.y, c.y + lift),
						vertexColors[layer.triangles[i]], colors, heights, bufferWidth, bufferHeight);
				}
			}

			bool hasTerrain = miniature.layers.Exists(layer => layer.kind == MiniatureLayerKind.Terrain && layer.triangles is { Length: > 0 });
			Color32 fill = hasTerrain ? s_terrainColor : s_groundColor;
			CloseSeams(colors, bufferWidth, bufferHeight, fill);
			FillHoles(colors, bufferWidth, bufferHeight, fill);

			var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
			texture.SetPixels32(Downsample(colors, bufferWidth, width, height));
			texture.Apply();

			return texture;
		}

		// One color per vertex: flat per layer, per connected piece by the height of its top for objects
		private static Color32[] GetVertexColors(MiniatureLayer layer, float baseHeight)
		{
			var result = new Color32[layer.vertices.Length];

			if (layer.kind != MiniatureLayerKind.Objects)
			{
				Color32 color = layer.kind == MiniatureLayerKind.Terrain ? s_terrainColor : s_groundColor;

				for (int i = 0; i < result.Length; i++)
				{
					result[i] = color;
				}

				return result;
			}

			Color[] objectColors = MiniaturePalette.GetObjectColors(layer, baseHeight);

			for (int i = 0; i < result.Length; i++)
			{
				result[i] = objectColors[i].gamma;
			}

			return result;
		}

		// x and y are buffer pixels, z is the height in the layer stack: the higher surface wins
		private static void DrawTriangle(Vector3 a, Vector3 b, Vector3 c, Color32 color, Color32[] colors, float[] heights, int width, int height)
		{
			float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

			if (Mathf.Abs(area) < 1e-6f)
			{
				return;
			}

			int minX = Mathf.Max(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0);
			int maxX = Mathf.Min(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), width - 1);
			int minY = Mathf.Max(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0);
			int maxY = Mathf.Min(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), height - 1);
			float inverseArea = 1f / area;

			for (int y = minY; y <= maxY; y++)
			{
				float py = y + 0.5f;

				for (int x = minX; x <= maxX; x++)
				{
					float px = x + 0.5f;
					float wa = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) * inverseArea;
					float wb = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) * inverseArea;
					float wc = 1f - wa - wb;

					if (wa < 0f || wb < 0f || wc < 0f)
					{
						continue;
					}

					float z = wa * a.z + wb * b.z + wc * c.z;
					int index = y * width + x;

					if (z > heights[index])
					{
						heights[index] = z;
						colors[index] = color;
					}
				}
			}
		}

		// Morphological closing of the drawn area: the layers are simplified separately, so thin transparent seams are left between
		// a road and the terrain around it. They take the terrain color, the outline of the map stays where it is
		private static void CloseSeams(Color32[] colors, int width, int height, Color32 fill)
		{
			var mask = new bool[colors.Length];

			for (int i = 0; i < mask.Length; i++)
			{
				mask[i] = colors[i].a != 0;
			}

			bool[] closed = Spread(mask, width, height, isSpreadValue: true);
			closed = Spread(closed, width, height, isSpreadValue: false);

			for (int i = 0; i < colors.Length; i++)
			{
				if (closed[i] && !mask[i])
				{
					colors[i] = fill;
				}
			}
		}

		// Transparent areas the drawn map encloses are holes in it (a lake, the footprint of a hidden building): they are painted
		// over as well. What is left transparent is reachable from the image border
		private static void FillHoles(Color32[] colors, int width, int height, Color32 fill)
		{
			var isOutside = new bool[colors.Length];
			var queue = new Queue<int>();

			void Visit(int index)
			{
				if (colors[index].a == 0 && !isOutside[index])
				{
					isOutside[index] = true;
					queue.Enqueue(index);
				}
			}

			for (int x = 0; x < width; x++)
			{
				Visit(x);
				Visit((height - 1) * width + x);
			}

			for (int y = 0; y < height; y++)
			{
				Visit(y * width);
				Visit(y * width + width - 1);
			}

			while (queue.Count > 0)
			{
				int index = queue.Dequeue();
				int x = index % width;

				if (x > 0)
				{
					Visit(index - 1);
				}

				if (x < width - 1)
				{
					Visit(index + 1);
				}

				if (index >= width)
				{
					Visit(index - width);
				}

				if (index < colors.Length - width)
				{
					Visit(index + width);
				}
			}

			for (int i = 0; i < colors.Length; i++)
			{
				if (colors[i].a == 0 && !isOutside[i])
				{
					colors[i] = fill;
				}
			}
		}

		// Dilation (spreads true) or erosion (spreads false) by SeamRadius, rows then columns. Outside the image is the opposite
		// of the spread value, so the image border changes nothing
		private static bool[] Spread(bool[] source, int width, int height, bool isSpreadValue)
		{
			bool[] rows = SpreadLines(source, lineLength: width, lineStep: 1, linesStep: width, isSpreadValue);

			return SpreadLines(rows, lineLength: height, lineStep: width, linesStep: 1, isSpreadValue);
		}

		private static bool[] SpreadLines(bool[] source, int lineLength, int lineStep, int linesStep, bool isSpreadValue)
		{
			var result = new bool[source.Length];
			int linesCount = source.Length / lineLength;

			for (int line = 0; line < linesCount; line++)
			{
				int start = line * linesStep;
				// Position of the last cell holding the spread value, scanned forward and then backward
				int last = int.MinValue / 2;

				for (int i = 0; i < lineLength; i++)
				{
					int index = start + i * lineStep;

					if (source[index] == isSpreadValue)
					{
						last = i;
					}

					result[index] = i - last <= SeamRadius ? isSpreadValue : !isSpreadValue;
				}

				last = int.MaxValue / 2;

				for (int i = lineLength - 1; i >= 0; i--)
				{
					int index = start + i * lineStep;

					if (source[index] == isSpreadValue)
					{
						last = i;
					}

					if (last - i <= SeamRadius)
					{
						result[index] = isSpreadValue;
					}
				}
			}

			return result;
		}

		// Box filter with premultiplied alpha: the edges against the transparent background stay clean
		private static Color32[] Downsample(Color32[] source, int sourceWidth, int width, int height)
		{
			var result = new Color32[width * height];
			const int samples = Supersampling * Supersampling;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int r = 0;
					int g = 0;
					int b = 0;
					int a = 0;

					for (int sy = 0; sy < Supersampling; sy++)
					{
						for (int sx = 0; sx < Supersampling; sx++)
						{
							Color32 sample = source[(y * Supersampling + sy) * sourceWidth + x * Supersampling + sx];
							r += sample.r * sample.a;
							g += sample.g * sample.a;
							b += sample.b * sample.a;
							a += sample.a;
						}
					}

					result[y * width + x] = a == 0
						? new Color32(0, 0, 0, 0)
						: new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / samples));
				}
			}

			return result;
		}
	}
}
