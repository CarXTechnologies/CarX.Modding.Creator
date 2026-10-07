using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Однократный импорт staging-представления материалов (.mtl) в типизированную <see cref="BinaryMaterialLibrary"/> при сборке.
	/// Клиент получает готовые значения и не запускает парсер Wavefront для контейнера.
	/// </summary>
	internal static class MtlBinaryMaterialReader
	{
		public static BinaryMaterialLibrary Read(string[] lines)
		{
			var materials = new List<BinaryMaterial>();
			var properties = new List<BinaryMaterialProperty>();
			BinaryMaterial material = null;

			foreach (string raw in lines)
			{
				string line = raw.Trim();

				if (line.Length == 0 || line.StartsWith("#"))
				{
					continue;
				}

				int space = line.IndexOf(' ');
				string token = space < 0 ? line : line.Substring(0, space);
				string value = space < 0 ? string.Empty : line.Substring(space + 1).Trim();

				if (token == "newmtl")
				{
					if (material != null)
					{
						material.properties = properties.ToArray();
					}

					properties.Clear();
					material = new BinaryMaterial { name = value };
					materials.Add(material);
					continue;
				}

				if (material == null)
				{
					throw new InvalidDataException("Material property without a material.");
				}

				ReadProperty(material, properties, token, value);
			}

			if (material != null)
			{
				material.properties = properties.ToArray();
			}

			return new BinaryMaterialLibrary { materials = materials.ToArray() };
		}

		private static void ReadProperty(BinaryMaterial material, List<BinaryMaterialProperty> properties, string token, string value)
		{
			switch (token)
			{
				case "cx_pbr":
					material.layeredResource = value;
					break;
				case "illum":
					material.illumination = int.Parse(value, CultureInfo.InvariantCulture);
					break;
				case "ds":
					material.doubleSided = ParseFloat(value) > .5f;
					material.flipNormals = ParseFloat(value) < 1.5f;
					break;
				case "d":
				case "Tr":
					material.alpha = token == "Tr" ? 1 - ParseFloat(value) : ParseFloat(value);
					properties.Add(new BinaryMaterialProperty { semantic = "d", type = BinaryMaterialPropertyType.Float, scalar = material.alpha });
					break;
				case "Ka":
				case "Kd":
				case "Ks":
				case "Ke":
					ReadColor(material, properties, token, value);
					break;
				case "Ns":
				case "Pr":
				case "Pm":
				case "Ni":
					properties.Add(new BinaryMaterialProperty { semantic = token, type = BinaryMaterialPropertyType.Float, scalar = ParseFloat(value) });
					break;
				default:
					ReadTexture(material, properties, token, value);
					break;
			}
		}

		private static void ReadColor(BinaryMaterial material, List<BinaryMaterialProperty> properties, string token, string value)
		{
			string[] xyz = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var vector = new Vector3(ParseFloat(xyz[0]), ParseFloat(xyz[1]), ParseFloat(xyz[2]));
			properties.Add(new BinaryMaterialProperty { semantic = token, type = BinaryMaterialPropertyType.Vector3, vector = vector });

			if (token == "Ke" && (vector.x > .0001f || vector.y > .0001f || vector.z > .0001f))
			{
				material.emission = true;
			}
		}

		private static void ReadTexture(BinaryMaterial material, List<BinaryMaterialProperty> properties, string token, string value)
		{
			if (!token.StartsWith("map_") && token != "bump" && token != "norm")
			{
				throw new InvalidDataException("Unsupported material semantic: " + token);
			}

			string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			int start = ReadTextureOptions(parts, out Vector4 uv);

			if (start >= parts.Length)
			{
				throw new InvalidDataException("Texture reference is missing.");
			}

			properties.Add(new BinaryMaterialProperty
			{
				semantic = token == "map_Bump" ? "map_bump" : token,
				type = BinaryMaterialPropertyType.Texture,
				texture = string.Join(" ", parts, start, parts.Length - start),
				uv = uv
			});

			if (token == "map_Kd")
			{
				material.uv = uv;
			}

			if (token == "map_d")
			{
				material.alphaTexture = true;
			}

			if (token == "map_Ke")
			{
				material.emission = true;
			}
		}

		/// <summary>Разбирает опции -s/-o/-t/-bm перед именем текстуры. Возвращает индекс имени текстуры.</summary>
		private static int ReadTextureOptions(string[] parts, out Vector4 uv)
		{
			uv = new Vector4(1, 1, 0, 0);
			int start = 0;

			while (start < parts.Length && parts[start].StartsWith("-"))
			{
				string option = parts[start++];

				if (option == "-bm")
				{
					start++;
					continue;
				}

				if (option != "-s" && option != "-o" && option != "-t")
				{
					throw new InvalidDataException("Unknown texture option: " + option);
				}

				for (int axis = 0; axis < 3 && start < parts.Length && float.TryParse(parts[start], NumberStyles.Float, CultureInfo.InvariantCulture, out float number); axis++, start++)
				{
					if (axis < 2 && option != "-t")
					{
						uv[axis + (option == "-o" ? 2 : 0)] = number;
					}
				}
			}

			return start;
		}

		private static float ParseFloat(string text)
		{
			return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
		}
	}
}
