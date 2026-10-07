using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Строковое поле с base64-данными для JSON; в бинарном документе вместо него пишутся сырые байты поля <see cref="bytesField"/>.
	/// </summary>
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
