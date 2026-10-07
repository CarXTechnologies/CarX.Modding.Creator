using System;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Marks an <see cref="IModPublisher"/> implementation so that <see cref="ModPublisherRegistry"/> can find it.
	/// Discovery goes through reflection on purpose: vendor implementations live in assemblies that are compiled out
	/// when their SDK is absent, and nothing may hold a hard reference to an assembly that might not exist.
	/// The annotated type has to expose a public constructor taking a single <see cref="ModPublisherContext"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class ModPublisherAttribute : Attribute
	{
		public string VendorId { get; }

		public string DisplayName { get; }

		/// <summary>Sort order in the vendor picker; lower comes first.</summary>
		public int Order { get; }

		public ModPublisherAttribute(string vendorId, string displayName, int order = 0)
		{
			VendorId = vendorId;
			DisplayName = displayName;
			Order = order;
		}
	}
}
