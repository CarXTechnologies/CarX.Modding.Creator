using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class EditorCollectionProvider : ProviderCollection
	{
		private static readonly IModFileProvider DefaultFileProvider = new DefaultFileProvider(Path.Combine(Application.dataPath, "Mods"));

		public EditorCollectionProvider(bool binary = false) : base(ModdingVersion.GetDefaultFullVersionFormat())
        {
            if (binary)
            {
                var entries = providers[0].providers;
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].type == typeof(UnityPrefabInstance))
                        entries[i] = new ModProvider(typeof(UnityPrefabInstance), new ObjMtlExporterProvider(DefaultFileProvider, true));
            }
        }

		protected override VersionProvider[] providers { get; set; } =
		{
			new(ModdingVersion.GetFullVersionFormat(),
				new ModProvider(typeof(ModMeta), new MetaProvider<ModMeta>(DefaultFileProvider, string.Empty)),
				new ModProvider(typeof(StaticHierarchyMeta), new HierarchiesMetaProvider<StaticHierarchyMeta>(DefaultFileProvider)),
				new ModProvider(typeof(PrefabHierarchyMeta), new PrefabsMetaProvider<PrefabHierarchyMeta>(DefaultFileProvider)),
				new ModProvider(typeof(UnityPrefabInstance), new ObjMtlExporterProvider(DefaultFileProvider)),
				new ModProvider(typeof(Texture2D), new TexturePngProvider(DefaultFileProvider)),
				new ModProvider(typeof(LodHierarchyMeta), new LodInstanceProvider<LodHierarchyMeta>(DefaultFileProvider)),
				new ModProvider(typeof(AnimationMeta), new AnimationMetaProvider(DefaultFileProvider)),
				new ModProvider(typeof(GameMarkerMeta), new GameMarkerInstanceProvider<GameMarkerMeta>(DefaultFileProvider)),
				new ModProvider(typeof(LightHierarchyMeta), new LightsMetaProvider<LightHierarchyMeta>(DefaultFileProvider))
			),
		};
	}
}