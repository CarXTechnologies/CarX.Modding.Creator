using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// ЕДИНСТВЕННЫЙ ручной вход зеркала rendercore: решения команды модов. Таблица Runtime/RenderCore/ModRenderCoreVariantTable.g.cs =
	/// generate(RenderCoreLock.txt, эти аннотации); после правки — ModSystem/Render Core/Regenerate Table (или CLI generate).
	///
	/// Variants — предлагаемые варианты в порядке таблицы. Для каждого:
	///   layoutKey и reviewedDecodeHash — то, что человек проверил в rendercore; при расхождении генератор отказывает (RCM040/RCM041) и
	///   печатает текущие значения. Смысл не изменился — обновить значение; изменился — поправить каналы/цвет вершин и поднять ревизию;
	///   channels — раскладка R, G, B, A всех u_sPackedMap* варианта (null — packed map нет); для Base/Layered сверяется с define'ами (RCM043);
	///   vertexColors — как вариант использует цвет вершин (сверяется с define'ами, RCM061);
	///   unavailableReason — почему вариант нельзя использовать в модах (обязателен, если потоки шире SupportedStreams, RCM060);
	///   revision — ревизия запечённого контракта; документы мода хранят её, клиент применяет только совпадающую.
	/// Contracts — замороженные контракты (вариант, ревизия): только дописываются. Новая ревизия = новая запись; старые не правятся
	/// (реестр отпечатков в RenderCoreMirrorTableTests это проверяет).
	/// Suggestions — какой вариант Export from material предлагает для материала HDRP (первая подходящая).
	/// AlphaTestPasses — проверенные конфигурации проходов выреза (DepthOnly/ShadowCaster): вырез модов альфа-теста делают они, Forward
	///   рисует с ZTest Equal. reviewedHash — что человек посмотрел в rendercore (Defs, особенности, потоки, Func и программа прохода);
	///   при расхождении генератор отказывает (RCM045) и печатает текущее значение.
	/// Ревизия 1 — состояние после исправлений перед первым выпуском модов: u_vThirdColor без роли (порог альфа-теста фиксированный),
	/// MAT_ROUGHNESS_ALPHABLEND B = Alpha, MAT_COLORMASK_* B = ColorMask, u_sTexture1 у MAT_ROAD_SIGN_EMISSION — эмиссия, MAT_INTERIOR
	/// недоступен (нужен TEXCOORD1) и цвет вершин не читает, MAT_LAYERS4 смешивает слои цветом вершин.
	/// </summary>
	public static class RenderCoreMirrorAnnotations
	{
		/// <summary>Потоки мешей модов (BinaryModMesh: позиция, нормаль, UV0, цвет; тангенсы клиент пересчитывает). Равно Runtime SupportedStreams.</summary>
		public const MirrorVertexStreams SupportedStreams = MirrorVertexStreams.Position | MirrorVertexStreams.Normal | MirrorVertexStreams.Tangent |
			MirrorVertexStreams.Uv0 | MirrorVertexStreams.Color;

		/// <summary>
		/// Отпечаток тела EditorUtils.SetupMaterialVariant, с которым сверён MaterialMappingPBR.ApplyRenderCoreVariant в CarX.Modding
		/// (keyword варианта + additionalKeywords, разбитые по ' ', ',', ';', '.', очередь base + offset, тег RenderType, materialVariant).
		/// </summary>
		public const string ReviewedSetupHash = "b342e9ce390b";

		private const string NoSecondUv = "нужен второй UV (TEXCOORD1), а меши модов несут только UV0";

		/// <summary>Область вариантов карт: та же, с которой извлекается снимок.</summary>
		public static RenderCoreScope Scope => RenderCoreScope.Default;

		public static readonly IReadOnlyList<MirrorVariantAnnotation> Variants = new[]
		{
			Variant("MAT_DEFAULT", revision: 1, layoutKey: "Base", reviewedDecodeHash: "2bbff0fe734d", MirrorVertexColors.None, channels: null,
				unavailableReason: null),
			Variant("MAT_DEFAULT_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND", reviewedDecodeHash: "f7b4b0fe70f7", MirrorVertexColors.None,
				Ch(MirrorChannel.Zero, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_DEFAULT_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST depth:generic shadow:generic", reviewedDecodeHash: "994ec483c0f8",
				MirrorVertexColors.None, Ch(MirrorChannel.Zero, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS", revision: 1, layoutKey: "Base AO ROUGHNESS", reviewedDecodeHash: "ae8f407f8c00", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero), unavailableReason: null),

			// BaseMaterialFunc не читает AO при ALPHABLEND: B — только альфа (подсказка rendercore «ao and alpha» устарела).
			Variant("MAT_ROUGHNESS_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND AO ROUGHNESS", reviewedDecodeHash: "f5d976a5eacd",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST AO ROUGHNESS depth:generic shadow:generic",
				reviewedDecodeHash: "b65b556fc272", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusionAlpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL", revision: 1, layoutKey: "Base AO NORMAL ROUGHNESS", reviewedDecodeHash: "af4f388cfded", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL_EMISSION", revision: 1, layoutKey: "Base AO EMISSION NORMAL ROUGHNESS", reviewedDecodeHash: "4016783f2d91",
				MirrorVertexColors.EmissionMultiplier, Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND NORMAL ROUGHNESS", reviewedDecodeHash: "bbe009805610",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST NORMAL ROUGHNESS depth:generic shadow:generic",
				reviewedDecodeHash: "7acc19d9baaa", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_METALNESS_NORMAL", revision: 1, layoutKey: "Base AO METALNESS NORMAL ROUGHNESS", reviewedDecodeHash: "2da9d53b278c",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),
			Variant("MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND METALNESS NORMAL ROUGHNESS",
				reviewedDecodeHash: "1ea8d2c2c751", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),

			// B — маска окраски u_vColor: из HDRP её не взять, Export from material пишет 0, маску автор рисует сам.
			Variant("MAT_COLORMASK_ROUGHNESS_METALNESS", revision: 1, layoutKey: "Base COLORMASK METALNESS ROUGHNESS", reviewedDecodeHash: "28ce2a3515a3",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.ColorMask, MirrorChannel.Zero),
				unavailableReason: null),
			Variant("MAT_COLORMASK_ROUGHNESS_METALNESS_NORMAL", revision: 1, layoutKey: "Base COLORMASK METALNESS NORMAL ROUGHNESS",
				reviewedDecodeHash: "6d351be6cb92", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.ColorMask, MirrorChannel.Zero), unavailableReason: null),

			// Веса слоёв — из цвета вершин (rgb — маска, a — оттенок): без цвета у меша видно только четвёртый слой.
			Variant("MAT_LAYERS4", revision: 1, layoutKey: "Layered", reviewedDecodeHash: "c86c330d411d", MirrorVertexColors.LayerBlend,
				Ch(MirrorChannel.NormalX, MirrorChannel.NormalY, MirrorChannel.AmbientOcclusion, MirrorChannel.Roughness), unavailableReason: null),
			Variant("MAT_FOLIAGE_SIMPLE_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST AO FOLIAGE depth:generic shadow:generic",
				reviewedDecodeHash: "9c7ead25cb89", MirrorVertexColors.None,
				Ch(MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_FOLIAGE_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST AO FOLIAGE NORMAL ROUGHNESS depth:generic shadow:generic",
				reviewedDecodeHash: "0885199ed7d9", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.AmbientOcclusion, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_EMISSION", revision: 1, layoutKey: "Base AO EMISSION METALNESS NORMAL ROUGHNESS", reviewedDecodeHash: "29ab28dc2384",
				MirrorVertexColors.EmissionMultiplier, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),

			// Эмиссия дорожного знака видна только ночью при включённом свете (глобальная яркость игры).
			Variant("MAT_ROAD_SIGN_EMISSION", revision: 1, layoutKey: "Base AO EMISSION METALNESS NORMAL ROAD_SIGN_EMISSION ROUGHNESS",
				reviewedDecodeHash: "b81c1f6de961", MirrorVertexColors.EmissionMultiplier,
				Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_WINDOW", revision: 1, layoutKey: "Base AO EMISSION METALNESS NORMAL ROUGHNESS WINDOW", reviewedDecodeHash: "eac9e24de3f7",
				MirrorVertexColors.EmissionMultiplier, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),

			// Interior: каналы правилами не выводятся (InteriorMaterialFunc), только аннотация. Цвет вершин не читается: эмиссия окон — из
			// глобальной яркости и атласа u_sTexture1 в координатах комнаты.
			Variant("MAT_INTERIOR", revision: 1, layoutKey: "Interior EMISSION METALNESS NORMAL ROUGHNESS WINDOW", reviewedDecodeHash: "550745410d88",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.Zero, MirrorChannel.Zero),
				unavailableReason: NoSecondUv)
		};

		public static readonly IReadOnlyList<MirrorBakedContract> Contracts = new[]
		{
			Contract("MAT_DEFAULT", revision: 1, MirrorVertexColors.None, "u_sTexture=srgb"),
			Contract("MAT_DEFAULT_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Zero,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_DEFAULT_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Zero,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Roughness,Zero,AmbientOcclusion,Zero",
				"u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Roughness,Zero,Alpha,Zero",
				"u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_ALPHATEST", revision: 1, MirrorVertexColors.None,
				"u_sPackedMap=linear:Roughness,Zero,AmbientOcclusionAlpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,AmbientOcclusion,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL_EMISSION", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_METALNESS_NORMAL", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_COLORMASK_ROUGHNESS_METALNESS", revision: 1, MirrorVertexColors.None,
				"u_sPackedMap=linear:Roughness,Metalness,ColorMask,Zero", "u_sTexture=srgb"),
			Contract("MAT_COLORMASK_ROUGHNESS_METALNESS_NORMAL", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,ColorMask,Zero", "u_sTexture=srgb"),
			Contract("MAT_LAYERS4", revision: 1, MirrorVertexColors.LayerBlend, "u_sPackedMap=linear:NormalX,NormalY,AmbientOcclusion,Roughness",
				"u_sPackedMap1=linear:NormalX,NormalY,AmbientOcclusion,Roughness", "u_sPackedMap2=linear:NormalX,NormalY,AmbientOcclusion,Roughness",
				"u_sPackedMap3=linear:NormalX,NormalY,AmbientOcclusion,Roughness", "u_sTexture=srgb", "u_sTexture1=srgb", "u_sTexture2=srgb",
				"u_sTexture3=srgb"),
			Contract("MAT_FOLIAGE_SIMPLE_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Zero,AmbientOcclusion,Alpha,Zero",
				"u_sTexture=srgb"),
			Contract("MAT_FOLIAGE_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,AmbientOcclusion,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_EMISSION", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_ROAD_SIGN_EMISSION", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_WINDOW", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_INTERIOR", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,Zero,Zero", "u_sTexture=srgb", "u_sTexture1=srgb", "u_sTexture2=srgb")
		};

		/// <summary>
		/// Все предлагаемые варианты альфа-теста режутся общей веткой ALPHATEST: ни DepthOnly, ни ShadowCaster не объявляют их keyword'ы
		/// (ключ раскладки "depth:generic shadow:generic"). Проверено: вырез по packed B, порог фиксированный, вершинные потоки — UV0.
		/// </summary>
		public static readonly IReadOnlyList<MirrorPassReview> AlphaTestPasses = new[]
		{
			PassReview("DepthOnly", keywords: "ALPHATEST", reviewedHash: "ce5a1c54d109"),
			PassReview("ShadowCaster", keywords: "ALPHATEST", reviewedHash: "cee4cd902811")
		};

		public static readonly IReadOnlyList<MirrorSuggestion> Suggestions = new[]
		{
			Suggest(MirrorRenderType.AlphaTest, hasNormal: true, hasEmission: false, "MAT_ROUGHNESS_NORMAL_ALPHATEST"),
			Suggest(MirrorRenderType.AlphaTest, hasNormal: false, hasEmission: false, "MAT_ROUGHNESS_NORMAL_ALPHATEST"),
			Suggest(MirrorRenderType.Transparent, hasNormal: true, hasEmission: false, "MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND"),
			Suggest(MirrorRenderType.Transparent, hasNormal: false, hasEmission: false, "MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND"),
			Suggest(MirrorRenderType.Opaque, hasNormal: true, hasEmission: true, "MAT_EMISSION"),
			Suggest(MirrorRenderType.Opaque, hasNormal: false, hasEmission: true, "MAT_EMISSION"),
			Suggest(MirrorRenderType.Opaque, hasNormal: true, hasEmission: false, "MAT_ROUGHNESS_METALNESS_NORMAL"),
			Suggest(MirrorRenderType.Opaque, hasNormal: false, hasEmission: false, "MAT_ROUGHNESS_METALNESS_NORMAL")
		};

		public static MirrorAnnotationSet Create()
		{
			return new MirrorAnnotationSet(Scope, SupportedStreams, ReviewedSetupHash, Variants, Contracts, Suggestions, AlphaTestPasses);
		}

		private static MirrorVariantAnnotation Variant(string name, int revision, string layoutKey, string reviewedDecodeHash, MirrorVertexColors vertexColors,
			MirrorChannel[] channels, string unavailableReason, params MirrorSlotOverride[] overrides)
		{
			return new MirrorVariantAnnotation(name, revision, layoutKey, reviewedDecodeHash, vertexColors, channels, unavailableReason, overrides);
		}

		private static MirrorChannel[] Ch(MirrorChannel r, MirrorChannel g, MirrorChannel b, MirrorChannel a)
		{
			return new[] { r, g, b, a };
		}

		/// <summary>Переопределение слота: только с причиной, понятной ревьюеру.</summary>
		private static MirrorSlotOverride Slot(string property, MirrorSource source, int layer, MirrorVector defaultValue, string reason)
		{
			return new MirrorSlotOverride(property, source, layer, defaultValue, reason);
		}

		private static MirrorBakedContract Contract(string variant, int revision, MirrorVertexColors vertexColors, params string[] textures)
		{
			return new MirrorBakedContract(variant, revision, vertexColors, textures);
		}

		private static MirrorPassReview PassReview(string pass, string keywords, string reviewedHash)
		{
			return new MirrorPassReview(pass, keywords, reviewedHash);
		}

		private static MirrorSuggestion Suggest(MirrorRenderType renderType, bool hasNormal, bool hasEmission, string variant)
		{
			return new MirrorSuggestion(renderType, hasNormal, hasEmission, variant);
		}
	}
}
