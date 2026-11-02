using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Mirror candidate: an enum member outside the excluded scope that has an entry in MaterialEditorSettings.asset (first by id wins,
	/// as in StandardShaderGUI). Combines the snapshot records variant, prop, defines, streams and decode. A name with the "?" suffix in
	/// features/streams/otherDefines/libraries is defined only in a branch on a runtime keyword or platform.
	/// </summary>
	public sealed class RenderCoreLockVariant
	{
		/// <summary>Enum member name = shader keyword.</summary>
		public readonly string name;

		public readonly int id;

		/// <summary>Name field of the asset entry; equals name for valid data (otherwise the generator refuses).</summary>
		public readonly string label;

		/// <summary>info.queue.value (StandardRenderQueue).</summary>
		public readonly int queueBase;

		public readonly int queueOffset;

		/// <summary>info.type: 0 Opaque, 1 Transparent, 2 AlphaTest.</summary>
		public readonly int renderType;

		/// <summary>info.additionalKeywords split as in SetupMaterialVariant (' ', ',', ';', '.'), in asset order.</summary>
		public readonly IReadOnlyList<string> keywords;

		/// <summary>Number of asset entries with this id (more than 1 means duplicates, the first is used).</summary>
		public readonly int entries;

		/// <summary>propertyData in asset order.</summary>
		public readonly IReadOnlyList<RenderCoreLockProperty> properties;

		/// <summary>Definitions file name without extension ("BaseMaterialDefs"); several joined with '+'; empty if the include is not found.</summary>
		public readonly string defs;

		/// <summary>MATERIALFEATURE_* and PACKED_MESH* after evaluating Definitions (MAT_X + SHADERPASS_FORWARD), ordinal.</summary>
		public readonly IReadOnlyList<string> features;

		/// <summary>Other active defines (except features, INPUT_NEED_* and the input MAT_X/SHADERPASS_FORWARD), ordinal.</summary>
		public readonly IReadOnlyList<string> otherDefines;

		/// <summary>Active INPUT_NEED_* (Forward pass vertex streams), ordinal.</summary>
		public readonly IReadOnlyList<string> streams;

		/// <summary>Library includes (outside the Standard folder) in active Definitions branches, by file name, ordinal; part of decodeHash.</summary>
		public readonly IReadOnlyList<string> libraries;

		/// <summary>Functions file name without extension ("BaseMaterialFunc"); several joined with '+'; empty if there are no functions.</summary>
		public readonly string func;

		/// <summary>Hash of Definitions libraries and active Func lines (RenderCoreLock.GetDecodeLines).</summary>
		public readonly string decodeHash;

		public RenderCoreLockVariant(string name, int id, string label, int queueBase, int queueOffset, int renderType, IReadOnlyList<string> keywords,
			int entries, IReadOnlyList<RenderCoreLockProperty> properties, string defs, IReadOnlyList<string> features, IReadOnlyList<string> otherDefines,
			IReadOnlyList<string> streams, IReadOnlyList<string> libraries, string func, string decodeHash)
		{
			this.name = name;
			this.id = id;
			this.label = label;
			this.queueBase = queueBase;
			this.queueOffset = queueOffset;
			this.renderType = renderType;
			this.keywords = keywords;
			this.entries = entries;
			this.properties = properties;
			this.defs = defs;
			this.features = features;
			this.otherDefines = otherDefines;
			this.streams = streams;
			this.libraries = libraries;
			this.func = func;
			this.decodeHash = decodeHash;
		}

		/// <summary>Material renderQueue, as in SetupMaterialVariant.</summary>
		public int Queue => queueBase + queueOffset;

		public bool HasFeature(string feature)
		{
			foreach (string candidate in features)
			{
				if (candidate == feature)
				{
					return true;
				}
			}

			return false;
		}

		public bool TryGetProperty(string propertyName, out RenderCoreLockProperty property)
		{
			foreach (RenderCoreLockProperty candidate in properties)
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
