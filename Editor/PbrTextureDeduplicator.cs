using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Удаление побайтовых дублей PNG, созданных экспортом Layered Lit. Удаляются только сгенерированные копии
	/// (<c>*_pbr.png</c>, <c>*_pbr_normal.png</c>); .mtl и исходные ассеты не трогаются.
	/// </summary>
	public static class PbrTextureDeduplicator
	{
		/// <summary>Возвращает число освобождённых байт.</summary>
		public static long Deduplicate(string directory)
		{
			(string path, ModPbrMaterial data)[] documents = Directory.GetFiles(directory, "*.pbr.json")
				.Select(path => (path, JsonUtility.FromJson<ModPbrMaterial>(File.ReadAllText(path))))
				.ToArray();

			if (documents.Length == 0 || documents.Any(document => document.data == null || document.data.version != 1 || document.data.layers == null))
			{
				return 0;
			}

			Dictionary<string, string> aliases = FindAliases(directory);

			if (aliases.Count == 0)
			{
				return 0;
			}

			foreach ((string path, ModPbrMaterial data) in documents)
			{
				data.blendMask = Resolve(aliases, data.blendMask);

				foreach (ModPbrLayer layer in data.layers)
				{
					layer.diffuse = Resolve(aliases, layer.diffuse);
					layer.normal = Resolve(aliases, layer.normal);
					layer.mask = Resolve(aliases, layer.mask);
				}

				File.WriteAllText(path, JsonUtility.ToJson(data));
			}

			// Ссылки переписаны до удаления файлов: прерванный проход не оставит висячих ссылок.
			long saved = 0;

			foreach (string name in aliases.Keys)
			{
				string path = Path.Combine(directory, name);
				saved += new FileInfo(path).Length;
				File.Delete(path);
			}

			return saved;
		}

		private static Dictionary<string, string> FindAliases(string directory)
		{
			string[] protectedMtl = Directory.GetFiles(directory, "*.mtl").Select(File.ReadAllText).ToArray();
			var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
			var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
			using SHA256 sha = SHA256.Create();

			IEnumerable<string> files = Directory.GetFiles(directory, "*.png")
				.OrderBy(path => IsPbrCopy(Path.GetFileName(path)))
				.ThenBy(path => path, StringComparer.Ordinal);

			foreach (string path in files)
			{
				string name = Path.GetFileName(path);
				string hash;

				using (FileStream input = File.OpenRead(path))
				{
					hash = Convert.ToBase64String(sha.ComputeHash(input));
				}

				if (!canonical.TryGetValue(hash, out string original))
				{
					canonical.Add(hash, name);
					continue;
				}

				if (!IsPbrCopy(name) || protectedMtl.Any(mtl => mtl.Contains(name)))
				{
					continue;
				}

				// Хэш только находит кандидатов; байты сравниваются, чтобы дедупликация была строго без потерь.
				if (File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(directory, original))))
				{
					aliases.Add(name, original);
				}
			}

			return aliases;
		}

		private static string Resolve(Dictionary<string, string> aliases, string name)
		{
			return name != null && aliases.TryGetValue(name, out string original) ? original : name;
		}

		private static bool IsPbrCopy(string name)
		{
			return name.EndsWith("_pbr.png", StringComparison.Ordinal) || name.EndsWith("_pbr_normal.png", StringComparison.Ordinal);
		}
	}
}
