using System;
using System.Globalization;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Значение по умолчанию свойства (x, y, z, w) без UnityEngine; в таблицу пишется как new Vector4(...).</summary>
	public readonly struct MirrorVector : IEquatable<MirrorVector>
	{
		public static readonly MirrorVector One = new MirrorVector(1, 1, 1, 1);

		public readonly float x;
		public readonly float y;
		public readonly float z;
		public readonly float w;

		public MirrorVector(float x, float y, float z, float w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		/// <summary>
		/// Литерал из Properties шейдера: "(1,1,1,1)" (меньше четырёх компонент — остальные 0) или число "1" (в x, как у Float в
		/// ModRenderCoreVariantProperty). Пробелы допускаются.
		/// </summary>
		public static bool TryParse(string literal, out MirrorVector value)
		{
			value = default;

			if (string.IsNullOrWhiteSpace(literal))
			{
				return false;
			}

			string text = literal.Trim();

			if (text.StartsWith("(", StringComparison.Ordinal) != text.EndsWith(")", StringComparison.Ordinal))
			{
				return false;
			}

			if (text.StartsWith("(", StringComparison.Ordinal))
			{
				text = text.Substring(1, text.Length - 2);
			}

			string[] parts = text.Split(',');

			if (parts.Length == 0 || parts.Length > 4)
			{
				return false;
			}

			float[] components = new float[4];

			for (int index = 0; index < parts.Length; index++)
			{
				if (!float.TryParse(parts[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out components[index]) ||
					float.IsNaN(components[index]) || float.IsInfinity(components[index]))
				{
					return false;
				}
			}

			value = new MirrorVector(components[0], components[1], components[2], components[3]);
			return true;
		}

		/// <summary>Компонента литералом C#: целое без суффикса ("1"), иначе кратчайшее "R" с суффиксом f ("0.5f").</summary>
		public static string FormatComponent(float value)
		{
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				throw new ArgumentOutOfRangeException(nameof(value), value, "в таблицу пишутся только конечные числа");
			}

			if (value == Math.Floor(value) && Math.Abs(value) < 1e7f)
			{
				return ((long)value).ToString(CultureInfo.InvariantCulture);
			}

			return value.ToString("R", CultureInfo.InvariantCulture) + "f";
		}

		public bool Equals(MirrorVector other)
		{
			return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z) && w.Equals(other.w);
		}

		public override bool Equals(object obj)
		{
			return obj is MirrorVector other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = x.GetHashCode();
				hash = hash * 397 ^ y.GetHashCode();
				hash = hash * 397 ^ z.GetHashCode();
				return hash * 397 ^ w.GetHashCode();
			}
		}

		/// <summary>"(1, 0.5, 0, 1)": для дампов и сообщений.</summary>
		public override string ToString()
		{
			return "(" + Format(x) + ", " + Format(y) + ", " + Format(z) + ", " + Format(w) + ")";
		}

		private static string Format(float value)
		{
			return value.ToString("R", CultureInfo.InvariantCulture);
		}
	}
}
