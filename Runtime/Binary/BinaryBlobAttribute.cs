using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Base64 string field for JSON; the binary document stores the raw bytes of <see cref="bytesField"/> instead.</summary>
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class BinaryBlobAttribute : Attribute
	{
		public readonly string bytesField;

		public BinaryBlobAttribute(string bytesField)
		{
			this.bytesField = bytesField;
		}
	}
}
