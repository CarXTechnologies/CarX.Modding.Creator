using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor.Publishing
{
	/// <summary>
	/// Modal prompts for the interactive parts of a vendor sign in: a single line of text, or a terms of use wall.
	/// </summary>
	/// <remarks>
	/// <c>ShowModalUtility</c> blocks until the window closes, which is what makes these usable as plain synchronous
	/// calls from inside an async sign in flow. They must therefore only ever be called on the main thread - see
	/// <see cref="EditorModAuthPrompt"/>, which is responsible for getting there.
	/// </remarks>
	internal sealed class ModAuthPromptWindow : EditorWindow
	{
		private const float Width = 420f;

		private string m_description;
		private string m_label;
		private string m_value = string.Empty;
		private string m_status = string.Empty;
		private bool m_termsMode;
		private Vector2 m_scroll;
		private bool m_confirmed;
		private bool m_focusSet;

		/// <summary>Shows a one line input. Returns null when the user cancels or closes the window.</summary>
		internal static string PromptForText(
			string title,
			string label,
			string description,
			string status,
			string initialValue = null)
		{
			var window = CreateInstance<ModAuthPromptWindow>();
			window.titleContent = new GUIContent(title);
			window.m_label = label;
			window.m_description = description;
			window.m_status = status;
			window.m_value = initialValue ?? string.Empty;
			window.m_termsMode = false;
			window.minSize = new Vector2(Width, 150f);
			window.ShowModalUtility();

			return window.m_confirmed && !string.IsNullOrWhiteSpace(window.m_value) ? window.m_value.Trim() : null;
		}

		/// <summary>Shows the vendor terms and returns whether the user accepted them.</summary>
		internal static bool PromptForTerms(string title, string terms)
		{
			var window = CreateInstance<ModAuthPromptWindow>();
			window.titleContent = new GUIContent(title);
			window.m_description = terms;
			window.m_termsMode = true;
			window.minSize = new Vector2(Width, 360f);
			window.ShowModalUtility();

			return window.m_confirmed;
		}

		private void OnGUI()
		{
			if (m_termsMode)
			{
				DrawTerms();
				return;
			}

			DrawTextInput();
		}

		private void DrawTerms()
		{
			EditorGUILayout.LabelField("Terms of use", EditorStyles.boldLabel);

			m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
			EditorGUILayout.LabelField(m_description, EditorStyles.wordWrappedLabel);
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Decline"))
				{
					Close();
				}

				if (GUILayout.Button("Accept"))
				{
					m_confirmed = true;
					Close();
				}
			}
		}

		private void DrawTextInput()
		{
			if (!string.IsNullOrWhiteSpace(m_description))
			{
				EditorGUILayout.LabelField(m_description, EditorStyles.wordWrappedLabel);
				EditorGUILayout.Space();
			}

			GUI.SetNextControlName("mod-auth-input");
			m_value = EditorGUILayout.TextField(m_label, m_value);

			if (!m_focusSet)
			{
				// The window has to be drawn once before focus can be moved into the field.
				EditorGUI.FocusTextInControl("mod-auth-input");
				m_focusSet = true;
			}

			if (!string.IsNullOrWhiteSpace(m_status))
			{
				EditorGUILayout.HelpBox(m_status, MessageType.Info);
			}

			// Enter submits, Escape cancels - typing a code and reaching for the mouse is needless friction.
			var submittedWithKeyboard = Event.current.type == EventType.KeyDown &&
			                            Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter;

			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
			{
				Close();
				return;
			}

			GUILayout.FlexibleSpace();

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Cancel"))
				{
					Close();
					return;
				}

				using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(m_value)))
				{
					if (GUILayout.Button("Continue") || (submittedWithKeyboard && !string.IsNullOrWhiteSpace(m_value)))
					{
						m_confirmed = true;
						Close();
					}
				}
			}
		}
	}
}
