using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Подготовка текстур карты к бинарному контейнеру: перекодирование в <see cref="BinaryTexture"/>,
	/// упаковка шероховатости/металличности/альфы в одну карту и запекание цвета эмиссии.
	/// Готовые ресурсы именуются по SHA-256 содержимого и отдаются через <see cref="Drain"/>.
	/// </summary>
	internal sealed class BinaryTexturePreparation
	{
		private readonly SortedDictionary<string, string> m_files;
		private readonly BinaryTextureEncoding m_encoding;
		private readonly Dictionary<string, string> m_prepared = new(StringComparer.Ordinal);
		private readonly HashSet<string> m_emitted = new(StringComparer.Ordinal);
		private readonly Queue<KeyValuePair<string, byte[]>> m_pending = new();
		private readonly HashSet<string> m_consumed = new(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<string> m_retained = new(StringComparer.OrdinalIgnoreCase);

		public BinaryTexturePreparation(SortedDictionary<string, string> files, BinaryTextureEncoding encoding)
		{
			m_files = files;
			m_encoding = encoding;
		}

		public IEnumerable<KeyValuePair<string, byte[]>> Drain()
		{
			while (m_pending.Count != 0)
			{
				yield return m_pending.Dequeue();
			}
		}

		public bool KeepOriginal(string name)
		{
			return !m_consumed.Contains(name) || m_retained.Contains(name);
		}

		public void PreservePreviews(ModMeta meta)
		{
			IEnumerable<string> names = new[] { meta.icon, meta.largeIcon }.Concat(meta.minimap?.textures ?? Array.Empty<string>());

			foreach (string name in names)
			{
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}

				string normalized = name.Replace('\\', '/');
				m_retained.Add(normalized.Contains('/') ? normalized : "textures/" + name);
			}
		}

		public void Prepare(string document, ModPbrMaterial material)
		{
			material.blendMask = Prepare(document, material.blendMask, linear: true);

			foreach (ModPbrLayer layer in material.layers)
			{
				layer.diffuse = Prepare(document, layer.diffuse, linear: false);
				layer.normal = Prepare(document, layer.normal, linear: true);
				layer.mask = Prepare(document, layer.mask, linear: true);
			}
		}

		public void Prepare(AnimationMeta animation)
		{
			foreach (VertexAnimationAsset asset in animation.assets)
			{
				foreach (VertexAnimationSurface surface in asset.surfaces)
				{
					surface.diffuseBytes = Bake(surface.diffusePng, surface.diffuseBytes, linear: false);
					surface.normalBytes = Bake(surface.normalPng, surface.normalBytes, linear: true);
					surface.maskBytes = Bake(surface.maskPng, surface.maskBytes, linear: true);
					surface.diffusePng = null;
					surface.normalPng = null;
					surface.maskPng = null;
				}
			}

			// Атласы анимации позиций/нормалей/тангенсов остаются точными RGBAHalf.
		}

		public void Prepare(string document, BinaryMaterialLibrary library)
		{
			foreach (BinaryMaterial material in library.materials)
			{
				if (!string.IsNullOrEmpty(material.layeredResource))
				{
					// Layered PBR главнее: его Wavefront-запасной вариант этим клиентом не используется.
					foreach (BinaryMaterialProperty property in material.properties.Where(property => property.type == BinaryMaterialPropertyType.Texture))
					{
						m_consumed.Add(Resolve(document, property.texture));
					}

					material.properties = Array.Empty<BinaryMaterialProperty>();
					continue;
				}

				material.properties = PrepareProperties(document, material).ToArray();
			}
		}

		private List<BinaryMaterialProperty> PrepareProperties(string document, BinaryMaterial material)
		{
			List<BinaryMaterialProperty> properties = material.properties.ToList();

			if (material.emission)
			{
				PrepareEmission(document, properties);
			}

			BinaryMaterialProperty[] packed = properties.Where(IsPackedSurfaceProperty).ToArray();

			if (packed.Length != 0)
			{
				string name = Emit(document, PackSurface(document, packed));
				properties.RemoveAll(property => packed.Contains(property));
				properties.Add(new BinaryMaterialProperty { semantic = BinaryTexture.PackedSemantic, type = BinaryMaterialPropertyType.Texture, texture = name });
			}

			IEnumerable<BinaryMaterialProperty> textures = properties.Where(property => property.type == BinaryMaterialPropertyType.Texture
				&& property.semantic != BinaryTexture.PackedSemantic
				&& property.semantic != "map_Ke");

			foreach (BinaryMaterialProperty property in textures)
			{
				property.texture = Prepare(document, property.texture, linear: property.semantic != "map_Kd");
			}

			return properties;
		}

		private static bool IsPackedSurfaceProperty(BinaryMaterialProperty property)
		{
			return property.semantic == "map_Pr" || property.semantic == "map_Pm" || property.semantic == "map_d" || property.semantic == "Pr" || property.semantic == "Pm";
		}

		private static string Resolve(string document, string name)
		{
			string path = ((Path.GetDirectoryName(document) ?? string.Empty) + "/" + Path.GetFileName(name)).TrimStart('/').Replace('\\', '/');
			return BinaryModArchive.Normalize(path);
		}

		private byte[] Bake(string text, byte[] bytes, bool linear)
		{
			if (bytes == null && string.IsNullOrEmpty(text))
			{
				return null;
			}

			return BinaryTextureBaker.Encode(bytes ?? Convert.FromBase64String(text), linear, m_encoding);
		}

		private string Emit(string document, byte[] bytes)
		{
			using SHA256 sha = SHA256.Create();
			string name = "cx_" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() + BinaryTexture.Extension;
			string path = Resolve(document, name);

			if (m_files.ContainsKey(path))
			{
				throw new InvalidDataException("Reserved prepared texture path: " + path);
			}

			if (m_emitted.Add(path))
			{
				m_pending.Enqueue(new KeyValuePair<string, byte[]>(path, bytes));
			}

			return name;
		}

		private string Prepare(string document, string name, bool linear)
		{
			if (string.IsNullOrEmpty(name))
			{
				return name;
			}

			string path = Resolve(document, name);

			if (!m_files.TryGetValue(path, out string file))
			{
				throw new InvalidDataException("Missing map texture: " + path);
			}

			m_consumed.Add(path);
			string key = path + (linear ? "|linear" : "|srgb");

			if (!m_prepared.TryGetValue(key, out string result))
			{
				result = Emit(document, BinaryTextureBaker.Encode(File.ReadAllBytes(file), linear, m_encoding));
				m_prepared.Add(key, result);
			}

			return result;
		}

		private void PrepareEmission(string document, List<BinaryMaterialProperty> properties)
		{
			BinaryMaterialProperty colorProperty = properties.LastOrDefault(property => property.semantic == "Ke");
			Vector3 color = colorProperty?.vector ?? Vector3.one;
			float intensity = Mathf.Max(color.x, Mathf.Max(color.y, color.z));

			if (intensity > 1)
			{
				color /= intensity;
			}

			BinaryMaterialProperty map = properties.LastOrDefault(property => property.semantic == "map_Ke");
			var source = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false, linear: false);

			try
			{
				Color gamma = new Color(color.x, color.y, color.z).gamma;

				if (map != null)
				{
					LoadEmissionMap(document, map, source, color, gamma);
				}
				else
				{
					source.SetPixels(Enumerable.Repeat(gamma, 16).ToArray());
					source.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				}

				properties.RemoveAll(property => property.semantic == "map_Ke" || property.semantic == "Ke");
				string texture = Emit(document, BinaryTextureBaker.Encode(source, linear: false, m_encoding));
				properties.Add(new BinaryMaterialProperty { semantic = "map_Ke", type = BinaryMaterialPropertyType.Texture, texture = texture });

				// Цвет запечён в текстуру, а HDR-интенсивность остаётся в параметрах поверхности клиента.
				properties.Add(new BinaryMaterialProperty { semantic = "Ke", type = BinaryMaterialPropertyType.Vector3, vector = Vector3.one * Mathf.Max(1, intensity) });
			}
			finally
			{
				Object.DestroyImmediate(source);
			}
		}

		private void LoadEmissionMap(string document, BinaryMaterialProperty map, Texture2D target, Vector3 color, Color gamma)
		{
			string path = Resolve(document, map.texture);

			if (!m_files.TryGetValue(path, out string file))
			{
				throw new InvalidDataException("Missing emission texture: " + path);
			}

			m_consumed.Add(path);

			if (!target.LoadImage(File.ReadAllBytes(file), markNonReadable: false))
			{
				throw new InvalidDataException("Invalid emission texture: " + path);
			}

			bool white = Mathf.Abs(color.x - 1) < .001f && Mathf.Abs(color.y - 1) < .001f && Mathf.Abs(color.z - 1) < .001f;

			if (white)
			{
				return;
			}

			Color32[] pixels = target.GetPixels32();

			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i].r = (byte)Mathf.Min(255, pixels[i].r * gamma.r);
				pixels[i].g = (byte)Mathf.Min(255, pixels[i].g * gamma.g);
				pixels[i].b = (byte)Mathf.Min(255, pixels[i].b * gamma.b);
			}

			target.SetPixels32(pixels);
			target.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		}

		private byte[] PackSurface(string document, BinaryMaterialProperty[] properties)
		{
			var sources = new List<Texture2D>();
			Material material = null;
			RenderTexture target = null;
			RenderTexture previous = RenderTexture.active;
			bool previousSrgb = GL.sRGBWrite;

			try
			{
				Texture2D roughness = ReadSurfaceTexture(document, properties, "map_Pr", sources);
				Texture2D metalness = ReadSurfaceTexture(document, properties, "map_Pm", sources);
				Texture2D alpha = ReadSurfaceTexture(document, properties, "map_d", sources);
				Texture2D reference = roughness != null ? roughness : metalness != null ? metalness : OrWhite(alpha);
				Shader shader = Shader.Find("Hidden/CarX Modding/PreparePackedMap");

				if (shader == null || !shader.isSupported)
				{
					throw new InvalidDataException("SDK PBR texture preparation shader is unavailable.");
				}

				material = new Material(shader);
				material.SetTexture("_RoughnessTex", OrWhite(roughness));
				material.SetTexture("_MetalnessTex", OrWhite(metalness));
				material.SetTexture("_AlphaTex", OrWhite(alpha));
				material.SetFloat("_RoughnessScale", properties.LastOrDefault(property => property.semantic == "Pr")?.scalar ?? 1);
				material.SetFloat("_MetalnessScale", properties.LastOrDefault(property => property.semantic == "Pm")?.scalar ?? (metalness != null ? 1 : 0));

				target = RenderTexture.GetTemporary(reference.width, reference.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
				GL.sRGBWrite = false;
				Graphics.Blit(source: null, target, material, pass: 0);
				RenderTexture.active = target;

				var packed = new Texture2D(reference.width, reference.height, TextureFormat.RGBA32, mipChain: false, linear: true);
				sources.Add(packed);
				packed.ReadPixels(new Rect(0, 0, reference.width, reference.height), 0, 0, recalculateMipMaps: false);
				packed.Apply(updateMipmaps: false, makeNoLongerReadable: false);

				return BinaryTextureBaker.Encode(packed, linear: true, m_encoding);
			}
			finally
			{
				GL.sRGBWrite = previousSrgb;
				RenderTexture.active = previous;

				if (target != null)
				{
					RenderTexture.ReleaseTemporary(target);
				}

				if (material != null)
				{
					Object.DestroyImmediate(material);
				}

				foreach (Texture2D source in sources)
				{
					Object.DestroyImmediate(source);
				}
			}
		}

		private static Texture2D OrWhite(Texture2D texture)
		{
			return texture != null ? texture : Texture2D.whiteTexture;
		}

		private Texture2D ReadSurfaceTexture(string document, BinaryMaterialProperty[] properties, string semantic, List<Texture2D> sources)
		{
			BinaryMaterialProperty property = properties.LastOrDefault(candidate => candidate.semantic == semantic);

			if (property == null)
			{
				return null;
			}

			string path = Resolve(document, property.texture);

			if (!m_files.TryGetValue(path, out string file))
			{
				throw new InvalidDataException("Missing PBR texture: " + path);
			}

			m_consumed.Add(path);
			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: true);
			sources.Add(texture);

			if (!texture.LoadImage(File.ReadAllBytes(file), markNonReadable: false))
			{
				throw new InvalidDataException("Invalid PBR texture: " + path);
			}

			return texture;
		}
	}
}
