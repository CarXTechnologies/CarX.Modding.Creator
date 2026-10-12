namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Конфигурация прохода выреза (DepthOnly/ShadowCaster) для набора keyword'ов материала: вычисленные Definitions и Func так, как их
	/// компилирует этот проход, и отпечаток активных строк программы прохода (без #pragma) по всем конфигурациям его keyword'ов.
	/// </summary>
	public sealed class RenderCorePassDefinitions
	{
		public readonly string pass;

		/// <summary>Ключ: keyword'ы материала, которые проход объявляет, через '+' (ordinal), или "_".</summary>
		public readonly string keywords;

		public readonly RenderCoreVariantDefinitions definitions;

		public readonly string programHash;

		public RenderCorePassDefinitions(string pass, string keywords, RenderCoreVariantDefinitions definitions, string programHash)
		{
			this.pass = pass;
			this.keywords = keywords;
			this.definitions = definitions;
			this.programHash = programHash;
		}
	}
}
