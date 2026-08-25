using System;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor.Publishing
{
	/// <summary>
	/// Drives a vendor sign in through editor modal dialogs.
	/// </summary>
	/// <remarks>
	/// A vendor SDK may resume the flow on a worker thread - mod.io hands the security code request back from
	/// wherever its http completion landed - and every editor UI call has to happen on the main thread. So each
	/// prompt is marshalled through <c>EditorApplication.delayCall</c> rather than being invoked inline.
	/// </remarks>
	public sealed class EditorModAuthPrompt : IModAuthPrompt
	{
		private readonly string m_vendorId;
		private readonly string m_vendorDisplayName;

		public EditorModAuthPrompt(string vendorId, string vendorDisplayName)
		{
			m_vendorId = vendorId;
			m_vendorDisplayName = vendorDisplayName;
		}

		public async Task<string> RequestEmailAsync(CancellationToken cancellationToken)
		{
			// Pre-filled with the last address that got this far, so a retry after an expired code is one keypress.
			var email = await OnMainThread(() => ModAuthPromptWindow.PromptForText(
				$"Sign in to {m_vendorDisplayName}",
				"Email",
				$"Enter the email address of your {m_vendorDisplayName} account. " +
				"A one time code will be sent to it.",
				status: null,
				initialValue: ModAuthPreferences.GetLastEmail(m_vendorId)), cancellationToken);

			ModAuthPreferences.SetLastEmail(m_vendorId, email);
			return email;
		}

		public Task<string> RequestSecurityCodeAsync(CancellationToken cancellationToken)
		{
			return OnMainThread(() => ModAuthPromptWindow.PromptForText(
				$"Sign in to {m_vendorDisplayName}",
				"Code",
				"Enter the one time code from the email.",
				status: "The code expires after a few minutes. Cancel and start over if it stops being accepted."),
				cancellationToken);
		}

		public async Task<bool> RequestTermsAcceptanceAsync(string termsText, CancellationToken cancellationToken)
		{
			var text = string.IsNullOrWhiteSpace(termsText)
				? $"Accept the {m_vendorDisplayName} terms of use to continue."
				: termsText;

			// Consent survives across sign ins; only a change to the text itself brings the wall back.
			if (ModAuthPreferences.HasAcceptedTerms(m_vendorId, text))
			{
				return true;
			}

			var accepted = await OnMainThread(() => ModAuthPromptWindow.PromptForTerms(
				$"{m_vendorDisplayName} terms of use", text), cancellationToken);

			if (accepted)
			{
				ModAuthPreferences.SetAcceptedTerms(m_vendorId, text);
			}

			return accepted;
		}

		public void ReportStatus(string message)
		{
			if (!string.IsNullOrWhiteSpace(message))
			{
				Debug.Log($"[{m_vendorDisplayName}] {message}");
			}
		}

		private static Task<T> OnMainThread<T>(Func<T> action, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<T>(cancellationToken);
			}

			var completion = new TaskCompletionSource<T>();

			EditorApplication.delayCall += () =>
			{
				if (cancellationToken.IsCancellationRequested)
				{
					completion.TrySetCanceled(cancellationToken);
					return;
				}

				try
				{
					completion.TrySetResult(action());
				}
				catch (Exception exception)
				{
					completion.TrySetException(exception);
				}
			};

			return completion.Task;
		}
	}
}
