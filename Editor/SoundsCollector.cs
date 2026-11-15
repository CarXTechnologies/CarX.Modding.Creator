using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Collects the Sound game markers of the exported map into the sounds document and the audio files of the mod
	public static class SoundsCollector
	{
		private const int FileHashLength = 8;

		private readonly struct SoundMarker
		{
			public readonly Component component;
			public readonly SoundEmitterKind kind;
			public readonly SoundMarkerSettings settings;

			public SoundMarker(Component component, SoundEmitterKind kind, SoundMarkerSettings settings)
			{
				this.component = component;
				this.kind = kind;
				this.settings = settings;
			}
		}

		/// <param name="files">Receives the audio files the emitters play, one per source asset</param>
		/// <returns>The document, or null when the map has no valid sound markers</returns>
		public static SoundsMeta Collect(string id, string version, Transform[] roots, Func<Transform, bool> isExcluded, List<ModSoundFile> files)
		{
			var emitters = new List<SoundEmitterInstance>();
			var fileNames = new List<string>();
			var fileIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
			long filesBytes = 0;
			bool hasAmbience = false;

			foreach (SoundMarker marker in GetMarkers(roots, isExcluded))
			{
				Component component = marker.component;

				if (emitters.Count >= SoundsMeta.MaxEmitters)
				{
					Debug.LogWarning($"[Sounds] More than {SoundsMeta.MaxEmitters} sound markers: '{component.name}' and the rest are not exported.", component);
					break;
				}

				SoundEmitterInstance emitter = CreateEmitter(marker);

				if (marker.kind == SoundEmitterKind.Ambience)
				{
					if (!IsKnownPreset(ModSoundPresets.Ambience, emitter.preset, component))
					{
						continue;
					}

					if (hasAmbience)
					{
						Debug.LogWarning($"[Sounds] The map has more than one ambience: '{component.name}' is skipped, the first one plays.", component);
						continue;
					}

					hasAmbience = true;
				}
				else if (marker.kind == SoundEmitterKind.Preset)
				{
					if (!IsKnownPreset(ModSoundPresets.Point, emitter.preset, component))
					{
						continue;
					}
				}
				else
				{
					emitter.preset = string.Empty;
					emitter.file = AddFile(marker, files, fileNames, fileIndexes, ref filesBytes);

					if (emitter.file < 0)
					{
						continue;
					}
				}

				emitters.Add(emitter);
			}

			if (emitters.Count == 0)
			{
				return null;
			}

			Debug.Log($"[Sounds] Exported {emitters.Count} sound emitters and {fileNames.Count} audio files.");
			return new SoundsMeta(id, version, emitters, fileNames);
		}

		/// <summary>The name of an audio asset in the mod: the asset name without unsafe characters and a short hash of the bytes.</summary>
		public static string GetFileName(string assetPath, byte[] bytes)
		{
			var name = new StringBuilder();

			foreach (char c in Path.GetFileNameWithoutExtension(assetPath))
			{
				name.Append(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' ? c : '_');
			}

			using SHA256 sha = SHA256.Create();
			string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).Substring(0, FileHashLength).ToLowerInvariant();

			return $"{name}_{hash}{Path.GetExtension(assetPath).ToLowerInvariant()}";
		}

		public static bool IsSupportedFile(string assetPath)
		{
			return SoundsMeta.FileExtensions.Contains(Path.GetExtension(assetPath).ToLowerInvariant());
		}

		private static SoundEmitterInstance CreateEmitter(SoundMarker marker)
		{
			SoundMarkerSettings settings = marker.settings;
			float maxDistance = Mathf.Clamp(settings.maxDistance, SoundMarkerSettings.MinDistanceLimit, SoundMarkerSettings.MaxDistanceLimit);

			return new SoundEmitterInstance
			{
				kind = marker.kind,
				preset = settings.preset?.Trim() ?? string.Empty,
				position = marker.component.transform.position,
				volume = Mathf.Clamp01(settings.volume),
				minDistance = Mathf.Clamp(settings.minDistance, SoundMarkerSettings.MinDistanceLimit, maxDistance),
				maxDistance = maxDistance,
			};
		}

		private static bool IsKnownPreset(IReadOnlyList<ModSoundPresets.Preset> presets, string preset, Component component)
		{
			if (presets.Any(item => item.id == preset))
			{
				return true;
			}

			Debug.LogWarning($"[Sounds] Sound '{component.name}' has the unknown preset '{preset}' and is skipped.", component);
			return false;
		}

		// The index of the marker's audio file in the document; one file per asset. -1 when the file is not exported
		private static int AddFile(SoundMarker marker, List<ModSoundFile> files, List<string> fileNames, Dictionary<string, int> fileIndexes,
			ref long filesBytes)
		{
			Component component = marker.component;
			AudioClip clip = marker.settings.clip;
			string assetPath = clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty;

			if (string.IsNullOrEmpty(assetPath))
			{
				Debug.LogWarning($"[Sounds] Sound '{component.name}' has no audio clip and is skipped.", component);
				return -1;
			}

			if (fileIndexes.TryGetValue(assetPath, out int index))
			{
				return index;
			}

			if (!IsSupportedFile(assetPath))
			{
				Debug.LogWarning($"[Sounds] Sound '{component.name}' uses '{Path.GetFileName(assetPath)}': only .ogg and .wav files are exported, the sound is skipped.", component);
				return -1;
			}

			if (fileNames.Count >= SoundsMeta.MaxFiles)
			{
				Debug.LogWarning($"[Sounds] More than {SoundsMeta.MaxFiles} audio files: sound '{component.name}' is skipped.", component);
				return -1;
			}

			byte[] bytes = File.ReadAllBytes(Path.GetFullPath(FileUtil.GetPhysicalPath(assetPath)));

			if (filesBytes + bytes.Length > SoundsMeta.MaxFilesBytes)
			{
				Debug.LogWarning($"[Sounds] Audio files exceed {SoundsMeta.MaxFilesBytes / (1024 * 1024)} MB: sound '{component.name}' is skipped.", component);
				return -1;
			}

			string name = GetFileName(assetPath, bytes);
			index = fileNames.IndexOf(name);

			if (index < 0)
			{
				filesBytes += bytes.Length;
				fileNames.Add(name);
				files.Add(new ModSoundFile(name, bytes));
				index = fileNames.Count - 1;
			}

			fileIndexes.Add(assetPath, index);
			return index;
		}

		private static IEnumerable<SoundMarker> GetMarkers(Transform[] roots, Func<Transform, bool> isExcluded)
		{
			foreach (Transform root in roots)
			{
				if (root == null)
				{
					continue;
				}

				foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: false))
				{
					if (component is IMarkerDataSource { MarkerHead: SoundMarkerSettings.MarkerHead, MarkerData: SoundMarkerSettings settings } source &&
						component.enabled && (isExcluded == null || !isExcluded(component.transform)))
					{
						yield return new SoundMarker(component, settings.Kind(source.MarkerParam), settings);
					}
				}
			}
		}
	}
}
