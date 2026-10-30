using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Конфигурация прохода выреза альфа-теста (DepthOnly/ShadowCaster) для набора keyword'ов материала (обычно общий ALPHATEST): что этот
	/// проход компилирует из Definitions/Functions (Defs, MATERIALFEATURE_*, потоки, библиотеки, отпечаток Func) и отпечаток программы
	/// прохода. Вырез мода в игре делают именно эти проходы, поэтому ReviewHash сверяется с проверенным в аннотациях (RCM045).
	/// </summary>
	public sealed class RenderCoreLockPassConfig
	{
		public readonly string pass;

		/// <summary>keyword'ы материала, которые объявляет проход, через '+' (ordinal), или RenderCoreLock.NoKeywords.</summary>
		public readonly string keywords;

		public readonly string defs;
		public readonly IReadOnlyList<string> features;

		/// <summary>Прочие define'ы (справка, в ReviewHash не входят: их влияние на Func видно по decodeHash).</summary>
		public readonly IReadOnlyList<string> otherDefines;

		public readonly IReadOnlyList<string> streams;
		public readonly IReadOnlyList<string> libraries;
		public readonly string func;
		public readonly string decodeHash;
		public readonly string programHash;

		public RenderCoreLockPassConfig(string pass, string keywords, string defs, IReadOnlyList<string> features, IReadOnlyList<string> otherDefines,
			IReadOnlyList<string> streams, IReadOnlyList<string> libraries, string func, string decodeHash, string programHash)
		{
			this.pass = pass;
			this.keywords = keywords;
			this.defs = defs;
			this.features = features;
			this.otherDefines = otherDefines;
			this.streams = streams;
			this.libraries = libraries;
			this.func = func;
			this.decodeHash = decodeHash;
			this.programHash = programHash;
		}

		/// <summary>То, что проверяет человек: всё, кроме прочих define'ов.</summary>
		public IReadOnlyList<string> ReviewLines => new[]
		{
			"pass=" + pass, "keywords=" + keywords, "defs=" + defs, "features=" + string.Join(",", features), "streams=" + string.Join(",", streams),
			"libs=" + string.Join(",", libraries), "func=" + func, "decode=" + decodeHash, "program=" + programHash
		};

		/// <summary>12 hex: отпечаток ReviewLines, сверяется с MirrorPassReview.reviewedHash.</summary>
		public string ReviewHash => RenderCoreText.Hash(ReviewLines);
	}
}
