namespace CarX.Modding.Creator.Publishing
{
	/// <summary>Mod upload stage, see <see cref="ModUploadProgress"/>.</summary>
	/// <remarks>
	/// Stages go strictly in order. A platform that cannot tell a stage apart skips it: mod.io without CARX PATCH 2 in the
	/// plugin (CARX_MODDING_MODIO_UPLOAD_PROGRESS not defined) goes from preparation straight to uploading and reports neither
	/// packing nor bytes. Steam reports packing as a single value, without progress inside it.
	/// </remarks>
	public enum ModUploadStage
	{
		/// <summary>Request validation, reading the item from the platform and sending metadata before the content.</summary>
		Preparing,

		/// <summary>Packing the build folder into an archive before sending.</summary>
		Packing,

		/// <summary>Sending the content to the platform.</summary>
		Uploading,

		/// <summary>Content sent; the platform accepts it and commits the changes.</summary>
		Processing,
	}
}
