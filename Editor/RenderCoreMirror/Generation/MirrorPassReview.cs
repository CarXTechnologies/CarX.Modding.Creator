namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Проверенная человеком конфигурация прохода выреза альфа-теста: что DepthOnly/ShadowCaster компилируют для набора keyword'ов
	/// материала (обычно общий ALPHATEST): Defs, MATERIALFEATURE_*, потоки, библиотеки, активные строки Func, программа прохода.
	/// Генератор сверяет reviewedHash с RenderCoreLockPassConfig.ReviewHash (RCM045).
	/// </summary>
	public sealed class MirrorPassReview
	{
		public readonly string pass;

		/// <summary>keyword'ы материала через '+' (как RenderCoreLockPassConfig.keywords) или "_".</summary>
		public readonly string keywords;

		public readonly string reviewedHash;

		public MirrorPassReview(string pass, string keywords, string reviewedHash)
		{
			this.pass = pass;
			this.keywords = keywords;
			this.reviewedHash = reviewedHash;
		}
	}
}
