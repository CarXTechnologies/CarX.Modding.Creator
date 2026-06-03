using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class EditorCollectionProvider : ProviderCollection
	{
		private static readonly IModFileProvider DefaultFileProvider = new DefaultFileProvider(Path.Combine(Application.dataPath, "Mods"));

		public EditorCollectionProvider() : base(Version.GetDefaultFullVersionFormat())
		{

		}

		protected override VersionProvider[] providers { get; set; } =
		{
			new(Version.GetFullVersionFormat(),
				new ModProvider(typeof(ModMeta), new MetaProvider<ModMeta>(DefaultFileProvider, string.Empty)),
				new ModProvider(typeof(StaticHierarchyMeta), new HierarchiesMetaProvider<StaticHierarchyMeta>(DefaultFileProvider)),
				new ModProvider(typeof(PrefabHierarchyMeta), new PrefabsMetaProvider<PrefabHierarchyMeta>(DefaultFileProvider)),
				new ModProvider(typeof(UnityPrefabInstance), new ObjMtlExporterProvider(DefaultFileProvider)),
				new ModProvider(typeof(Texture2D), new TexturePngProvider(DefaultFileProvider))
			),
		};
	}
}