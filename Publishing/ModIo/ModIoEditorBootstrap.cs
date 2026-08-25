using System.Threading.Tasks;
using Modio;
using Modio.API.Interfaces;
using Modio.Authentication;
using Modio.Unity;
using Modio.Users;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;

namespace Plugins.CarX.Modding.Creator.Publishing.ModIo
{
	/// <summary>
	/// Stands the mod.io SDK up inside the editor.
	/// </summary>
	/// <remarks>
	/// The plugin wires its own services from <c>ModioUnity.OnAfterAssembliesLoaded</c>, which carries
	/// <c>[RuntimeInitializeOnLoadMethod]</c> and therefore only ever runs when entering play mode. An editor tool
	/// that never enters play mode has to do that wiring itself, which is what this type is for. Only the services
	/// the upload flow actually touches are bound - the root path provider is already handled by the plugin's own
	/// <c>ModioEditorRootPathBinder</c>, which does run at editor load.
	/// </remarks>
	internal static class ModIoEditorBootstrap
	{
		private static bool m_servicesBound;
		private static ModioEmailAuthService m_authService;

		/// <summary>
		/// Credentials the running client was actually initialized with. The api base url is baked in at init, so
		/// editing the config asset afterwards has no effect until the client is brought back up - which used to
		/// mean the fix "did not work" until the next domain reload.
		/// </summary>
		private static string m_activeFingerprint;

		/// <summary>Endpoint the cached session belongs to, so a move to another one can drop it.</summary>
		private static string m_activeServerUrl;

		/// <summary>The initialization currently running, shared by every caller that arrives while it is in flight.</summary>
		private static Task<ModOperationResult> m_initialization;

		/// <summary>The email auth service the SDK was told to use. Null until <see cref="Bind"/> has run.</summary>
		internal static ModioEmailAuthService AuthService => m_authService;

		/// <summary>
		/// Binds the SDK services and returns the settings the client should be initialized with.
		/// </summary>
		/// <remarks>
		/// Only ever called immediately before an <see cref="ModioClient.Init"/>. Binding <c>ModioSettings</c> while
		/// the client is already up makes the SDK log "you have changed the ModioSettings after the ModioClient has
		/// been initialized" and keeps the old values anyway, so a config change goes through a restart instead.
		/// </remarks>
		private static ModioSettings Bind(ModIoConfig config)
		{
			var settings = new ModioSettings
			{
				GameId = config.GameId,
				APIKey = config.ApiKey,
				ServerURL = config.ServerUrl,
				DefaultLanguage = config.DefaultLanguage,
				// The values line up one to one; the mirror exists only so the config compiles without the plugin.
				LogLevel = (LogLevel)config.LogLevel,
			};

			ModioServices.BindInstance(settings, ModioServicePriority.DeveloperOverride);

			if (m_servicesBound)
			{
				return settings;
			}

			ModioServices.Bind<IModioLogHandler>()
				.FromNew<ModioUnityLogger>(ModioServicePriority.EngineImplementation);

			ModioServices.Bind<IModioAPIInterface>()
				.FromNew<ModioAPIUnityClient>(ModioServicePriority.EngineImplementation);

			// One long lived auth service; the prompt that drives it is swapped per sign in attempt instead, because
			// the prompt belongs to whichever window started the flow.
			m_authService = new ModioEmailAuthService();
			ModioServices.Bind<IModioAuthService>()
				.FromInstance(m_authService, ModioServicePriority.DeveloperOverride);

			m_servicesBound = true;
			return settings;
		}

		/// <summary>
		/// Brings the client up, restarting it when the config changed since it was initialized.
		/// </summary>
		/// <remarks>
		/// Callers overlap in practice - opening the uploader window starts a fetch from two places at once, and any
		/// action can be triggered while another is still initializing. Checking <c>ModioClient.IsInitialized</c> is
		/// not enough to serialise them: it stays false for the whole of <c>Init</c>, so a second caller would walk
		/// straight past it and rebind the settings mid-initialization, which the SDK reports as
		/// "you have changed the ModioSettings after the ModioClient has been initialized". Overlapping callers
		/// therefore share the one in flight operation.
		/// </remarks>
		internal static Task<ModOperationResult> InitializeAsync(ModIoConfig config)
		{
			if (m_initialization is { IsCompleted: false })
			{
				return m_initialization;
			}

			m_initialization = InitializeCoreAsync(config);
			return m_initialization;
		}

		private static async Task<ModOperationResult> InitializeCoreAsync(ModIoConfig config)
		{
			var fingerprint = Fingerprint(config);

			if (ModioClient.IsInitialized)
			{
				if (fingerprint == m_activeFingerprint)
				{
					// Nothing changed - and nothing is touched, because rebinding here is exactly what the SDK
					// warns about. This path runs before every action, so it has to stay side effect free.
					return ModOperationResult.Ok();
				}

				// The cached auth token lives on disk, so a restart does not usually cost the user a new sign in.
				await ModioClient.Shutdown();
			}

			var movedEndpoint = m_activeServerUrl != null && m_activeServerUrl != config.ServerUrl;

			var settings = Bind(config);
			var error = await ModioClient.Init(settings);

			if (error)
			{
				m_activeFingerprint = null;
				return ModOperationResult.Fail($"mod.io failed to initialize: {error.GetMessage()}");
			}

			m_activeFingerprint = fingerprint;
			m_activeServerUrl = config.ServerUrl;

			if (movedEndpoint)
			{
				// The auth token is cached on disk and survives a restart, but it was minted by whichever endpoint
				// issued it. Carrying one across endpoints leaves a session that authenticates yet fails on the
				// calls that matter, so the sign in is dropped and asked for again.
				await User.LogOut();

				return ModOperationResult.Ok("The mod.io endpoint changed, so the previous sign in was dropped. " +
				                             "Sign in again.");
			}

			return ModOperationResult.Ok();
		}

		/// <summary>Taken from the config rather than from bound settings, so it can be compared without binding.</summary>
		private static string Fingerprint(ModIoConfig config)
		{
			return $"{config.GameId}|{config.ApiKey}|{config.ServerUrl}|{config.DefaultLanguage}|{config.LogLevel}";
		}
	}
}
