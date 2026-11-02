namespace CarX.Modding.Creator.Runtime
{
	/// <summary>Portable mesh data: Unity ECS layouts and Shader.PropertyToID values are not stored.</summary>
	public sealed class BinaryModModel
	{
		public string name;
		public BinaryModMesh[] meshes;
	}
}
