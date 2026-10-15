namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Переносимые данные меша: раскладки Unity ECS и значения Shader.PropertyToID не сохраняются.</summary>
	public sealed class BinaryModModel
	{
		public string name;
		public BinaryModMesh[] meshes;
	}
}
