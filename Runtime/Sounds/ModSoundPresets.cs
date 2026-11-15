using System.Collections.Generic;

namespace CarX.Modding.Creator.Runtime
{
	// Game sounds a map can use by id. The game maps each id to its own sound event; an id it does not know is not played
	public static class ModSoundPresets
	{
		public readonly struct Preset
		{
			public readonly string id;
			public readonly string category;

			public Preset(string id, string category)
			{
				this.id = id;
				this.category = category;
			}
		}

		// Sounds of a place, played at the marker position
		public static readonly IReadOnlyList<Preset> Point = new[]
		{
			new Preset("airport", "Area"),
			new Preset("container_zone", "Area"),
			new Preset("raceway", "Area"),
			new Preset("wind_mountain", "Area"),
			new Preset("boat_wood", "Boats"),
			new Preset("boat_wood_big", "Boats"),
			new Preset("castle", "Buildings"),
			new Preset("house_jp", "Buildings"),
			new Preset("office", "Buildings"),
			new Preset("radio_station", "Buildings"),
			new Preset("service", "Buildings"),
			new Preset("shop", "Buildings"),
			new Preset("temple", "Buildings"),
			new Preset("birds", "Fauna"),
			new Preset("cicadas", "Fauna"),
			new Preset("zoo", "Fauna"),
			new Preset("forest", "Forest"),
			new Preset("dock", "Garage"),
			new Preset("tunnel", "Garage"),
			new Preset("electric_booth", "Objects"),
			new Preset("hvac", "Objects"),
			new Preset("vending_machine", "Objects"),
			new Preset("camper_van", "Objects"),
			new Preset("tent", "Objects"),
			new Preset("people_couple", "People"),
			new Preset("crowd_small", "People"),
			new Preset("crowd_medium", "People"),
			new Preset("crowd_stadium", "People"),
			new Preset("person", "People"),
			new Preset("ambulance_idle", "Vehicles"),
			new Preset("car_idle", "Vehicles"),
			new Preset("fire_truck_idle", "Vehicles"),
			new Preset("helicopter", "Vehicles"),
			new Preset("truck_idle", "Vehicles"),
			new Preset("pool", "Water"),
			new Preset("pool_big", "Water"),
			new Preset("river", "Water"),
			new Preset("sea", "Water"),
			new Preset("sea_close", "Water"),
		};

		// Background sound of a whole map, as on the game tracks
		public static readonly IReadOnlyList<Preset> Ambience = new[]
		{
			new Preset("adm", "Tracks"),
			new Preset("dominion", "Tracks"),
			new Preset("ebisu", "Tracks"),
			new Preset("fuji", "Tracks"),
			new Preset("gunsai", "Tracks"),
			new Preset("kami", "Tracks"),
			new Preset("nurburgring", "Tracks"),
			new Preset("polygon", "Tracks"),
			new Preset("south_port", "Tracks"),
			new Preset("springstone", "Tracks"),
			new Preset("utah", "Tracks"),
			new Preset("wind", "Weather"),
		};
	}
}
