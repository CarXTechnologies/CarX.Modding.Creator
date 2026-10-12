using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Зеркало вариантов Standard-шейдера rendercore для маркера Material (только Binary). Данные — в ModRenderCoreVariantTable.g.cs: его генерирует
	/// зеркало rendercore (Editor/RenderCoreMirror) из снимка RenderCoreLock.txt и ручных аннотаций. Здесь — API и правила, которые запечены
	/// в опубликованные моды и потому не меняются (цветовое пространство текстур по имени свойства).
	/// Таблица — только управляемые данные без Unity API: клиент читает её с пула потоков.
	/// </summary>
	public static class ModRenderCoreVariants
	{
		/// <summary>LockContentHash снимка rendercore, из которого сгенерирована таблица.</summary>
		public const string LockContentHash = ModRenderCoreVariantTable.LockContentHash;

		private const string ColorTexturePrefix = "u_sTexture";

		/// <summary>Потоки, которые несут меши модов (BinaryModMesh: позиция, нормаль, UV0, цвет; тангенсы клиент пересчитывает).</summary>
		public const ModRenderCoreVertexStreams SupportedStreams = ModRenderCoreVertexStreams.Position | ModRenderCoreVertexStreams.Normal |
			ModRenderCoreVertexStreams.Tangent | ModRenderCoreVertexStreams.Uv0 | ModRenderCoreVertexStreams.Color;

		private static readonly ModRenderCoreVariant[] s_variants;
		private static readonly ModRenderCoreVariant[] s_offered;
		private static readonly ModRenderCoreSuggestion[] s_suggestions;
		private static readonly Dictionary<string, ModRenderCoreVariant> s_byName;
		private static readonly string[] s_duplicates;

		/// <summary>Единственное место инициализации: таблица строится один раз, без исключений.</summary>
		static ModRenderCoreVariants()
		{
			s_variants = ModRenderCoreVariantTable.CreateVariants();
			s_suggestions = ModRenderCoreVariantTable.CreateSuggestions();
			s_byName = BuildLookup(s_variants, out s_duplicates);
			s_offered = Array.FindAll(s_variants, variant => variant.Available);
		}

		/// <summary>Все варианты зеркала, включая недоступные в модах.</summary>
		public static IReadOnlyList<ModRenderCoreVariant> All => s_variants;

		/// <summary>Варианты, которые можно выбрать в маркере.</summary>
		public static IReadOnlyList<ModRenderCoreVariant> Offered => s_offered;

		/// <summary>Имена, встретившиеся в таблице повторно (берётся первое); для корректной таблицы пусто.</summary>
		public static IReadOnlyList<string> Duplicates => s_duplicates;

		/// <summary>Доступный вариант по имени (keyword). Недоступный или неизвестный — false.</summary>
		public static bool TryGet(string name, out ModRenderCoreVariant variant)
		{
			return TryFind(name, out variant) && variant.Available;
		}

		/// <summary>Вариант по имени независимо от доступности: чтобы объяснить автору, почему его нельзя использовать.</summary>
		public static bool TryFind(string name, out ModRenderCoreVariant variant)
		{
			if (string.IsNullOrEmpty(name))
			{
				variant = null;
				return false;
			}

			return s_byName.TryGetValue(name, out variant);
		}

		/// <summary>Вариант, который Export from material предлагает для материала HDRP, или null.</summary>
		public static string Suggest(ModRenderCoreRenderType renderType, bool hasNormal, bool hasEmission)
		{
			foreach (ModRenderCoreSuggestion suggestion in s_suggestions)
			{
				if (suggestion.renderType == renderType && suggestion.hasNormal == hasNormal && suggestion.hasEmission == hasEmission &&
					TryGet(suggestion.variant, out _))
				{
					return suggestion.variant;
				}
			}

			return null;
		}

		/// <summary>
		/// Цветовая текстура (u_sTexture*) хранится в sRGB, остальные (packed map, нормали) — линейные. Правило запечено в заголовки BinaryTexture
		/// опубликованных модов и поэтому не следует за rendercore; зеркало проверяет, что [Gamma] шейдера с ним согласен.
		/// </summary>
		public static bool IsColorTexture(string propertyName)
		{
			return propertyName != null && propertyName.StartsWith(ColorTexturePrefix, StringComparison.Ordinal);
		}

		/// <summary>Меши модов дают все потоки, которые читает вариант.</summary>
		public static bool HasSupportedStreams(ModRenderCoreVariant variant)
		{
			return (variant.requiredStreams & ~SupportedStreams) == ModRenderCoreVertexStreams.None;
		}

		private static Dictionary<string, ModRenderCoreVariant> BuildLookup(ModRenderCoreVariant[] variants, out string[] duplicates)
		{
			var lookup = new Dictionary<string, ModRenderCoreVariant>(StringComparer.Ordinal);
			var repeated = new List<string>();

			foreach (ModRenderCoreVariant variant in variants)
			{
				// Первый выигрывает, как в rendercore; исключение в статическом конструкторе сломало бы клиент на пуле потоков.
				if (!lookup.TryAdd(variant.name, variant))
				{
					repeated.Add(variant.name);
				}
			}

			duplicates = repeated.ToArray();
			return lookup;
		}

		internal static ModRenderCoreVariantProperty Texture(string name, string label, string tooltip, ModRenderCoreSource source, int layer)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, ModRenderCorePropertyType.Texture, source, layer, Vector4.one, vectorLabels: null, channels: null);
		}

		internal static ModRenderCoreVariantProperty Packed(string name, string label, string tooltip, int layer, ModRenderCoreChannel r, ModRenderCoreChannel g,
			ModRenderCoreChannel b, ModRenderCoreChannel a)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, ModRenderCorePropertyType.Texture, ModRenderCoreSource.Surface, layer, Vector4.one,
				vectorLabels: null, new[] { r, g, b, a });
		}

		internal static ModRenderCoreVariantProperty Value(string name, string label, string tooltip, ModRenderCorePropertyType type, ModRenderCoreSource source,
			Vector4 defaultValue, IReadOnlyList<string> vectorLabels)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, type, source, layer: 0, defaultValue, vectorLabels, channels: null);
		}
	}
}
