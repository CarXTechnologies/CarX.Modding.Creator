namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Visibility of a mod entry on the vendor side.
	/// Steam additionally knows <c>FriendsOnly</c> and <c>Unlisted</c>, mod.io only distinguishes hidden from public,
	/// so the common contract carries the intersection that every vendor can honour.
	/// </summary>
	public enum ModVisibility
	{
		/// <summary>
		/// No visibility. When listing entries it means the vendor did not report one; on
		/// <see cref="ModUploadRequest"/> it means "leave the current setting alone", which is what an ordinary
		/// content update wants. Not valid when creating an entry - a vendor has to be told something there.
		/// </summary>
		Unknown = -1,

		Private = 0,
		Public = 1,
	}
}
