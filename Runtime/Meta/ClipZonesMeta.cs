using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	public enum ClipZoneKind
	{
		TouchAndGo = 0,
		Triple = 1
	}

	public enum ClipZoneSide
	{
		Left = 0,
		Right = 1
	}

	public enum TripleZoneKind
	{
		Inner = 0,
		Outer = 1
	}

	// Free ride clipping zones and their chains. Zone geometry is in map space; the game derives zone edges and visuals from it
	[Serializable]
	public class ClipZonesMeta : IModResources, IModResourcesVersion
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
		public List<ClipZoneInstance> zones = new();
		public List<ClipChainInstance> chains = new();

		public ClipZonesMeta(string id, string version, List<ClipZoneInstance> zones, List<ClipChainInstance> chains)
		{
			this.id = id;
			this.version = version;
			this.zones = zones;
			this.chains = chains;
		}

		// For BinaryModData: fields missing from the document get their initializer values, not default.
		private ClipZonesMeta()
		{
		}
	}

	[Serializable]
	public class ClipZoneInstance
	{
		public ClipZoneKind kind;
		// Center line of the zone on the road surface, in map space
		public Vector3[] points = Array.Empty<Vector3>();
		// Distance from the center line to each zone edge
		public float halfWidth;

		// Triple zone only
		public ClipZoneSide innerSide;
		public TripleZoneKind tripleKind = TripleZoneKind.Outer;
		public int blocksCount = 3;
		public float blockLength = 2.2f;
		public float extendedAreaWidthFactor = 1f;

		public ClipZoneInstance()
		{
		}
	}

	// Zones in the order the player drives through them, as indexes into ClipZonesMeta.zones
	[Serializable]
	public class ClipChainInstance
	{
		public string name;
		public int[] zones = Array.Empty<int>();

		public ClipChainInstance()
		{
		}
	}
}
