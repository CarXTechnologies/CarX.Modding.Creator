using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class EditorCollectionProvider : ProviderCollection
	{
		private static readonly IModFileProvider s_defaultFileProvider = new DefaultFileProvider(Path.Combine(Application.dataPath, "Mods"));

		public EditorCollectionProvider(bool binary = false) : base(ModdingVersion.GetDefaultFullVersionFormat())
		{
			if (!binary)
			{
				return;
			}

			ModProvider[] entries = providers[0].providers;

			for (int i = 0; i < entries.Length; i++)
			{
				if (entries[i].type == typeof(UnityPrefabInstance))
				{
					entries[i] = new ModProvider(typeof(UnityPrefabInstance), new ObjMtlExporterProvider(s_defaultFileProvider, binary: true));
				}
			}
		}

		protected override VersionProvider[] providers { get; set; } =
		{
			new(ModdingVersion.GetFullVersionFormat(),
				new ModProvider(typeof(ModMeta), new MetaProvider<ModMeta>(s_defaultFileProvider, string.Empty)),
				new ModProvider(typeof(StaticHierarchyMeta), new HierarchiesMetaProvider<StaticHierarchyMeta>(s_defaultFileProvider)),
				new ModProvider(typeof(PrefabHierarchyMeta), new PrefabsMetaProvider<PrefabHierarchyMeta>(s_defaultFileProvider)),
				new ModProvider(typeof(UnityPrefabInstance), new ObjMtlExporterProvider(s_defaultFileProvider)),
				new ModProvider(typeof(Texture2D), new TexturePngProvider(s_defaultFileProvider)),
				new ModProvider(typeof(LodHierarchyMeta), new LodInstanceProvider<LodHierarchyMeta>(s_defaultFileProvider)),
				new ModProvider(typeof(AnimationMeta), new AnimationMetaProvider(s_defaultFileProvider)),
				new ModProvider(typeof(GameMarkerMeta), new GameMarkerInstanceProvider<GameMarkerMeta>(s_defaultFileProvider)),
				new ModProvider(typeof(LightHierarchyMeta), new LightsMetaProvider<LightHierarchyMeta>(s_defaultFileProvider))
			),
		};
	}
}