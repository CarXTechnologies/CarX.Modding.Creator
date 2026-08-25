namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	public enum ModAuthStatus
	{
		/// <summary>The vendor backend cannot be reached at all - SDK missing, client not running, config empty.</summary>
		Unavailable = 0,

		/// <summary>The backend is reachable but nobody is signed in.</summary>
		NotAuthenticated = 1,

		/// <summary>A sign in flow is in flight.</summary>
		Authenticating = 2,

		Authenticated = 3,
	}

	/// <summary>Snapshot of the sign in state of a vendor, shaped for direct display in the editor UI.</summary>
	public readonly struct ModAuthState
	{
		public ModAuthStatus Status { get; }

		/// <summary>Display name of the signed in user, empty unless <see cref="Status"/> is Authenticated.</summary>
		public string UserName { get; }

		/// <summary>Human readable explanation of the current state - why it is unavailable, why a login failed.</summary>
		public string Message { get; }

		public ModAuthState(ModAuthStatus status, string userName = null, string message = null)
		{
			Status = status;
			UserName = userName ?? string.Empty;
			Message = message ?? string.Empty;
		}

		public bool IsAuthenticated => Status == ModAuthStatus.Authenticated;

		public static ModAuthState Unavailable(string message)
		{
			return new ModAuthState(ModAuthStatus.Unavailable, null, message);
		}

		public static ModAuthState NotAuthenticated(string message = null)
		{
			return new ModAuthState(ModAuthStatus.NotAuthenticated, null, message);
		}

		public static ModAuthState Authenticated(string userName)
		{
			return new ModAuthState(ModAuthStatus.Authenticated, userName);
		}
	}
}
