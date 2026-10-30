using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Разобранный Standard.shader: блок Properties и проходы.</summary>
	public sealed class RenderCoreShaderInfo
	{
		public readonly IReadOnlyList<RenderCoreLockShaderProperty> properties;
		public readonly IReadOnlyList<RenderCoreShaderPass> passes;

		public RenderCoreShaderInfo(IReadOnlyList<RenderCoreLockShaderProperty> properties, IReadOnlyList<RenderCoreShaderPass> passes)
		{
			this.properties = properties;
			this.passes = passes;
		}

		public bool TryGetProperty(string name, out RenderCoreLockShaderProperty property)
		{
			foreach (RenderCoreLockShaderProperty candidate in properties)
			{
				if (candidate.name == name)
				{
					property = candidate;
					return true;
				}
			}

			property = null;
			return false;
		}

		public bool TryGetPass(string name, out RenderCoreShaderPass pass)
		{
			foreach (RenderCoreShaderPass candidate in passes)
			{
				if (candidate.name == name)
				{
					pass = candidate;
					return true;
				}
			}

			pass = null;
			return false;
		}
	}
}
