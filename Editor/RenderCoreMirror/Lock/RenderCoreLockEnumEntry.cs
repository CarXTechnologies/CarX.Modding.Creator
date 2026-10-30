namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Член enum RenderCore.Editor.MaterialVariant: имя — keyword шейдера, id — значение свойства materialVariant.</summary>
	public sealed class RenderCoreLockEnumEntry
	{
		public readonly string name;
		public readonly int id;

		public RenderCoreLockEnumEntry(string name, int id)
		{
			this.name = name;
			this.id = id;
		}
	}
}
