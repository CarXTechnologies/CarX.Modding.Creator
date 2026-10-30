using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Решение команды модов по предлагаемому варианту: что из rendercore проверено человеком (ключ раскладки, отпечаток декодирования,
	/// каналы packed map, цвет вершин) и под какой ревизией контракта вариант публикуется. Генератор сверяет это со снимком и отказывает
	/// при расхождении (MirrorFindingCodes).
	/// </summary>
	public sealed class MirrorVariantAnnotation
	{
		/// <summary>Имя члена enum MaterialVariant = keyword шейдера.</summary>
		public readonly string name;

		/// <summary>Ревизия запечённого контракта (≥ 1): поднимается, когда меняется смысл уже опубликованных текстур или цвета вершин.</summary>
		public readonly int revision;

		/// <summary>Ключ раскладки, как его выводит MirrorLayoutRules: "Base ALPHATEST AO ROUGHNESS depth:generic shadow:generic".</summary>
		public readonly string layoutKey;

		/// <summary>Отпечаток активных строк Func варианта, который просмотрел человек (decode в снимке).</summary>
		public readonly string reviewedDecodeHash;

		public readonly MirrorVertexColors vertexColors;

		/// <summary>Каналы R, G, B, A для каждого u_sPackedMap* варианта; null — у варианта нет packed map.</summary>
		public readonly IReadOnlyList<MirrorChannel> channels;

		/// <summary>Почему вариант недоступен в модах (например, нужен второй UV) или null.</summary>
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
