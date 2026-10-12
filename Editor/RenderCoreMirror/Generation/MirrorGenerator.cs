using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// generate(снимок, аннотации): строит данные ModRenderCoreVariantTable.g.cs и проверяет ворота. Rendercore владеет id, очередью, RenderType,
	/// keyword'ами и списком свойств с текстами; аннотации — тем, что запекается в опубликованные моды. Всё, что меняет запечённое без
	/// решения человека (раскладка, декодирование, вырез в DepthOnly/ShadowCaster, [Gamma], цвет вершин, потоки, имена RenderType, setup,
	/// контракт без новой ревизии), — Breaking, и таблица не строится (fail closed). Механические изменения (id, очередь, keyword'ы,
	/// добавленные свойства) просто попадают в таблицу.
	/// </summary>
	public static class MirrorGenerator
	{
		private const string PackedMapPrefix = "u_sPackedMap";

		public static MirrorGenerationResult Generate(RenderCoreLock lockData, MirrorAnnotationSet annotations)
		{
			var findings = new List<RenderCoreFinding>();
			var variants = new List<MirrorVariant>();
			var annotated = new HashSet<string>(StringComparer.Ordinal);
			var passUsers = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
			var alphaTestSources = new List<RenderCoreLockVariant>();

			if (!annotations.scope.SameAs(lockData.scope))
			{
				Add(findings, MirrorFindingCodes.Scope, RenderCoreFindingTier.Stale, "scope",
					$"снимок извлечён для области ({lockData.scope}), аннотации — для ({annotations.scope}): обновить снимок");
			}

			foreach (MirrorVariantAnnotation annotation in annotations.variants)
			{
				if (!annotated.Add(annotation.name))
				{
					Add(findings, MirrorFindingCodes.DuplicateAnnotation, RenderCoreFindingTier.Breaking, annotation.name, "вариант аннотирован дважды");
					continue;
				}

				if (!lockData.TryGetVariant(annotation.name, out RenderCoreLockVariant source))
				{
					Add(findings, MirrorFindingCodes.OfferedMissing, RenderCoreFindingTier.Breaking, annotation.name,
						"предлагаемого варианта нет в rendercore (переименован или удалён): перенести аннотацию на новое имя или удалить её" +
						DescribeRename(lockData, annotations, annotation));
					continue;
				}

				MirrorVariant variant = BuildVariant(lockData, source, annotation, annotations, findings);
				variants.Add(variant);
				CheckContract(variant, annotations.contracts, findings);
				CollectPassRequirements(source, passUsers);

				if (MirrorLayoutRules.IsAlphaTest(source))
				{
					alphaTestSources.Add(source);
				}
			}

			CheckPasses(lockData, passUsers, findings);
			CheckPassConfigs(lockData, annotations, alphaTestSources, findings);
			CheckRenderTypes(lockData, findings);
			CheckDuplicateIds(variants, findings);

			if (lockData.setupHash != annotations.reviewedSetupHash)
			{
				Add(findings, MirrorFindingCodes.Setup, RenderCoreFindingTier.Breaking, CSharpMethodExtractor.SetupMethodName,
					$"тело изменилось (проверено {annotations.reviewedSetupHash}, сейчас {lockData.setupHash}): сверить MaterialMappingPBR." +
					"ApplyRenderCoreVariant в CarX.Modding, затем обновить ReviewedSetupHash; изменённые строки — RCM150 при сравнении снимков");
			}

			CheckSuggestions(annotations, variants, findings);

			var notAnnotated = new List<string>();

			foreach (RenderCoreLockVariant candidate in lockData.variants.OrderBy(candidate => candidate.name, StringComparer.Ordinal))
			{
				if (annotated.Contains(candidate.name))
				{
					continue;
				}

				notAnnotated.Add(candidate.name);
				MirrorLayout layout = MirrorLayoutRules.Derive(lockData, candidate);
				Add(findings, MirrorFindingCodes.NotAnnotated, RenderCoreFindingTier.Info, candidate.name,
					"кандидат без аннотации, в таблицу не попадёт; чтобы предложить, проверить и дописать: " +
					MirrorAnnotationSnippets.FormatVariant(candidate.name, revision: 1, layout, candidate.decodeHash, annotations.supportedStreams,
						candidate.properties.Any(property => property.name.StartsWith(PackedMapPrefix, StringComparison.Ordinal))));
			}

			List<RenderCoreFinding> sorted = findings.OrderByDescending(finding => finding.tier).ThenBy(finding => finding.code, StringComparer.Ordinal)
				.ThenBy(finding => finding.subject, StringComparer.Ordinal).ToList();
			bool blocked = sorted.Any(finding => finding.tier >= RenderCoreFindingTier.Breaking);
			return new MirrorGenerationResult(sorted, blocked ? null : variants, annotations.suggestions, notAnnotated, RenderCoreLockText.ContentHash(lockData));
		}

		private static MirrorVariant BuildVariant(RenderCoreLock lockData, RenderCoreLockVariant source, MirrorVariantAnnotation annotation,
			MirrorAnnotationSet annotations, List<RenderCoreFinding> findings)
		{
			string name = source.name;
			MirrorLayout layout = MirrorLayoutRules.Derive(lockData, source);

			if (annotation.revision < 1)
			{
				Add(findings, MirrorFindingCodes.Revision, RenderCoreFindingTier.Breaking, name, $"ревизия {annotation.revision}, нужна ≥ 1");
			}

			if (source.label != name)
			{
				Add(findings, MirrorFindingCodes.LabelMismatch, RenderCoreFindingTier.Breaking, name,
					$"метка записи asset '{source.label}' не совпадает с именем enum: rendercore-редактор покажет одно, keyword будет другим");
			}

			if (source.entries > 1)
			{
				Add(findings, MirrorFindingCodes.DuplicateEntries, RenderCoreFindingTier.Info, name,
					$"в asset {source.entries} записи с id {source.id}; как и StandardShaderGUI, зеркало берёт первую");
			}

			bool knownRenderType = Enum.IsDefined(typeof(MirrorRenderType), source.renderType);
			bool alphaKeyword = source.keywords.Contains(RenderCoreLock.AlphaTestKeyword);

			if (!knownRenderType)
			{
				Add(findings, MirrorFindingCodes.AlphaTest, RenderCoreFindingTier.Breaking, name, $"RenderType {source.renderType} неизвестен");
			}
			else if (alphaKeyword != (source.renderType == (int)MirrorRenderType.AlphaTest))
			{
				Add(findings, MirrorFindingCodes.AlphaTest, RenderCoreFindingTier.Breaking, name,
					$"RenderType {(MirrorRenderType)source.renderType}, а keyword ALPHATEST {(alphaKeyword ? "есть" : "нет")}: очередь и вырез разойдутся");
			}

			if (!layout.knownFamily)
			{
				Add(findings, MirrorFindingCodes.UnknownFeature, RenderCoreFindingTier.Breaking, name,
					$"файл Defs '{source.defs}' неизвестен правилам раскладки (Base, Layered, Interior)");
			}

			foreach (string feature in layout.unknownFeatures)
			{
				Add(findings, MirrorFindingCodes.UnknownFeature, RenderCoreFindingTier.Breaking, name,
					$"{feature} неизвестен правилам: решить, что он делает с packed map, слотами и цветом вершин, и дописать MirrorLayoutRules");
			}

			if (layout.key != annotation.layoutKey)
			{
				Add(findings, MirrorFindingCodes.LayoutKey, RenderCoreFindingTier.Breaking, name,
					$"аннотация «{annotation.layoutKey}», rendercore «{layout.key}». Регрессия rendercore — сообщить и не обновлять; намеренно — " +
					"обновить ключ и каналы (смена запечённого — новая ревизия и контракт)");
			}

			if (source.decodeHash != annotation.reviewedDecodeHash)
			{
				Add(findings, MirrorFindingCodes.DecodeHash, RenderCoreFindingTier.Breaking, name,
					$"библиотеки Definitions или активные строки {source.func} изменились (проверено {annotation.reviewedDecodeHash}, сейчас {source.decodeHash}): " +
					"смысл тот же — обновить reviewedDecodeHash; другой — поправить каналы/цвет вершин и поднять ревизию. Отличия — RCM134/RCM135 при " +
					"сравнении снимков");
			}

			foreach (string stream in layout.unknownStreams)
			{
				Add(findings, MirrorFindingCodes.UnknownStream, RenderCoreFindingTier.Breaking, name, $"{stream} не переводится в вершинный поток");
			}

			string unavailableReason = string.IsNullOrEmpty(annotation.unavailableReason) ? null : annotation.unavailableReason;
			MirrorVertexStreams missing = layout.streams & ~annotations.supportedStreams;

			if (missing != MirrorVertexStreams.None && unavailableReason == null)
			{
				Add(findings, MirrorFindingCodes.Streams, RenderCoreFindingTier.Breaking, name,
					$"нужны потоки {missing}, которых нет у мешей модов ({annotations.supportedStreams}): вариант недоступен, укажите unavailableReason");
			}

			if (layout.vertexColors != annotation.vertexColors)
			{
				Add(findings, MirrorFindingCodes.VertexColors, RenderCoreFindingTier.Breaking, name,
					$"аннотация {annotation.vertexColors}, по define'ам {layout.vertexColors}; цвет вершин входит в контракт — нужна новая ревизия");
			}

			List<MirrorProperty> properties = BuildProperties(lockData, source, annotation, layout, findings);
			CheckChannels(name, annotation, layout, properties, findings);

			return new MirrorVariant(name, source.id, source.Queue, knownRenderType ? (MirrorRenderType)source.renderType : MirrorRenderType.Opaque,
				source.keywords.ToList(), annotation.vertexColors, layout.streams, unavailableReason, annotation.revision, properties);
		}

		private static List<MirrorProperty> BuildProperties(RenderCoreLock lockData, RenderCoreLockVariant source, MirrorVariantAnnotation annotation,
			MirrorLayout layout, List<RenderCoreFinding> findings)
		{
			var properties = new List<MirrorProperty>();
			bool emission = source.HasFeature(MirrorSlotRules.EmissionFeature);

			foreach (RenderCoreLockProperty property in source.properties)
			{
				string subject = source.name + "." + property.name;

				if (!MirrorSlotRules.TryMapAssetType(property.type, out MirrorPropertyType type))
				{
					Add(findings, MirrorFindingCodes.SlotType, RenderCoreFindingTier.Breaking, subject, $"тип {property.type} в asset не поддержан таблицей");
					continue;
				}

				lockData.TryGetShaderProperty(property.name, out RenderCoreLockShaderProperty shaderProperty);
				bool hasSlot = MirrorSlotRules.TryGetSlot(property.name, layout.family, emission, shaderProperty, out MirrorSlot slot, out string problem);
				bool hasOverride = annotation.TryGetOverride(property.name, out MirrorSlotOverride slotOverride);

				if (!hasSlot && !hasOverride)
				{
					Add(findings, MirrorFindingCodes.UnknownSlot, RenderCoreFindingTier.Breaking, subject,
						$"свойство ({type}) вне правил слотов: дописать MirrorSlotRules (и решить цветовое пространство) или переопределение с причиной");
					continue;
				}

				if (hasSlot && !hasOverride && problem != null)
				{
					Add(findings, MirrorFindingCodes.SlotType, RenderCoreFindingTier.Breaking, subject, problem);
				}

				if (hasSlot && slot.type != type)
				{
					Add(findings, MirrorFindingCodes.SlotType, RenderCoreFindingTier.Breaking, subject, $"тип в asset {type}, слот ждёт {slot.type} ({slot.rule})");
				}

				if (shaderProperty == null || shaderProperty.type == RenderCoreLockShaderProperty.AbsentType)
				{
					Add(findings, MirrorFindingCodes.SlotType, RenderCoreFindingTier.Breaking, subject, "свойства нет в Properties шейдера Standard");
				}
				else if (!MirrorSlotRules.IsShaderTypeCompatible(shaderProperty.type, type))
				{
					Add(findings, MirrorFindingCodes.SlotType, RenderCoreFindingTier.Breaking, subject,
						$"тип в Properties шейдера {shaderProperty.type} не подходит к {type}");
				}

				if (type == MirrorPropertyType.Texture && shaderProperty != null && shaderProperty.gamma != MirrorContracts.IsColorTexture(property.name))
				{
					Add(findings, MirrorFindingCodes.Gamma, RenderCoreFindingTier.Breaking, subject,
						$"[Gamma] {(shaderProperty.gamma ? "есть" : "нет")}, а замороженное правило префикса {MirrorContracts.ColorTexturePrefix} даёт " +
						$"{(MirrorContracts.IsColorTexture(property.name) ? MirrorContracts.Srgb : MirrorContracts.Linear)}: заголовки BinaryTexture " +
						"опубликованных модов разойдутся с шейдером; эскалировать в rendercore");
				}

				MirrorSource role = hasOverride ? slotOverride.source : slot.source;
				int layer = hasOverride ? slotOverride.layer : slot.layer;
				MirrorVector defaultValue = type == MirrorPropertyType.Texture ? MirrorVector.One : hasOverride ? slotOverride.defaultValue : slot.defaultValue;

				if (hasOverride && !IsValidOverride(slotOverride, type, out string reason))
				{
					Add(findings, MirrorFindingCodes.Override, RenderCoreFindingTier.Breaking, subject, reason);
				}

				// Помощники таблицы: текстуры без подписей компонент, у Value слой всегда 0, каналы — только у packed map (Surface).
				IReadOnlyList<string> vectorLabels = type != MirrorPropertyType.Texture && property.vectorLabels.Count > 0 ? property.vectorLabels.ToList() : null;
				IReadOnlyList<MirrorChannel> channels = role == MirrorSource.Surface ? annotation.channels : null;
				properties.Add(new MirrorProperty(property.name, property.label, property.tooltip, type, role, layer, defaultValue, vectorLabels, channels));
			}

			foreach (MirrorSlotOverride slotOverride in annotation.overrides)
			{
				if (!source.TryGetProperty(slotOverride.property, out _))
				{
					Add(findings, MirrorFindingCodes.Override, RenderCoreFindingTier.Breaking, source.name + "." + slotOverride.property,
						"переопределение для свойства, которого у варианта нет: удалить");
				}
			}

			return properties;
		}

		private static bool IsValidOverride(MirrorSlotOverride slotOverride, MirrorPropertyType type, out string reason)
		{
			if (string.IsNullOrWhiteSpace(slotOverride.reason))
			{
				reason = "переопределение без причины";
				return false;
			}

			bool texture = type == MirrorPropertyType.Texture;

			if (!texture && (slotOverride.layer != 0 || slotOverride.source == MirrorSource.Surface || slotOverride.source == MirrorSource.Normal ||
				slotOverride.source == MirrorSource.Diffuse || slotOverride.source == MirrorSource.Emission))
			{
				reason = $"для {type} таблица выражает только слой 0 и источники None/EmissionParameters ({slotOverride.source}, слой {slotOverride.layer})";
				return false;
			}

			if (texture && (slotOverride.source == MirrorSource.EmissionParameters || slotOverride.layer < 0 || slotOverride.layer > 3))
			{
				reason = $"для текстуры источник {slotOverride.source} или слой {slotOverride.layer} невозможны";
				return false;
			}

			reason = null;
			return true;
		}

		private static void CheckChannels(string name, MirrorVariantAnnotation annotation, MirrorLayout layout, List<MirrorProperty> properties,
			List<RenderCoreFinding> findings)
		{
			bool hasPacked = properties.Any(property => property.source == MirrorSource.Surface);

			if (hasPacked && annotation.channels == null)
			{
				string derived = layout.channels == null ? "" : "; по define'ам " + MirrorAnnotationSnippets.FormatChannels(layout.channels);
				Add(findings, MirrorFindingCodes.ChannelsShape, RenderCoreFindingTier.Breaking, name, "у варианта есть packed map, а каналов в аннотации нет" + derived);
			}
			else if (!hasPacked && annotation.channels != null)
			{
				Add(findings, MirrorFindingCodes.ChannelsShape, RenderCoreFindingTier.Breaking, name, "каналы аннотированы, а packed map (u_sPackedMap*) у варианта нет");
			}
			else if (annotation.channels != null && annotation.channels.Count != 4)
			{
				Add(findings, MirrorFindingCodes.ChannelsShape, RenderCoreFindingTier.Breaking, name, $"каналов {annotation.channels.Count}, нужно 4 (R, G, B, A)");
			}
			else if (hasPacked && layout.channels != null && !annotation.channels.SequenceEqual(layout.channels))
			{
				Add(findings, MirrorFindingCodes.Channels, RenderCoreFindingTier.Breaking, name,
					$"аннотация {MirrorAnnotationSnippets.FormatChannels(annotation.channels)}, по define'ам {MirrorAnnotationSnippets.FormatChannels(layout.channels)}");
			}
		}

		private static void CheckContract(MirrorVariant variant, IReadOnlyList<MirrorBakedContract> contracts, List<RenderCoreFinding> findings)
		{
			MirrorBakedContract current = MirrorContracts.Derive(variant);
			List<MirrorBakedContract> frozen = contracts.Where(contract => contract.variant == variant.name && contract.revision == variant.revision).ToList();

			if (frozen.Count == 0)
			{
				Add(findings, MirrorFindingCodes.ContractMissing, RenderCoreFindingTier.Breaking, variant.name,
					$"нет контракта ревизии {variant.revision}; проверить и дописать: {MirrorAnnotationSnippets.FormatContract(current)}");
				return;
			}

			if (frozen.Count > 1)
			{
				Add(findings, MirrorFindingCodes.ContractDuplicate, RenderCoreFindingTier.Breaking, variant.name, $"контрактов ревизии {variant.revision}: {frozen.Count}");
				return;
			}

			MirrorBakedContract contract = frozen[0];
			string bump = $"; поднять ревизию до {variant.revision + 1} и дописать " +
				MirrorAnnotationSnippets.FormatContract(new MirrorBakedContract(variant.name, variant.revision + 1, current.vertexColors, current.textures));

			if (contract.vertexColors != current.vertexColors)
			{
				Add(findings, MirrorFindingCodes.ContractChanged, RenderCoreFindingTier.Breaking, variant.name,
					$"цвет вершин {contract.vertexColors} → {current.vertexColors} при ревизии {variant.revision}" + bump);
			}

			foreach (string texture in contract.textures)
			{
				string property = MirrorBakedContract.GetProperty(texture);

				if (!current.TryGetTexture(property, out string now))
				{
					Add(findings, MirrorFindingCodes.ContractPropertyRemoved, RenderCoreFindingTier.Breaking, variant.name + "." + property,
						$"свойство из контракта ревизии {variant.revision} пропало ({texture})" + bump);
				}
				else if (now != texture)
				{
					Add(findings, MirrorFindingCodes.ContractChanged, RenderCoreFindingTier.Breaking, variant.name + "." + property,
						$"{texture} → {now} при ревизии {variant.revision}" + bump);
				}
			}
		}

		/// <summary>Какие объявления нужны: keyword варианта и additionalKeywords — в Forward, ALPHATEST — в DepthOnly и ShadowCaster.</summary>
		private static void CollectPassRequirements(RenderCoreLockVariant source, SortedDictionary<string, List<string>> passUsers)
		{
			AddPassUser(passUsers, RenderCoreLock.ForwardPass, source.name, source.name);

			foreach (string keyword in source.keywords)
			{
				if (keyword == RenderCoreLock.AlphaTestKeyword)
				{
					AddPassUser(passUsers, MirrorLayoutRules.DepthPass, keyword, source.name);
					AddPassUser(passUsers, MirrorLayoutRules.ShadowPass, keyword, source.name);
				}
				else
				{
					AddPassUser(passUsers, RenderCoreLock.ForwardPass, keyword, source.name);
				}
			}
		}

		private static void CheckPasses(RenderCoreLock lockData, SortedDictionary<string, List<string>> passUsers, List<RenderCoreFinding> findings)
		{
			var keywords = new SortedSet<string>(StringComparer.Ordinal);

			foreach (KeyValuePair<string, List<string>> entry in passUsers)
			{
				SplitPassKey(entry.Key, out string pass, out string keyword);
				keywords.Add(keyword);
				string kind = lockData.GetPassKind(pass, keyword);

				if (!kind.StartsWith("multi_compile", StringComparison.Ordinal))
				{
					Add(findings, MirrorFindingCodes.PassKind, RenderCoreFindingTier.Breaking, $"{pass}/{keyword}",
						$"объявлен как '{kind}', нужен безусловный multi_compile*: материал мода, собранный в рантайме, иначе останется без варианта " +
						$"({string.Join(", ", entry.Value)})");
				}
			}

			// shader_feature* вырезается, если ни один материал сборки его не включает, — нельзя ни в одном проходе.
			foreach (string pass in RenderCoreLock.TrackedPasses)
			{
				foreach (string keyword in keywords)
				{
					string kind = lockData.GetPassKind(pass, keyword);

					if (kind.Contains("shader_feature") && !passUsers.ContainsKey(PassKey(pass, keyword)))
					{
						Add(findings, MirrorFindingCodes.PassKind, RenderCoreFindingTier.Breaking, $"{pass}/{keyword}",
							$"объявлен как '{kind}': вариант может быть вырезан из сборки");
					}
				}
			}
		}

		/// <summary>
		/// Вырез вариантов альфа-теста делают DepthOnly и ShadowCaster (Forward рисует с ZTest Equal): то, что они компилируют для keyword'ов
		/// варианта, должно совпадать с проверенным (MirrorPassReview). Одна находка на конфигурацию, со списком вариантов.
		/// </summary>
		private static void CheckPassConfigs(RenderCoreLock lockData, MirrorAnnotationSet annotations, List<RenderCoreLockVariant> alphaTestSources,
			List<RenderCoreFinding> findings)
		{
			var users = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
			var used = new HashSet<string>(StringComparer.Ordinal);

			foreach (RenderCoreLockVariant source in alphaTestSources)
			{
				foreach (string pass in RenderCoreLock.AlphaTestPasses)
				{
					AddPassUser(users, pass, lockData.GetPassConfigKeywords(pass, source), source.name);
				}
			}

			foreach (KeyValuePair<string, List<string>> entry in users)
			{
				SplitPassKey(entry.Key, out string pass, out string keywords);
				used.Add(entry.Key);
				string variantList = string.Join(", ", entry.Value);

				if (!lockData.TryGetPassConfig(pass, keywords, out RenderCoreLockPassConfig config))
				{
					Add(findings, MirrorFindingCodes.PassConfig, RenderCoreFindingTier.Breaking, entry.Key,
						$"в снимке нет конфигурации прохода выреза (проход пропал?): {variantList} останутся без выреза");
					continue;
				}

				if (!annotations.TryGetPassReview(pass, keywords, out MirrorPassReview review))
				{
					Add(findings, MirrorFindingCodes.PassConfig, RenderCoreFindingTier.Breaking, entry.Key,
						$"конфигурация прохода выреза не проверена ({variantList}): проверить Defs, особенности, потоки и Func этого прохода в rendercore " +
						"и дописать " + MirrorAnnotationSnippets.FormatPassReview(pass, keywords, config.ReviewHash));
					continue;
				}

				if (review.reviewedHash != config.ReviewHash)
				{
					Add(findings, MirrorFindingCodes.PassConfig, RenderCoreFindingTier.Breaking, entry.Key,
						$"вырез изменился (проверено {review.reviewedHash}, сейчас {config.ReviewHash}; {variantList}): смысл тот же — обновить reviewedHash; " +
						"другой (канал, порог, потоки) — поправить каналы и поднять ревизию. Отличия — RCM141 при сравнении снимков");
				}
			}

			foreach (MirrorPassReview review in annotations.passReviews)
			{
				if (!used.Contains(PassKey(review.pass, review.keywords)))
				{
					Add(findings, MirrorFindingCodes.PassConfig, RenderCoreFindingTier.Info, PassKey(review.pass, review.keywords),
						"проверка конфигурации прохода не используется ни одним аннотированным вариантом альфа-теста: удалить");
				}
			}
		}

		/// <summary>
		/// SetupMaterialVariant пишет тег RenderType именем члена rendercore RenderType, клиент — именем ModRenderCoreRenderType (= MirrorRenderType):
		/// имена и числа должны совпадать полностью.
		/// </summary>
		private static void CheckRenderTypes(RenderCoreLock lockData, List<RenderCoreFinding> findings)
		{
			List<string> expected = Enum.GetValues(typeof(MirrorRenderType)).Cast<MirrorRenderType>()
				.Select(value => $"{value}={(int)value}").OrderBy(text => text, StringComparer.Ordinal).ToList();
			List<string> actual = lockData.renderTypes.Select(entry => $"{entry.name}={entry.id}").OrderBy(text => text, StringComparer.Ordinal).ToList();

			if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
			{
				Add(findings, MirrorFindingCodes.RenderTypeNames, RenderCoreFindingTier.Breaking, MaterialVariantEnumParser.RenderTypeEnumName,
					$"rendercore [{string.Join(", ", actual)}], зеркало [{string.Join(", ", expected)}]: тег RenderType материалов rendercore и модов " +
					"разойдётся — поправить MirrorRenderType, ModRenderCoreRenderType и клиент");
			}
		}

		private static void CheckDuplicateIds(List<MirrorVariant> variants, List<RenderCoreFinding> findings)
		{
			foreach (IGrouping<int, MirrorVariant> group in variants.GroupBy(variant => variant.id).Where(group => group.Count() > 1))
			{
				Add(findings, MirrorFindingCodes.DuplicateId, RenderCoreFindingTier.Error, string.Join(",", group.Select(variant => variant.name)),
					$"один id {group.Key} у нескольких вариантов таблицы: materialVariant материала станет неоднозначным");
			}
		}

		private static void CheckSuggestions(MirrorAnnotationSet annotations, List<MirrorVariant> variants, List<RenderCoreFinding> findings)
		{
			foreach (MirrorSuggestion suggestion in annotations.suggestions)
			{
				MirrorVariant variant = variants.FirstOrDefault(candidate => candidate.name == suggestion.variant);

				if (variant == null || !variant.Available)
				{
					Add(findings, MirrorFindingCodes.Suggestion, RenderCoreFindingTier.Breaking,
						$"{suggestion.renderType}/normal={suggestion.hasNormal}/emission={suggestion.hasEmission}",
						$"подсказка ведёт на {suggestion.variant}, которого нет среди доступных вариантов таблицы");
				}
			}
		}

		private static string DescribeRename(RenderCoreLock lockData, MirrorAnnotationSet annotations, MirrorVariantAnnotation annotation)
		{
			foreach (RenderCoreLockVariant candidate in lockData.variants)
			{
				if (annotations.TryGetVariant(candidate.name, out _))
				{
					continue;
				}

				MirrorLayout layout = MirrorLayoutRules.Derive(lockData, candidate);

				if (layout.key == annotation.layoutKey && candidate.decodeHash == annotation.reviewedDecodeHash)
				{
					return $"; похоже на переименование в {candidate.name} (id {candidate.id}): тот же ключ раскладки и то же декодирование";
				}
			}

			return "";
		}

		private static void AddPassUser(SortedDictionary<string, List<string>> passUsers, string pass, string keyword, string variant)
		{
			string key = PassKey(pass, keyword);

			if (!passUsers.TryGetValue(key, out List<string> users))
			{
				users = new List<string>();
				passUsers.Add(key, users);
			}

			users.Add(variant);
		}

		private static string PassKey(string pass, string keyword)
		{
			return pass + "/" + keyword;
		}

		private static void SplitPassKey(string key, out string pass, out string keyword)
		{
			int separator = key.IndexOf('/');
			pass = key.Substring(0, separator);
			keyword = key.Substring(separator + 1);
		}

		private static void Add(List<RenderCoreFinding> findings, string code, RenderCoreFindingTier tier, string subject, string message)
		{
			findings.Add(new RenderCoreFinding(code, tier, subject, message));
		}
	}
}
