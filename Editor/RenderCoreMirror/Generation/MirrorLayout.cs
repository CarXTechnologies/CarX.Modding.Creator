using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Что правила раскладки выводят из снимка для одного кандидата (MirrorLayoutRules.Derive).</summary>
	public sealed class MirrorLayout
	{
		/// <summary>Семейство Defs: имя файла без "MaterialDefs" ("Base", "Layered", "Interior"); несколько — через '+'.</summary>
		public readonly string family;

		/// <summary>Человекочитаемый ключ раскладки, который сверяется с аннотацией.</summary>
		public readonly string key;

		/// <summary>Каналы packed map по define'ам (Base, Layered) или null, если семейство размечается только аннотацией (Interior) или неизвестно.</summary>
		public readonly IReadOnlyList<MirrorChannel> channels;

		public readonly MirrorVertexColors vertexColors;

		/// <summary>Position плюс потоки из INPUT_NEED_*.</summary>
		public readonly MirrorVertexStreams streams;

		/// <summary>MATERIALFEATURE_*, о которых правила ничего не знают.</summary>
		public readonly IReadOnlyList<string> unknownFeatures;

		/// <summary>INPUT_NEED_*, которые не переводятся в поток.</summary>
		public readonly IReadOnlyList<string> unknownStreams;

		/// <summary>Семейство Defs известно правилам (Base, Layered, Interior).</summary>
		public readonly bool knownFamily;

		public MirrorLayout(string family, string key, IReadOnlyList<MirrorChannel> channels, MirrorVertexColors vertexColors, MirrorVertexStreams streams,
			IReadOnlyList<string> unknownFeatures, IReadOnlyList<string> unknownStreams, bool knownFamily)
		{
			this.family = family;
			this.key = key;
			this.channels = channels;
			this.vertexColors = vertexColors;
			this.streams = streams;
			this.unknownFeatures = unknownFeatures;
			this.unknownStreams = unknownStreams;
			this.knownFamily = knownFamily;
		}
	}
}
