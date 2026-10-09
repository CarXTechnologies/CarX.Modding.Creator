using System.Threading;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Everything an interactive sign in flow may need to ask the user for.
	/// The publisher owns the protocol, the host owns the UI - a console tool can implement this against stdin just
	/// as well as the editor window does against UIElements.
	/// </summary>
	public interface IModAuthPrompt
	{
		/// <summary>Ask for the email address to send a one time code to. Return null to abort the sign in.</summary>
		public Task<string> RequestEmailAsync(CancellationToken cancellationToken);

		/// <summary>Ask for the one time code that was mailed out. Return null to abort the sign in.</summary>
		public Task<string> RequestSecurityCodeAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Show the vendor terms of use and ask whether the user accepts them.
		/// mod.io refuses to authenticate unless the caller confirms the terms were displayed.
		/// </summary>
		public Task<bool> RequestTermsAcceptanceAsync(string termsText, CancellationToken cancellationToken);

		/// <summary>Report progress or a recoverable problem while the flow is running.</summary>
		public void ReportStatus(string message);
	}
}
