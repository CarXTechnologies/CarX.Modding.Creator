namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModResultCollector
	{
		public ModResults CollectModResults(IModCollectionProvider collectionProvider, string version);
	}
}