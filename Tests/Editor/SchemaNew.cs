using System;
using System.Collections.Generic;

// DTO для тестов должны лежать в пространстве имён формата: BinaryModData читает только его типы.
namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Та же DTO после обновления игры: добавлены поля с инициализаторами.</summary>
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
