using System;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;
using Steamworks;

namespace Plugins.CarX.Modding.Creator.Publishing.Steam
{
	/// <summary>
	/// Reports the Steam session the running Steam client already owns.
	/// There is no sign in flow to drive here: either the client is running and signed in before the editor starts,
	/// or the vendor is simply unavailable. <see cref="LoginAsync"/> therefore only re-checks that session.
	/// </summary>
	public sealed class SteamWorkshopAuthProvider : IModAuthProvider
	{
		private const string NoClientMessage =
			"Steam is not running or the Steam client is not signed in. Start Steam, sign in, then reopen this window.";

		private ModAuthState m_state = ModAuthState.Unavailable(NoClientMessage);

		public ModAuthState State => m_state;

		public bool RequiresInteractiveLogin => false;

		public event Action<ModAuthState> StateChanged;

		public Task<ModAuthState> RefreshAsync(CancellationToken cancellationToken)
		{
			SetState(ReadClientState());
			return Task.FromResult(m_state);
		}

		public Task<ModOperationResult> LoginAsync(IModAuthPrompt prompt, CancellationToken cancellationToken)
		{
			SetState(ReadClientState());

			return Task.FromResult(m_state.IsAuthenticated
				? ModOperationResult.Ok($"Signed in as {m_state.UserName}.")
				: ModOperationResult.Fail(NoClientMessage));
		}

		public Task<ModOperationResult> LogoutAsync()
		{
			// The session belongs to the Steam client; signing out has to happen there.
			return Task.FromResult(ModOperationResult.Fail("Sign out of Steam from the Steam client itself."));
		}

		private static ModAuthState ReadClientState()
		{
			if (!SteamClient.IsValid)
			{
				return ModAuthState.Unavailable(NoClientMessage);
			}

			var name = SteamClient.Name;
			return string.IsNullOrWhiteSpace(name)
				? ModAuthState.Authenticated(SteamClient.SteamId.ToString())
				: ModAuthState.Authenticated(name);
		}

		private void SetState(ModAuthState state)
		{
			if (state.Status == m_state.Status && state.UserName == m_state.UserName)
			{
				return;
			}

			m_state = state;
			StateChanged?.Invoke(m_state);
		}
	}
}
