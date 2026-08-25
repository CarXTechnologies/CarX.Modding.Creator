using System;
using System.Threading;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Sign in half of a vendor integration, split out from <see cref="IModPublisher"/> because the two have very
	/// different lifetimes: Steam is signed in by the running client before the tool even starts, while mod.io needs
	/// an interactive flow and can be signed out again without tearing the publisher down.
	/// </summary>
	public interface IModAuthProvider
	{
		public ModAuthState State { get; }

		/// <summary>
		/// True when <see cref="LoginAsync"/> has to be driven by a real user through <see cref="IModAuthPrompt"/>.
		/// False for vendors that inherit an ambient session (Steam), where the UI should not offer a login button.
		/// </summary>
		public bool RequiresInteractiveLogin { get; }

		public event Action<ModAuthState> StateChanged;

		/// <summary>Re-read the sign in state from the vendor without starting a new flow.</summary>
		public Task<ModAuthState> RefreshAsync(CancellationToken cancellationToken);

		public Task<ModOperationResult> LoginAsync(IModAuthPrompt prompt, CancellationToken cancellationToken);

		public Task<ModOperationResult> LogoutAsync();
	}
}
