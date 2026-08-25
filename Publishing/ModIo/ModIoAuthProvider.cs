using System;
using System.Threading;
using System.Threading.Tasks;
using Modio;
using Modio.Authentication;
using Modio.Users;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;

namespace Plugins.CarX.Modding.Creator.Publishing.ModIo
{
	/// <summary>
	/// mod.io sign in over the email one time code flow.
	/// </summary>
	/// <remarks>
	/// Email was picked over the Steam ticket flow because this runs in the editor: a Steam ticket needs the game to
	/// be launched through a running Steam client, which is exactly what an editor tool is not. The token the SDK
	/// receives is cached by the plugin on disk, so the code only has to be entered again once it expires.
	/// </remarks>
	public sealed class ModIoAuthProvider : IModAuthProvider, IEmailCodePrompter
	{
		private readonly ModIoConfig m_config;

		private ModAuthState m_state = ModAuthState.Unavailable("mod.io is not initialized yet.");

		/// <summary>
		/// The prompt driving the sign in currently in flight. The mod.io auth service asks for the security code
		/// through <see cref="ShowCodePrompt"/> at a point where the caller is no longer on the stack, so the prompt
		/// has to be parked here for the duration of the flow.
		/// </summary>
		private IModAuthPrompt m_activePrompt;

		private CancellationToken m_activeCancellation;

		public ModIoAuthProvider(ModIoConfig config)
		{
			m_config = config;
		}

		public ModAuthState State => m_state;

		public bool RequiresInteractiveLogin => true;

		public event Action<ModAuthState> StateChanged;

		public Task<ModAuthState> RefreshAsync(CancellationToken cancellationToken)
		{
			SetState(ReadClientState());
			return Task.FromResult(m_state);
		}

		public async Task<ModOperationResult> LoginAsync(IModAuthPrompt prompt, CancellationToken cancellationToken)
		{
			if (prompt == null)
			{
				return ModOperationResult.Fail("mod.io sign in needs an interactive prompt.");
			}

			if (!ModioClient.IsInitialized)
			{
				return ModOperationResult.Fail("mod.io is not initialized.");
			}

			var authService = ModIoEditorBootstrap.AuthService;
			if (authService == null)
			{
				return ModOperationResult.Fail("mod.io auth service was not bound.");
			}

			// Terms come before anything is asked of the user: mod.io only accepts an authentication call that
			// confirms they were displayed, so refusing them makes the email pointless. The prompt remembers a
			// previous acceptance, so in practice this wall only appears once.
			var (termsError, terms) = await TermsOfUse.Get();
			if (termsError)
			{
				return ModOperationResult.Fail($"Could not fetch the mod.io terms of use: {termsError.GetMessage()}");
			}

			var accepted = await prompt.RequestTermsAcceptanceAsync(terms.TermsText, cancellationToken);
			if (!accepted)
			{
				return ModOperationResult.Fail("The mod.io terms of use were not accepted.");
			}

			var email = await prompt.RequestEmailAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(email))
			{
				return ModOperationResult.Fail("Sign in cancelled.");
			}

			SetState(new ModAuthState(ModAuthStatus.Authenticating, null, $"Sending a code to {email}..."));

			m_activePrompt = prompt;
			m_activeCancellation = cancellationToken;
			authService.SetCodePrompter(this);

			try
			{
				var error = await authService.Authenticate(true, email);

				if (error)
				{
					SetState(ModAuthState.NotAuthenticated(error.GetMessage()));
					return ModOperationResult.Fail($"mod.io sign in failed: {error.GetMessage()}");
				}
			}
			finally
			{
				m_activePrompt = null;
				m_activeCancellation = default;
			}

			SetState(ReadClientState());

			return m_state.IsAuthenticated
				? ModOperationResult.Ok($"Signed in as {m_state.UserName}.")
				: ModOperationResult.Fail("mod.io reported success but no user session is active.");
		}

		public async Task<ModOperationResult> LogoutAsync()
		{
			if (!ModioClient.IsInitialized)
			{
				return ModOperationResult.Fail("mod.io is not initialized.");
			}

			await User.LogOut();
			SetState(ReadClientState());
			return ModOperationResult.Ok("Signed out of mod.io.");
		}

		/// <summary>Called by the mod.io auth service once the one time code has been mailed out.</summary>
		Task<string> IEmailCodePrompter.ShowCodePrompt()
		{
			if (m_activePrompt == null)
			{
				// Nothing is driving a sign in, so there is nobody to ask; returning null aborts the flow cleanly.
				return Task.FromResult<string>(null);
			}

			m_activePrompt.ReportStatus("A five character code was emailed to you. Enter it to finish signing in.");
			return m_activePrompt.RequestSecurityCodeAsync(m_activeCancellation);
		}

		private ModAuthState ReadClientState()
		{
			if (!ModioClient.IsInitialized)
			{
				return ModAuthState.Unavailable("mod.io is not initialized yet.");
			}

			var user = User.Current;
			if (user == null || !user.IsAuthenticated)
			{
				return ModAuthState.NotAuthenticated($"Not signed in to {m_config.ProfileUrl}.");
			}

			// User.UserId reads straight through Profile, so it is only safe once a profile has been synced.
			var profile = user.Profile;
			if (profile == null)
			{
				return ModAuthState.Authenticated("mod.io user");
			}

			return ModAuthState.Authenticated(
				string.IsNullOrWhiteSpace(profile.Username) ? profile.UserId.ToString() : profile.Username);
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
