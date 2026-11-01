namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Copy of Runtime ModRenderCoreSource for the core without UnityEngine: names and values match (checked by the table test).</summary>
	public enum MirrorSource
	{
		None = 0,
		Diffuse = 1,
		Emission = 2,
		Normal = 3,
		Surface = 4,

		// 5 was AlphaCutoff; the number is not reused.
		EmissionParameters = 6
	}
}
