using System;
using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Mods team decision on an offered variant: what a human reviewed in rendercore (layout key, decode fingerprint, packed map channels,
	/// vertex colors) and under which contract revision the variant is published. The generator checks this against the snapshot and
	/// refuses on mismatch (MirrorFindingCodes).
	/// </summary>
	public sealed class MirrorVariantAnnotation
	{
		/// <summary>MaterialVariant enum member name = shader keyword.</summary>
		public readonly string name;

		/// <summary>Baked contract revision (≥ 1): bumped when the meaning of already published textures or vertex colors changes.</summary>
		public readonly int revision;

		/// <summary>Layout key as MirrorLayoutRules derives it: "Base ALPHATEST AO ROUGHNESS depth:generic shadow:generic".</summary>
		public readonly string layoutKey;

		/// <summary>Fingerprint of the variant's active Func lines that a human reviewed (decode in the snapshot).</summary>
		public readonly string reviewedDecodeHash;

		public readonly MirrorVertexColors vertexColors;

		/// <summary>R, G, B, A channels for each u_sPackedMap* of the variant; null — the variant has no packed map.</summary>
		public readonly IReadOnlyList<MirrorChannel> channels;

		/// <summary>Why the variant is unavailable in mods (e.g. it needs a second UV), or null.</summary>
		public readonly string unavailableReason;

		public readonly IReadOnlyList<MirrorSlotOverride> overrides;

		public MirrorVariantAnnotation(string name, int revision, string layoutKey, string reviewedDecodeHash, MirrorVertexColors vertexColors,
			IReadOnlyList<MirrorChannel> channels, string unavailableReason, IReadOnlyList<MirrorSlotOverride> overrides)
		{
			this.name = name;
			this.revision = revision;
			this.layoutKey = layoutKey;
			this.reviewedDecodeHash = reviewedDecodeHash;
			this.vertexColors = vertexColors;
			this.channels = channels;
			this.unavailableReason = unavailableReason;
			this.overrides = overrides ?? Array.Empty<MirrorSlotOverride>();
		}

		public bool TryGetOverride(string property, out MirrorSlotOverride slotOverride)
		{
			foreach (MirrorSlotOverride candidate in overrides)
			{
				if (candidate.property == property)
				{
					slotOverride = candidate;
					return true;
				}
			}

			slotOverride = null;
			return false;
		}
	}
}
