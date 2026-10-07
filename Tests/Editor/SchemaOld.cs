using System;

// DTO для тестов должны лежать в пространстве имён формата: BinaryModData читает только его типы.
namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Схема DTO на момент публикации мода.</summary>
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
