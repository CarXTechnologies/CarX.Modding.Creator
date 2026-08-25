using System.Text;
using UnityEditor;

namespace Plugins.CarX.Modding.Creator.Editor.Publishing
{
	/// <summary>
	/// Remembers the parts of a sign in flow that a person should not have to repeat.
	/// </summary>
	/// <remarks>
	/// Scoped per vendor rather than per project: someone signs in with the same mod.io account whichever project
	/// they have open. Only the email address and the fact that the terms were accepted are kept - never the one time
	/// code, and never a token, which the vendor SDK caches itself.
	/// </remarks>
	internal static class ModAuthPreferences
	{
		private const string EmailKey = "CarX.Modding.Publisher.Email";
		private const string TermsKey = "CarX.Modding.Publisher.Terms";

		internal static string GetLastEmail(string vendorId)
		{
			return EditorPrefs.GetString($"{EmailKey}.{vendorId}", string.Empty);
		}

		internal static void SetLastEmail(string vendorId, string email)
		{
			if (!string.IsNullOrWhiteSpace(email))
			{
				EditorPrefs.SetString($"{EmailKey}.{vendorId}", email.Trim());
			}
		}

		/// <summary>
		/// Whether this exact terms text was already accepted. Keyed by the text itself, so a vendor publishing new
		/// terms asks again instead of silently riding on the old consent.
		/// </summary>
		internal static bool HasAcceptedTerms(string vendorId, string termsText)
		{
			var stored = EditorPrefs.GetString($"{TermsKey}.{vendorId}", string.Empty);
			return stored.Length > 0 && stored == Fingerprint(termsText);
		}

		internal static void SetAcceptedTerms(string vendorId, string termsText)
		{
			EditorPrefs.SetString($"{TermsKey}.{vendorId}", Fingerprint(termsText));
		}

		internal static void ForgetTerms(string vendorId)
		{
			EditorPrefs.DeleteKey($"{TermsKey}.{vendorId}");
		}

		/// <summary>
		/// FNV-1a over the terms text. Written out by hand rather than using <c>string.GetHashCode</c>, which is not
		/// guaranteed to be stable between runtimes - and this value has to survive in EditorPrefs.
		/// </summary>
		private static string Fingerprint(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			const ulong offsetBasis = 14695981039346656037;
			const ulong prime = 1099511628211;

			var hash = offsetBasis;

			foreach (var b in Encoding.UTF8.GetBytes(value))
			{
				hash ^= b;
				hash *= prime;
			}

			return hash.ToString("X16");
		}
	}
}
