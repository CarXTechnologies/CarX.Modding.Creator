using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	public enum SoundEmitterKind
	{
		// A game sound by ModSoundPresets.Point id, at a position
		Preset,
		// An audio file of the mod (SoundsMeta.files), at a position
		Custom,
		// A game background sound by ModSoundPresets.Ambience id, for the whole map
		Ambience,
	}

	[Serializable]
	public class SoundEmitterInstance
	{
		public SoundEmitterKind kind;
		// Preset and Ambience: the preset id
		public string preset = string.Empty;
		// Custom: index into SoundsMeta.files
		public int file = -1;
		// World space of the map
		public Vector3 position;
		public float volume = 1f;
		public float minDistance = 5f;
		public float maxDistance = 50f;
	}

	// Sounds of a map: emitters and the audio files of the mod they play
	[Serializable]
	public class SoundsMeta : IModResources, IModResourcesVersion
	{
		public const int MaxEmitters = 128;
		public const int MaxFiles = 32;
		public const long MaxFilesBytes = 20L * 1024 * 1024;
		public static readonly string[] FileExtensions = { ".ogg", ".wav" };

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
		public List<SoundEmitterInstance> emitters = new();
		// File names under SoundsMetaProvider.FilesCatalog, with the extension
		public List<string> files = new();

		public SoundsMeta(string id, string version, List<SoundEmitterInstance> emitters, List<string> files)
		{
			this.id = id;
			this.version = version;
			this.emitters = emitters;
			this.files = files;
		}

		// For BinaryModData: fields missing from the document get their initializer values, not default.
		private SoundsMeta()
		{
		}
	}
}
