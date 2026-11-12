using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Average albedo of a material: its color textures (several layers of a splat shader too) times its tint.
	// Tells asphalt and concrete (grey) from terrain (grass, soil, sand) for the world map miniature
	public sealed class MaterialColorSampler : IDisposable
	{
		private const int SampleSize = 8;
		// Grey enough to be asphalt or concrete, and neither black nor white
		private const float RoadMaxSaturation = 0.2f;
		private const float RoadMinValue = 0.12f;
		private const float RoadMaxValue = 0.9f;

		private static readonly string[] s_colorTextureHints = { "basecolor", "albedo", "diffuse", "maintex", "basemap" };
		private static readonly string[] s_tintNames = { "_BaseColor", "_Color" };

		private readonly Dictionary<Material, bool> m_isRoadByMaterial = new();
		private readonly Dictionary<Texture, Color> m_averageByTexture = new();
		private RenderTexture m_target;
		private Texture2D m_readback;

		public bool IsRoad(Material material)
		{
			if (material == null)
			{
				return false;
			}

			if (m_isRoadByMaterial.TryGetValue(material, out bool isRoad))
			{
				return isRoad;
			}

			Color color = GetAverageColor(material);
			Color.RGBToHSV(color, out _, out float saturation, out float value);
			isRoad = saturation <= RoadMaxSaturation && value >= RoadMinValue && value <= RoadMaxValue;
			m_isRoadByMaterial.Add(material, isRoad);

			return isRoad;
		}

		public Color GetAverageColor(Material material)
		{
			var sum = new Color(0f, 0f, 0f, 0f);
			int count = 0;

			foreach (string property in material.GetTexturePropertyNames())
			{
				string name = property.ToLowerInvariant();

				if (!Array.Exists(s_colorTextureHints, hint => name.Contains(hint)) || material.GetTexture(property) is not { } texture)
				{
					continue;
				}

				sum += GetAverageColor(texture);
				count++;
			}

			Color color = count > 0 ? sum / count : Color.white;

			foreach (string tint in s_tintNames)
			{
				// A black tint is an unused property (some map shaders keep _Color at zero)
				if (material.HasColor(tint) && material.GetColor(tint).maxColorComponent > 0.01f)
				{
					color *= material.GetColor(tint);
					break;
				}
			}

			return color;
		}

		public void Dispose()
		{
			if (m_target != null)
			{
				m_target.Release();
				UnityEngine.Object.DestroyImmediate(m_target);
				m_target = null;
			}

			if (m_readback != null)
			{
				UnityEngine.Object.DestroyImmediate(m_readback);
				m_readback = null;
			}
		}

		// The texture scaled down on the GPU: works for compressed and not readable textures
		private Color GetAverageColor(Texture texture)
		{
			if (m_averageByTexture.TryGetValue(texture, out Color average))
			{
				return average;
			}

			m_target ??= new RenderTexture(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			m_readback ??= new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, mipChain: false);

			RenderTexture previous = RenderTexture.active;
			Graphics.Blit(texture, m_target);
			RenderTexture.active = m_target;
			m_readback.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0);
			RenderTexture.active = previous;

			average = new Color(0f, 0f, 0f, 0f);

			foreach (Color pixel in m_readback.GetPixels())
			{
				average += pixel;
			}

			average /= SampleSize * SampleSize;
			m_averageByTexture.Add(texture, average);

			return average;
		}
	}
}
