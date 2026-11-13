using System;
using System.Collections.Generic;

// Test DTOs must live in the format namespace: BinaryModData reads only its types.
namespace CarX.Modding.Creator.Runtime
{
	/// <summary>The same DTO after a game update: fields with initializers added.</summary>
	internal static class SchemaNew
	{
		[Serializable]
		public sealed class EvolvingDto
		{
			public const int AddedLaterDefault = 7;

			public string name;
			public int count;
			public int addedLater = AddedLaterDefault;
			public List<string> addedList = new();
		}
	}
}
