using System;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Vendor agnostic identity of a published mod entry.
	/// Steam addresses items by <c>PublishedFileId</c> and mod.io by <c>ModioId</c>; both are stored here in their
	/// decimal string form so that the surrounding tooling never has to know which vendor produced the value.
	/// </summary>
	[Serializable]
	public struct ModItemKey : IEquatable<ModItemKey>
	{
		public string vendor;
		public string id;

		public ModItemKey(string vendor, string id)
		{
			this.vendor = vendor;
			this.id = id;
		}

		public bool IsValid => !string.IsNullOrWhiteSpace(vendor) && !string.IsNullOrWhiteSpace(id) && id != "0";

		/// <summary>
		/// Numeric form of the id. Every vendor supported so far hands out numeric ids, so tooling that has to keep
		/// working with numbers (legacy configs, file names) can go through here instead of parsing by hand.
		/// </summary>
		public bool TryGetNumericId(out ulong numericId)
		{
			return ulong.TryParse(id, out numericId);
		}

		public bool Equals(ModItemKey other)
		{
			return string.Equals(vendor, other.vendor, StringComparison.OrdinalIgnoreCase) &&
			       string.Equals(id, other.id, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is ModItemKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			var vendorHash = vendor == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(vendor);
			var idHash = id == null ? 0 : id.GetHashCode();
			return unchecked((vendorHash * 397) ^ idHash);
		}

		public override string ToString()
		{
			return IsValid ? $"{vendor}:{id}" : string.Empty;
		}

		public static bool operator ==(ModItemKey left, ModItemKey right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ModItemKey left, ModItemKey right)
		{
			return !left.Equals(right);
		}
	}
}
