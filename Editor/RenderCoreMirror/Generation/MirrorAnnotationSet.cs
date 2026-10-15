using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Аннотации как вход генератора. Закоммиченный набор — RenderCoreMirrorAnnotations.Create(); тесты собирают свои наборы (фикстуры,
	/// сценарии «правка каналов без поднятия ревизии» и т.п.) через конструктор и With*.
	/// </summary>
	public sealed class MirrorAnnotationSet
	{
		public readonly RenderCoreScope scope;

		/// <summary>Потоки, которые несут меши модов; вариант, которому нужно больше, обязан быть недоступным (с причиной).</summary>
		public readonly MirrorVertexStreams supportedStreams;

		/// <summary>Отпечаток тела EditorUtils.SetupMaterialVariant, с которым сверён MaterialMappingPBR.ApplyRenderCoreVariant клиента.</summary>
		public readonly string reviewedSetupHash;

		/// <summary>Предлагаемые варианты в порядке таблицы.</summary>
		public readonly IReadOnlyList<MirrorVariantAnnotation> variants;

		/// <summary>Замороженные контракты (вариант, ревизия); только дописываются.</summary>
		public readonly IReadOnlyList<MirrorBakedContract> contracts;

		/// <summary>Подсказки Export from material; ключ (RenderType, нормаль, эмиссия, растительность) уникален (RCM080).</summary>
		public readonly IReadOnlyList<MirrorSuggestion> suggestions;

		/// <summary>Проверенные конфигурации проходов выреза альфа-теста (DepthOnly/ShadowCaster).</summary>
		public readonly IReadOnlyList<MirrorPassReview> passReviews;

		public MirrorAnnotationSet(RenderCoreScope scope, MirrorVertexStreams supportedStreams, string reviewedSetupHash,
			IReadOnlyList<MirrorVariantAnnotation> variants, IReadOnlyList<MirrorBakedContract> contracts, IReadOnlyList<MirrorSuggestion> suggestions,
			IReadOnlyList<MirrorPassReview> passReviews)
		{
			this.scope = scope;
			this.supportedStreams = supportedStreams;
			this.reviewedSetupHash = reviewedSetupHash;
			this.variants = variants;
			this.contracts = contracts;
			this.suggestions = suggestions;
			this.passReviews = passReviews ?? Array.Empty<MirrorPassReview>();
		}

		/// <summary>Имена предлагаемых вариантов: «значимые» для сравнения снимков (RenderCoreLockDiff.Compare).</summary>
		public HashSet<string> GetVariantNames()
		{
			return new HashSet<string>(variants.Select(variant => variant.name), StringComparer.Ordinal);
		}

		public bool TryGetVariant(string name, out MirrorVariantAnnotation variant)
		{
			foreach (MirrorVariantAnnotation candidate in variants)
			{
				if (candidate.name == name)
				{
					variant = candidate;
					return true;
				}
			}

			variant = null;
			return false;
		}

		public bool TryGetPassReview(string pass, string keywords, out MirrorPassReview review)
		{
			foreach (MirrorPassReview candidate in passReviews)
			{
				if (candidate.pass == pass && candidate.keywords == keywords)
				{
					review = candidate;
					return true;
				}
			}

			review = null;
			return false;
		}

		/// <summary>Набор, где аннотация с тем же именем заменена (или дописана в конец, если её не было).</summary>
		public MirrorAnnotationSet WithVariant(MirrorVariantAnnotation variant)
		{
			var result = new List<MirrorVariantAnnotation>(variants);
			int index = result.FindIndex(candidate => candidate.name == variant.name);

			if (index < 0)
			{
				result.Add(variant);
			}
			else
			{
				result[index] = variant;
			}

			return new MirrorAnnotationSet(scope, supportedStreams, reviewedSetupHash, result, contracts, suggestions, passReviews);
		}

		public MirrorAnnotationSet WithContracts(IReadOnlyList<MirrorBakedContract> newContracts)
		{
			return new MirrorAnnotationSet(scope, supportedStreams, reviewedSetupHash, variants, newContracts, suggestions, passReviews);
		}

		public MirrorAnnotationSet WithSuggestions(IReadOnlyList<MirrorSuggestion> newSuggestions)
		{
			return new MirrorAnnotationSet(scope, supportedStreams, reviewedSetupHash, variants, contracts, newSuggestions, passReviews);
		}

		public MirrorAnnotationSet WithSetupHash(string setupHash)
		{
			return new MirrorAnnotationSet(scope, supportedStreams, setupHash, variants, contracts, suggestions, passReviews);
		}

		public MirrorAnnotationSet WithPassReviews(IReadOnlyList<MirrorPassReview> newPassReviews)
		{
			return new MirrorAnnotationSet(scope, supportedStreams, reviewedSetupHash, variants, contracts, suggestions, newPassReviews);
		}
	}
}
