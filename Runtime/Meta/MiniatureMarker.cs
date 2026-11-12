namespace CarX.Modding.Creator.Runtime
{
	// The Miniature game marker: what an object and everything under it is in the world map miniature,
	// instead of the automatic guess. Not exported to the game
	public static class MiniatureMarker
	{
		public const string Head = "Miniature";
		public const string Road = Head + "/Road";
		public const string Terrain = Head + "/Terrain";
		public const string Object = Head + "/Object";
		public const string Hidden = Head + "/Hidden";
	}
}
