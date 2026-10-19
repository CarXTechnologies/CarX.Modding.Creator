using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Сравнивает закоммиченный снимок с живым и раскладывает отличия по уровням (provenance не сравнивается):
	/// Info — тексты, прочие define'ы, литералы по умолчанию, пути, имя файла Func, новые кандидаты, порядок свойств;
	/// Stale — id, очередь, RenderType варианта, keyword'ы, свойства (добавлены, убраны, тип), потоки, тип в Properties, [Gamma] не текстуры,
	/// объявления keyword'ов в проходах, область;
	/// Breaking — пропавший вариант, метка ≠ enum, defs/features, декодирование и библиотеки Definitions, [Gamma] текстуры, конфигурация
	/// проходов выреза, члены enum RenderType, setup.
	/// Breaking — ровно то, что не пропускают и ворота генератора (код ворот — в RenderCoreFindingCodes): Update Lock решает по воротам, и
	/// снимок, записанный им, не должен быть Breaking для Check/Validation/проверки сборки. То, что ворота проверяют частично (потоки,
	/// объявления keyword'ов, типы), — Stale, а опасную часть Check показывает находками ворот.
	/// Изменения вариантов вне relevant (не предлагаемых) — всегда Info. relevant == null — значимы все кандидаты закоммиченного снимка.
	/// </summary>
	public static class RenderCoreLockDiff
	{
		private const int MaxDiffLines = 40;
		private const string TextureShaderType = "2D";

		public static List<RenderCoreFinding> Compare(RenderCoreLock committed, RenderCoreLock live, ICollection<string> relevant)
		{
			var findings = new List<RenderCoreFinding>();
			Func<string, bool> isRelevant = name => relevant == null ? committed.TryGetVariant(name, out _) : relevant.Contains(name);

			if (!committed.scope.SameAs(live.scope))
			{
				findings.Add(new RenderCoreFinding(RenderCoreFindingCodes.Scope, RenderCoreFindingTier.Stale, "scope",
					$"lock scope ({committed.scope}) does not match the extraction scope ({live.scope})"));
			}

			CompareVariants(committed, live, isRelevant, findings);
			CompareShaderProperties(committed, live, isRelevant, findings);
			ComparePasses(committed, live, isRelevant, findings);
			ComparePassConfigs(committed, live, isRelevant, findings);
			CompareRenderTypes(committed, live, findings);
			CompareSetup(committed, live, findings);
			CompareFiles(committed, live, findings);
			CompareEnum(committed, live, findings);

			return findings.OrderByDescending(finding => finding.tier).ThenBy(finding => finding.code, StringComparer.Ordinal)
				.ThenBy(finding => finding.subject, StringComparer.Ordinal).ToList();
		}

		private static void CompareVariants(RenderCoreLock committed, RenderCoreLock live, Func<string, bool> isRelevant, List<RenderCoreFinding> findings)
		{
			foreach (RenderCoreLockVariant before in committed.variants)
			{
				bool relevant = isRelevant(before.name);

				if (!live.TryGetVariant(before.name, out RenderCoreLockVariant after))
				{
					findings.Add(new RenderCoreFinding(RenderCoreFindingCodes.VariantRemoved, Tier(relevant, RenderCoreFindingTier.Breaking), before.name,
						"candidate is gone from rendercore (no enum member or asset data)" + DescribeRename(before, live)));
					continue;
				}

				CompareVariant(before, after, relevant, committed, live, findings);
			}

			foreach (RenderCoreLockVariant after in live.variants)
			{
				if (!committed.TryGetVariant(after.name, out _))
				{
					findings.Add(new RenderCoreFinding(RenderCoreFindingCodes.VariantAdded, RenderCoreFindingTier.Info, after.name,
						$"new candidate (id {after.id}, {after.defs} {string.Join(" ", after.features)}); not added to the table without an annotation"));
				}
			}
		}

		private static void CompareVariant(RenderCoreLockVariant before, RenderCoreLockVariant after, bool relevant, RenderCoreLock committed, RenderCoreLock live,
			List<RenderCoreFinding> findings)
		{
			string name = before.name;

			if (before.id != after.id)
			{
				Add(findings, RenderCoreFindingCodes.VariantId, Tier(relevant, RenderCoreFindingTier.Stale), name, $"id {before.id} → {after.id}");
			}

			if (before.queueBase != after.queueBase || before.queueOffset != after.queueOffset)
			{
				Add(findings, RenderCoreFindingCodes.VariantQueue, Tier(relevant, RenderCoreFindingTier.Stale), name,
					$"queue {before.queueBase}+{before.queueOffset} → {after.queueBase}+{after.queueOffset}");
			}

			if (before.renderType != after.renderType)
			{
				Add(findings, RenderCoreFindingCodes.VariantRenderType, Tier(relevant, RenderCoreFindingTier.Stale), name, $"RenderType {before.renderType} → {after.renderType}");
			}

			if (!before.keywords.SequenceEqual(after.keywords, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.VariantKeywords, Tier(relevant, RenderCoreFindingTier.Stale), name,
					$"additionalKeywords [{string.Join(",", before.keywords)}] → [{string.Join(",", after.keywords)}]");
			}

			if (before.label != after.label)
			{
				bool mismatch = after.label != after.name;
				Add(findings, RenderCoreFindingCodes.VariantLabel, mismatch ? Tier(relevant, RenderCoreFindingTier.Breaking) : RenderCoreFindingTier.Info, name,
					mismatch ? $"asset entry label '{after.label}' does not match the enum name (was '{before.label}')" : $"label '{before.label}' → '{after.label}'");
			}

			if (before.entries != after.entries)
			{
				Add(findings, RenderCoreFindingCodes.VariantEntries, RenderCoreFindingTier.Info, name,
					$"asset entries with id {after.id}: {before.entries} → {after.entries} (the first one is used)");
			}

			CompareProperties(before, after, relevant, findings);

			if (before.defs != after.defs)
			{
				Add(findings, RenderCoreFindingCodes.Defs, Tier(relevant, RenderCoreFindingTier.Breaking), name, $"Definitions {before.defs} → {after.defs}");
			}

			if (!before.features.SequenceEqual(after.features, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.Features, Tier(relevant, RenderCoreFindingTier.Breaking), name, DescribeSet(before.features, after.features));
			}

			if (!before.otherDefines.SequenceEqual(after.otherDefines, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.OtherDefines, RenderCoreFindingTier.Info, name, DescribeSet(before.otherDefines, after.otherDefines));
			}

			if (!before.streams.SequenceEqual(after.streams, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.Streams, Tier(relevant, RenderCoreFindingTier.Stale), name, DescribeSet(before.streams, after.streams));
			}

			if (!before.libraries.SequenceEqual(after.libraries, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.Libraries, Tier(relevant, RenderCoreFindingTier.Breaking), name,
					"Definitions libraries: " + DescribeSet(before.libraries, after.libraries) + "; the mirror does not inspect them — check what they define");
			}

			if (before.decodeHash != after.decodeHash)
			{
				string header = before.func != after.func
					? $"Func {before.func} → {after.func}"
					: $"Definitions libraries or active lines of {after.func} changed ({before.decodeHash} → {after.decodeHash})";
				Add(findings, RenderCoreFindingCodes.Decode, Tier(relevant, RenderCoreFindingTier.Breaking), name,
					header + DescribeCode(committed.GetDecodeLines(before.decodeHash), live.GetDecodeLines(after.decodeHash)));
			}
			else if (before.func != after.func)
			{
				Add(findings, RenderCoreFindingCodes.Decode, RenderCoreFindingTier.Info, name, $"Func {before.func} → {after.func}, active lines unchanged");
			}
		}

		private static void CompareProperties(RenderCoreLockVariant before, RenderCoreLockVariant after, bool relevant, List<RenderCoreFinding> findings)
		{
			foreach (RenderCoreLockProperty property in before.properties)
			{
				string subject = $"{before.name}.{property.name}";

				if (!after.TryGetProperty(property.name, out RenderCoreLockProperty current))
				{
					Add(findings, RenderCoreFindingCodes.PropertyRemoved, Tier(relevant, RenderCoreFindingTier.Stale), subject, "property removed");
					continue;
				}

				if (property.type != current.type)
				{
					Add(findings, RenderCoreFindingCodes.PropertyType, Tier(relevant, RenderCoreFindingTier.Stale), subject, $"type {property.type} → {current.type}");
				}

				if (property.label != current.label || property.tooltip != current.tooltip ||
					!property.vectorLabels.SequenceEqual(current.vectorLabels, StringComparer.Ordinal))
				{
					Add(findings, RenderCoreFindingCodes.PropertyText, RenderCoreFindingTier.Info, subject, DescribeText(property, current));
				}
			}

			foreach (RenderCoreLockProperty property in after.properties)
			{
				if (!before.TryGetProperty(property.name, out _))
				{
					Add(findings, RenderCoreFindingCodes.PropertyAdded, Tier(relevant, RenderCoreFindingTier.Stale), $"{before.name}.{property.name}",
						$"property added (type {property.type})");
				}
			}

			List<string> commonBefore = before.properties.Select(property => property.name).Where(name => after.TryGetProperty(name, out _)).ToList();
			List<string> commonAfter = after.properties.Select(property => property.name).Where(name => before.TryGetProperty(name, out _)).ToList();

			if (!commonBefore.SequenceEqual(commonAfter, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.PropertyOrder, RenderCoreFindingTier.Info, before.name,
					$"property order {string.Join(",", commonBefore)} → {string.Join(",", commonAfter)}");
			}
		}

		private static void CompareShaderProperties(RenderCoreLock committed, RenderCoreLock live, Func<string, bool> isRelevant, List<RenderCoreFinding> findings)
		{
			foreach (RenderCoreLockShaderProperty before in committed.shaderProperties)
			{
				if (!live.TryGetShaderProperty(before.name, out RenderCoreLockShaderProperty after))
				{
					// Свойство больше никто из кандидатов не перечисляет: это уже видно по записям prop.
					continue;
				}

				bool relevant = UsedByRelevant(committed, before.name, isRelevant) || UsedByRelevant(live, before.name, isRelevant);

				if (before.type != after.type)
				{
					Add(findings, RenderCoreFindingCodes.ShaderPropertyType, Tier(relevant, RenderCoreFindingTier.Stale), before.name,
						$"type in Properties {before.type} → {after.type}");
				}

				if (before.gamma != after.gamma)
				{
					// Ворота сверяют [Gamma] текстур предлагаемых вариантов живого снимка с замороженным правилом префикса (RCM050).
					bool texture = after.type == TextureShaderType && UsedByRelevant(live, before.name, isRelevant);
					Add(findings, RenderCoreFindingCodes.ShaderPropertyGamma, texture ? RenderCoreFindingTier.Breaking : Tier(relevant, RenderCoreFindingTier.Stale),
						before.name, $"[Gamma] {(before.gamma ? "on" : "off")} → {(after.gamma ? "on" : "off")}" +
						(texture ? ": the texture color space is baked into mods" : ""));
				}

				if (before.defaultValue != after.defaultValue)
				{
					Add(findings, RenderCoreFindingCodes.ShaderPropertyDefault, RenderCoreFindingTier.Info, before.name,
						$"default {before.defaultValue} → {after.defaultValue}");
				}
			}
		}

		private static void ComparePasses(RenderCoreLock committed, RenderCoreLock live, Func<string, bool> isRelevant, List<RenderCoreFinding> findings)
		{
			var relevantKeywords = new HashSet<string>(StringComparer.Ordinal) { RenderCoreLock.AlphaTestKeyword };

			foreach (RenderCoreLockVariant variant in committed.variants.Where(variant => isRelevant(variant.name)))
			{
				relevantKeywords.Add(variant.name);
				relevantKeywords.UnionWith(variant.keywords);
			}

			foreach (RenderCoreLockPassKeyword before in committed.passKeywords)
			{
				string after = live.GetPassKind(before.pass, before.keyword);
				bool known = live.passKeywords.Any(entry => entry.pass == before.pass && entry.keyword == before.keyword);

				// Keyword пропал из снимка вместе с кандидатом — об этом уже сказано в RCM101.
				if (!known || after == before.kind)
				{
					continue;
				}

				Add(findings, RenderCoreFindingCodes.PassKind, Tier(relevantKeywords.Contains(before.keyword), RenderCoreFindingTier.Stale),
					$"{before.pass}/{before.keyword}", $"{before.kind} → {after}");
			}
		}

		/// <summary>
		/// Конфигурации проходов выреза: изменение той, которой рисуется предлагаемый вариант альфа-теста (в любом из снимков), — Breaking.
		/// Появление и пропажа конфигурации (вариант перешёл на свою ветку или общую) видны по RCM140 и ключу раскладки (RCM040).
		/// </summary>
		private static void ComparePassConfigs(RenderCoreLock committed, RenderCoreLock live, Func<string, bool> isRelevant, List<RenderCoreFinding> findings)
		{
			var relevantConfigs = new HashSet<string>(StringComparer.Ordinal);
			CollectRelevantConfigs(committed, isRelevant, relevantConfigs);
			CollectRelevantConfigs(live, isRelevant, relevantConfigs);

			foreach (RenderCoreLockPassConfig before in committed.passConfigs)
			{
				string subject = $"{before.pass}/{before.keywords}";

				if (!live.TryGetPassConfig(before.pass, before.keywords, out RenderCoreLockPassConfig after))
				{
					Add(findings, RenderCoreFindingCodes.PassConfig, RenderCoreFindingTier.Info, subject, "pass configuration no longer exists");
					continue;
				}

				string changes = DescribePassConfig(before, after);

				if (changes.Length > 0)
				{
					Add(findings, RenderCoreFindingCodes.PassConfig, Tier(relevantConfigs.Contains(subject), RenderCoreFindingTier.Breaking), subject,
						changes + DescribeCode(committed.GetDecodeLines(before.decodeHash), live.GetDecodeLines(after.decodeHash)));
				}
				else if (!before.otherDefines.SequenceEqual(after.otherDefines, StringComparer.Ordinal))
				{
					Add(findings, RenderCoreFindingCodes.PassConfig, RenderCoreFindingTier.Info, subject,
						"other defines " + DescribeSet(before.otherDefines, after.otherDefines));
				}
			}

			foreach (RenderCoreLockPassConfig after in live.passConfigs)
			{
				if (!committed.TryGetPassConfig(after.pass, after.keywords, out _))
				{
					Add(findings, RenderCoreFindingCodes.PassConfig, RenderCoreFindingTier.Info, $"{after.pass}/{after.keywords}", "new pass configuration");
				}
			}
		}

		private static void CollectRelevantConfigs(RenderCoreLock lockData, Func<string, bool> isRelevant, HashSet<string> configs)
		{
			foreach (RenderCoreLockVariant variant in lockData.variants)
			{
				if (!isRelevant(variant.name) || !RenderCoreLock.IsAlphaTest(variant.features, variant.keywords))
				{
					continue;
				}

				foreach (string pass in RenderCoreLock.AlphaTestPasses)
				{
					configs.Add($"{pass}/{lockData.GetPassConfigKeywords(pass, variant)}");
				}
			}
		}

		private static string DescribePassConfig(RenderCoreLockPassConfig before, RenderCoreLockPassConfig after)
		{
			var parts = new List<string>();

			if (before.defs != after.defs)
			{
				parts.Add($"Definitions {before.defs} → {after.defs}");
			}

			AddSetChange(parts, "features", before.features, after.features);
			AddSetChange(parts, "streams", before.streams, after.streams);
			AddSetChange(parts, "Definitions libraries", before.libraries, after.libraries);

			if (before.func != after.func)
			{
				parts.Add($"Func {before.func} → {after.func}");
			}

			if (before.decodeHash != after.decodeHash)
			{
				parts.Add($"Func active lines {before.decodeHash} → {after.decodeHash}");
			}

			if (before.programHash != after.programHash)
			{
				parts.Add($"pass program {before.programHash} → {after.programHash}");
			}

			return string.Join("; ", parts);
		}

		private static void AddSetChange(List<string> parts, string what, IReadOnlyList<string> before, IReadOnlyList<string> after)
		{
			if (!before.SequenceEqual(after, StringComparer.Ordinal))
			{
				parts.Add(what + " " + DescribeSet(before, after));
			}
		}

		/// <summary>Члены enum RenderType: SetupMaterialVariant пишет тег RenderType именем члена, клиент — именем ModRenderCoreRenderType.</summary>
		private static void CompareRenderTypes(RenderCoreLock committed, RenderCoreLock live, List<RenderCoreFinding> findings)
		{
			List<string> before = committed.renderTypes.Select(Describe).ToList();
			List<string> after = live.renderTypes.Select(Describe).ToList();

			if (!before.SequenceEqual(after, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.RenderTypes, RenderCoreFindingTier.Breaking, MaterialVariantEnumParser.RenderTypeEnumName,
					DescribeSet(before, after) + "; the material RenderType tag is written as the member name — check ModRenderCoreRenderType");
			}
		}

		private static void CompareSetup(RenderCoreLock committed, RenderCoreLock live, List<RenderCoreFinding> findings)
		{
			if (committed.setupHash != live.setupHash)
			{
				Add(findings, RenderCoreFindingCodes.Setup, RenderCoreFindingTier.Breaking, CSharpMethodExtractor.SetupMethodName,
					$"body changed ({committed.setupHash} → {live.setupHash}); check MaterialMappingPBR.ApplyRenderCoreVariant" +
					DescribeCode(committed.setupLines, live.setupLines));
			}
		}

		private static void CompareFiles(RenderCoreLock committed, RenderCoreLock live, List<RenderCoreFinding> findings)
		{
			foreach (RenderCoreLockFile before in committed.files)
			{
				if (!live.TryGetFile(before.role, out string path))
				{
					Add(findings, RenderCoreFindingCodes.Files, RenderCoreFindingTier.Info, before.role, $"file is no longer read ({before.path})");
				}
				else if (path != before.path)
				{
					Add(findings, RenderCoreFindingCodes.Files, RenderCoreFindingTier.Info, before.role, $"file moved: {before.path} → {path}");
				}
			}

			foreach (RenderCoreLockFile after in live.files)
			{
				if (!committed.TryGetFile(after.role, out _))
				{
					Add(findings, RenderCoreFindingCodes.Files, RenderCoreFindingTier.Info, after.role, $"new input file {after.path}");
				}
			}
		}

		private static void CompareEnum(RenderCoreLock committed, RenderCoreLock live, List<RenderCoreFinding> findings)
		{
			List<string> before = committed.enumEntries.Where(entry => !committed.TryGetVariant(entry.name, out _)).Select(Describe).ToList();
			List<string> after = live.enumEntries.Where(entry => !live.TryGetVariant(entry.name, out _)).Select(Describe).ToList();

			if (!before.SequenceEqual(after, StringComparer.Ordinal))
			{
				Add(findings, RenderCoreFindingCodes.EnumEntries, RenderCoreFindingTier.Info, MaterialVariantEnumParser.EnumName,
					"members without asset data: " + DescribeSet(before, after));
			}
		}

		private static string DescribeRename(RenderCoreLockVariant before, RenderCoreLock live)
		{
			foreach (RenderCoreLockVariant candidate in live.variants)
			{
				if (candidate.id == before.id && candidate.name != before.name)
				{
					bool sameLayout = candidate.defs == before.defs && candidate.features.SequenceEqual(before.features, StringComparer.Ordinal);
					bool sameDecode = candidate.decodeHash == before.decodeHash;
					return $"; same id now used by {candidate.name}: layout {(sameLayout ? "same" : "different")}, decoding {(sameDecode ? "same" : "different")}";
				}
			}

			return "";
		}

		private static bool UsedByRelevant(RenderCoreLock lockData, string property, Func<string, bool> isRelevant)
		{
			return lockData.variants.Any(variant => isRelevant(variant.name) && variant.TryGetProperty(property, out _));
		}

		private static string Describe(RenderCoreLockEnumEntry entry)
		{
			return $"{entry.name}={entry.id}";
		}

		private static string DescribeText(RenderCoreLockProperty before, RenderCoreLockProperty after)
		{
			var parts = new List<string>();

			if (before.label != after.label)
			{
				parts.Add($"label '{before.label}' → '{after.label}'");
			}

			if (before.tooltip != after.tooltip)
			{
				parts.Add($"tooltip '{before.tooltip}' → '{after.tooltip}'");
			}

			if (!before.vectorLabels.SequenceEqual(after.vectorLabels, StringComparer.Ordinal))
			{
				parts.Add($"vectorLabels [{string.Join("|", before.vectorLabels)}] → [{string.Join("|", after.vectorLabels)}]");
			}

			return string.Join("; ", parts);
		}

		private static string DescribeSet(IEnumerable<string> before, IEnumerable<string> after)
		{
			var beforeSet = new HashSet<string>(before, StringComparer.Ordinal);
			var afterSet = new HashSet<string>(after, StringComparer.Ordinal);
			List<string> removed = beforeSet.Where(item => !afterSet.Contains(item)).OrderBy(item => item, StringComparer.Ordinal).ToList();
			List<string> added = afterSet.Where(item => !beforeSet.Contains(item)).OrderBy(item => item, StringComparer.Ordinal).ToList();
			var parts = new List<string>();

			if (removed.Count > 0)
			{
				parts.Add("-" + string.Join(" -", removed));
			}

			if (added.Count > 0)
			{
				parts.Add("+" + string.Join(" +", added));
			}

			return parts.Count == 0 ? "order changed" : string.Join(" ", parts);
		}

		/// <summary>
		/// Разница строк кода rendercore. Закоммиченный снимок их не хранит (SDK публичный), тогда разница не строится:
		/// изменение смотрится через git diff rendercore между пином снимка и текущим.
		/// </summary>
		private static string DescribeCode(IReadOnlyList<string> before, IReadOnlyList<string> after)
		{
			if (before.Count == 0 || after.Count == 0)
			{
				return "; the lock does not store code lines — see git diff of rendercore between the lock pin and the current one";
			}

			return DescribeLines(before, after);
		}

		/// <summary>Построчный diff (LCS) для сообщений: "- было" / "+ стало", не больше MaxDiffLines строк.</summary>
		public static string DescribeLines(IReadOnlyList<string> before, IReadOnlyList<string> after)
		{
			int[,] lengths = new int[before.Count + 1, after.Count + 1];

			for (int i = before.Count - 1; i >= 0; i--)
			{
				for (int j = after.Count - 1; j >= 0; j--)
				{
					lengths[i, j] = before[i] == after[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
				}
			}

			var builder = new StringBuilder();
			int written = 0;
			int beforeIndex = 0;
			int afterIndex = 0;

			while ((beforeIndex < before.Count || afterIndex < after.Count) && written < MaxDiffLines)
			{
				if (beforeIndex < before.Count && afterIndex < after.Count && before[beforeIndex] == after[afterIndex])
				{
					beforeIndex++;
					afterIndex++;
				}
				else if (afterIndex < after.Count && (beforeIndex >= before.Count || lengths[beforeIndex, afterIndex + 1] >= lengths[beforeIndex + 1, afterIndex]))
				{
					builder.Append("\n    + ").Append(after[afterIndex++]);
					written++;
				}
				else
				{
					builder.Append("\n    - ").Append(before[beforeIndex++]);
					written++;
				}
			}

			if (written >= MaxDiffLines)
			{
				builder.Append("\n    …");
			}

			return builder.ToString();
		}

		private static RenderCoreFindingTier Tier(bool relevant, RenderCoreFindingTier tier)
		{
			return relevant ? tier : RenderCoreFindingTier.Info;
		}

		private static void Add(List<RenderCoreFinding> findings, string code, RenderCoreFindingTier tier, string subject, string message)
		{
			findings.Add(new RenderCoreFinding(code, tier, subject, message));
		}
	}
}
