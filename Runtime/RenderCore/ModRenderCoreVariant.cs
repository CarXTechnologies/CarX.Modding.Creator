using System.Collections.Generic;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// rendercore Standard shader variant (MAT_* keyword): a mirror of RenderCore.Editor.MaterialVariantData without depending on the rendercore
	/// editor assembly. The material is set up like EditorUtils.SetupMaterialVariant: variant keyword and additional keywords, queue,
	/// RenderType tag and the materialVariant property.
	/// </summary>
	public sealed class ModRenderCoreVariant
	{
		public readonly string name;

		/// <summary>RenderCore.Editor.MaterialVariant value (the material's materialVariant property); metadata only, nothing is looked up by id.</summary>
		public readonly int id;

		public readonly int queue;
		public readonly ModRenderCoreRenderType renderType;
		public readonly IReadOnlyList<string> keywords;

		public readonly ModRenderCoreVertexColors vertexColors;

		public readonly ModRenderCoreVertexStreams requiredStreams;

		/// <summary>Why the variant cannot be used in mods (e.g. it needs a second UV), or null if it can.</summary>
		public readonly string unavailableReason;

		/// <summary>
		/// Revision of the variant's baked contract (packed map layout, color space, vertex colors). Written to the mod document;
		/// the client applies the document only with the same revision, otherwise it uses the MTL fallback material.
		/// </summary>
		public readonly int revision;

		public readonly IReadOnlyList<ModRenderCoreVariantProperty> properties;

		public ModRenderCoreVariant(string name, int id, int queue, ModRenderCoreRenderType renderType, IReadOnlyList<string> keywords,
			ModRenderCoreVertexColors vertexColors, ModRenderCoreVertexStreams requiredStreams, string unavailableReason, int revision,
			IReadOnlyList<ModRenderCoreVariantProperty> properties)
		{
			this.name = name;
			this.id = id;
			this.queue = queue;
			this.renderType = renderType;
			this.keywords = keywords;
			this.vertexColors = vertexColors;
			this.requiredStreams = requiredStreams;
			this.unavailableReason = unavailableReason;
			this.revision = revision;
			this.properties = properties;
		}

		/// <summary>The variant can be selected in the marker and applied in the game.</summary>
		public bool Available => unavailableReason == null;

		/// <summary>Emissive variant: rendercore multiplies emission by vertex color.</summary>
		public bool Emission => vertexColors == ModRenderCoreVertexColors.EmissionMultiplier;

		public bool TryGetProperty(string propertyName, out ModRenderCoreVariantProperty property)
		{
			foreach (ModRenderCoreVariantProperty candidate in properties)
			{
				if (candidate.name == propertyName)
				{
					property = candidate;
					return true;
				}
			}

			property = null;
			return false;
		}
	}
}
