using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Замороженный контракт (вариант, ревизия): как клиент читает уже опубликованные текстуры документа (цветовое пространство, раскладка
	/// packed map, кодировка нормалей) и цвет вершин. Записи только дописываются: изменить смысл — значит поднять ревизию и добавить новую
	/// запись (MirrorFindingCodes.ContractChanged). Свойства вне контракта (добавленные позже) ревизию не требуют.
	/// </summary>
	public sealed class MirrorBakedContract
	{
		public readonly string variant;
		public readonly int revision;
		public readonly MirrorVertexColors vertexColors;

		/// <summary>"&lt;свойство&gt;=&lt;srgb|linear&gt;[:&lt;R,G,B,A&gt;|:normal]", по имени свойства (ordinal).</summary>
		public readonly IReadOnlyList<string> textures;

		public MirrorBakedContract(string variant, int revision, MirrorVertexColors vertexColors, IEnumerable<string> textures)
		{
			this.variant = variant;
			this.revision = revision;
			this.vertexColors = vertexColors;
			this.textures = textures.OrderBy(GetProperty, StringComparer.Ordinal).ThenBy(texture => texture, StringComparer.Ordinal).ToList();
		}

		/// <summary>Одна строка: "MAT_X@1 vertexColors=None u_sPackedMap=linear:Roughness,Zero,Alpha,Zero u_sTexture=srgb".</summary>
		public string Text => MirrorContracts.Format(variant, revision, vertexColors, textures);

		/// <summary>Имя свойства записи textures (до '=').</summary>
		public static string GetProperty(string texture)
		{
			int separator = texture.IndexOf('=');
			return separator < 0 ? texture : texture.Substring(0, separator);
		}

		public bool TryGetTexture(string property, out string texture)
		{
			foreach (string candidate in textures)
			{
				if (GetProperty(candidate) == property)
				{
					texture = candidate;
					return true;
				}
			}

			texture = null;
			return false;
		}
	}
}
