using System;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Settings of the Sound game marker. A preset or custom sound plays at the marker position (3D);
	// a map ambience preset plays without a position for the whole map
	[Serializable]
	public class SoundMarkerSettings
	{
		public const string MarkerHead = "Sound";
		public const string PresetParam = MarkerHead + "/Preset";
		public const string CustomParam = MarkerHead + "/Custom";
		public const string AmbienceParam = MarkerHead + "/Ambience";

		public const float MinDistanceLimit = 0.5f;
		public const float MaxDistanceLimit = 500f;

		[Tooltip("Game sound, see ModSoundPresets")]
		public string preset = string.Empty;

		[Tooltip("Own sound: an .ogg or .wav file of the project")]
		public AudioClip clip;

		[Tooltip("Loudness of the sound, 0 to 1")]
		[Range(0f, 1f)]
		public float volume = 1f;

		[Tooltip("Up to this distance the sound plays at full volume, meters")]
		[Range(MinDistanceLimit, MaxDistanceLimit)]
		public float minDistance = 5f;

		[Tooltip("Beyond this distance the sound is not heard, meters")]
		[Range(MinDistanceLimit, MaxDistanceLimit)]
		public float maxDistance = 50f;

		public SoundEmitterKind Kind(string param)
		{
			return param switch
			{
				CustomParam => SoundEmitterKind.Custom,
				AmbienceParam => SoundEmitterKind.Ambience,
				_ => SoundEmitterKind.Preset,
			};
		}
	}
}
