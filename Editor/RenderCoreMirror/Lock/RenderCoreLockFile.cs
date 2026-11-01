namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Snapshot input file: role and path from the package root. Paths are reference only (Info); files are found by name.</summary>
	public sealed class RenderCoreLockFile
	{
		public const string EnumRole = "enum";
		public const string SettingsRole = "settings";
		public const string SetupRole = "setup";
		public const string ShaderRole = "shader";
		public const string DefinitionsRole = "definitions";
		public const string FunctionsRole = "functions";
		public const string PackageRole = "package";

		/// <summary>Role prefix of a Definitions/* file: "defs:BaseMaterialDefs".</summary>
		public const string DefsRolePrefix = "defs:";

		/// <summary>Role prefix of a Functions/* file: "func:BaseMaterialFunc".</summary>
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
