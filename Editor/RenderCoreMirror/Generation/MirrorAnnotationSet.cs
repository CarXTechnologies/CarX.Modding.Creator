using System;
using System.Collections.Generic;
using System.Linq;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Annotations as generator input. The committed set is RenderCoreMirrorAnnotations.Create(); tests build their own sets (fixtures,
	/// "channels changed without a revision bump" scenarios, etc.) via the constructor and With*.
	/// </summary>
	public sealed class MirrorAnnotationSet
	{
		public readonly RenderCoreScope scope;

		/// <summary>Streams carried by mod meshes; a variant that needs more must be unavailable (with a reason).</summary>
		public readonly MirrorVertexStreams supportedStreams;

		/// <summary>Fingerprint of the EditorUtils.SetupMaterialVariant body the client's MaterialMappingPBR.ApplyRenderCoreVariant was checked against.</summary>
		public readonly string reviewedSetupHash;

		/// <summary>Offered variants in table order.</summary>
		public readonly IReadOnlyList<MirrorVariantAnnotation> variants;

		/// <summary>Frozen (variant, revision) contracts; append only.</summary>
		public readonly IReadOnlyList<MirrorBakedContract> contracts;

		/// <summary>Export from material suggestions; the key (RenderType, normal, emission, foliage) is unique (RCM080).</summary>
		public readonly IReadOnlyList<MirrorSuggestion> suggestions;

		/// <summary>Reviewed alpha-test cutout pass configs (DepthOnly/ShadowCaster).</summary>
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

		/// <summary>Offered variant names: the "significant" ones for snapshot comparison (RenderCoreLockDiff.Compare).</summary>
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

		/// <summary>A set where the annotation with the same name is replaced (or appended if it was missing).</summary>
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
