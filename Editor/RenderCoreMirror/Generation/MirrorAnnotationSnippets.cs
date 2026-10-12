using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Строки C# для RenderCoreMirrorAnnotations, которые генератор печатает в находках: аннотация нового кандидата (выведенные ключ,
	/// отпечаток, каналы, цвет вершин), контракт новой ревизии и проверка конфигурации прохода выреза. Человек проверяет их и переносит
	/// в аннотации.
	/// </summary>
	public static class MirrorAnnotationSnippets
	{
		/// <summary>hasPackedMap — у кандидата есть u_sPackedMap*: только тогда предлагаются выведенные каналы.</summary>
		public static string FormatVariant(string name, int revision, MirrorLayout layout, string decodeHash, MirrorVertexStreams supportedStreams,
			bool hasPackedMap)
		{
			string channels = layout.channels == null || !hasPackedMap ? "channels: null" : FormatChannels(layout.channels);
			bool unsupported = (layout.streams & ~supportedStreams) != MirrorVertexStreams.None;
			string reason = unsupported ? $"unavailableReason: \"нужны потоки {layout.streams & ~supportedStreams}\"" : "unavailableReason: null";
			return $"Variant({Quote(name)}, revision: {revision}, layoutKey: {Quote(layout.key)}, reviewedDecodeHash: {Quote(decodeHash)}, " +
				$"MirrorVertexColors.{layout.vertexColors}, {channels}, {reason})";
		}

		public static string FormatContract(MirrorBakedContract contract)
		{
			var builder = new StringBuilder();
			builder.Append("Contract(").Append(Quote(contract.variant)).Append(", revision: ").Append(contract.revision).Append(", MirrorVertexColors.")
				.Append(contract.vertexColors);

			foreach (string texture in contract.textures)
			{
				builder.Append(", ").Append(Quote(texture));
			}

			return builder.Append(')').ToString();
		}

		public static string FormatPassReview(string pass, string keywords, string reviewedHash)
		{
			return $"PassReview({Quote(pass)}, keywords: {Quote(keywords)}, reviewedHash: {Quote(reviewedHash)})";
		}

		public static string FormatChannels(IReadOnlyList<MirrorChannel> channels)
		{
			return "Ch(" + string.Join(", ", channels.Select(channel => "MirrorChannel." + channel)) + ")";
		}

		private static string Quote(string value)
		{
			return MirrorCSharpWriter.Literal(value);
		}
	}
}
