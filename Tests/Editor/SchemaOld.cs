using System;

// Test DTOs must live in the format namespace: BinaryModData reads only its types.
namespace CarX.Modding.Creator.Runtime
{
	/// <summary>DTO schema at the time the mod was published.</summary>
	internal static class SchemaOld
	{
		[Serializable]
		public sealed class EvolvingDto
		{
			public string name;
			public int count;
		}
	}
}
