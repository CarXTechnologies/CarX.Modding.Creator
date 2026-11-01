using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Frozen (variant, revision) contract: how the client reads already published document textures (color space, packed map layout,
	/// normal encoding) and vertex colors. Entries are append only: changing the meaning means bumping the revision and adding a new
	/// entry (MirrorFindingCodes.ContractChanged). Properties outside the contract (added later) do not require a revision.
	/// </summary>
	public sealed class MirrorBakedContract
	{
		public readonly string variant;
		public readonly int revision;
		public readonly MirrorVertexColors vertexColors;

		/// <summary>"&lt;property&gt;=&lt;srgb|linear&gt;[:&lt;R,G,B,A&gt;|:normal]", ordered by property name (ordinal).</summary>
		public readonly IReadOnlyList<string> textures;

		public MirrorBakedContract(string variant, int revision, MirrorVertexColors vertexColors, IEnumerable<string> textures)
		{
			this.variant = variant;
			this.revision = revision;
			this.vertexColors = vertexColors;
			this.textures = textures.OrderBy(GetProperty, StringComparer.Ordinal).ThenBy(texture => texture, StringComparer.Ordinal).ToList();
		}

		/// <summary>One line: "MAT_X@1 vertexColors=None u_sPackedMap=linear:Roughness,Zero,Alpha,Zero u_sTexture=srgb".</summary>
		public string Text => MirrorContracts.Format(variant, revision, vertexColors, textures);

		/// <summary>Property name of a textures entry (before '=').</summary>
		public static string GetProperty(string texture)
		{
			int separator = texture.IndexOf('=');
			return separator < 0 ? texture : texture.Substring(0, separator);
		}

		public bool TryGetTexture(string property, out string texture)
		{
			foreach (string candidate in textures)
			{
				if (GetProperty(candidate) == property)
				{
					texture = candidate;
					return true;
				}
			}

			texture = null;
			return false;
		}
	}
}
