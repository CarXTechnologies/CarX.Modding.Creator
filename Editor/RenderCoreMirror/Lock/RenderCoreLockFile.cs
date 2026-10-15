namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Входной файл снимка: роль и путь от корня пакета. Пути — только справка (Info), файлы ищутся по имени.</summary>
	public sealed class RenderCoreLockFile
	{
		public const string EnumRole = "enum";
		public const string SettingsRole = "settings";
		public const string SetupRole = "setup";
		public const string ShaderRole = "shader";
		public const string DefinitionsRole = "definitions";
		public const string FunctionsRole = "functions";
		public const string PackageRole = "package";

		/// <summary>Префикс роли файла Definitions/*: "defs:BaseMaterialDefs".</summary>
		public const string DefsRolePrefix = "defs:";

		/// <summary>Префикс роли файла Functions/*: "func:BaseMaterialFunc".</summary>
		public const string FuncRolePrefix = "func:";

		public readonly string role;
		public readonly string path;

		public RenderCoreLockFile(string role, string path)
		{
			this.role = role;
			this.path = path;
		}
	}
}
