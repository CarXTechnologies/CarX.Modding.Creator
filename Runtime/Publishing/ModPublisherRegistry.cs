using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Finds every <see cref="IModPublisher"/> that got compiled into the project and instantiates them on demand.
	/// A host project picks a vendor by id; the registry is the only place that knows which vendors exist at all.
	/// </summary>
	public static class ModPublisherRegistry
	{
		/// <summary>Descriptor of a vendor that is available in this project, without instantiating it.</summary>
		public sealed class Entry
		{
			public string VendorId { get; }

			public string DisplayName { get; }

			public int Order { get; }

			internal Type ImplementationType { get; }

			internal Entry(string vendorId, string displayName, int order, Type implementationType)
			{
				VendorId = vendorId;
				DisplayName = displayName;
				Order = order;
				ImplementationType = implementationType;
			}
		}

		private static Entry[] m_entries;

		/// <summary>Vendors compiled into this project, ordered by <see cref="ModPublisherAttribute.Order"/>.</summary>
		public static IReadOnlyList<Entry> Entries => m_entries ??= Discover();

		/// <summary>
		/// Drops the cached scan. Call after the domain reloads with a different set of vendor assemblies, which in
		/// the editor happens whenever a vendor SDK is added or removed.
		/// </summary>
		public static void Invalidate()
		{
			m_entries = null;
		}

		public static bool TryGetEntry(string vendorId, out Entry entry)
		{
			foreach (var candidate in Entries)
			{
				if (string.Equals(candidate.VendorId, vendorId, StringComparison.OrdinalIgnoreCase))
				{
					entry = candidate;
					return true;
				}
			}

			entry = null;
			return false;
		}

		/// <summary>
		/// Instantiates the vendor registered under <paramref name="vendorId"/>.
		/// Returns a failure result rather than throwing when the vendor is missing, because "the SDK for this vendor
		/// is not installed" is an ordinary state the UI has to render, not an exceptional one.
		/// </summary>
		public static ModOperationResult<IModPublisher> Create(string vendorId, ModPublisherContext context)
		{
			if (string.IsNullOrWhiteSpace(vendorId))
			{
				return ModOperationResult<IModPublisher>.Fail("No mod publisher vendor selected.");
			}

			if (!TryGetEntry(vendorId, out var entry))
			{
				return ModOperationResult<IModPublisher>.Fail(
					$"Mod publisher '{vendorId}' is not available in this project. " +
					$"Available: {(Entries.Count == 0 ? "none" : string.Join(", ", Entries.Select(e => e.VendorId)))}.");
			}

			try
			{
				var publisher = (IModPublisher)Activator.CreateInstance(entry.ImplementationType, context);
				return ModOperationResult<IModPublisher>.Ok(publisher);
			}
			catch (Exception exception)
			{
				// TargetInvocationException hides the real cause one level down, and that inner message is the only
				// part worth showing - a vendor constructor failing usually means its config asset is not filled in.
				var reason = exception is TargetInvocationException { InnerException: not null } invocation
					? invocation.InnerException.Message
					: exception.Message;

				return ModOperationResult<IModPublisher>.Fail($"Failed to create mod publisher '{vendorId}': {reason}");
			}
		}

		private static Entry[] Discover()
		{
			var found = new List<Entry>();
			var publisherType = typeof(IModPublisher);

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;

				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException exception)
				{
					// A half loadable assembly still tends to contain the types we want, so keep what resolved.
					types = exception.Types.Where(type => type != null).ToArray();
				}
				catch (Exception)
				{
					continue;
				}

				foreach (var type in types)
				{
					if (type.IsAbstract || type.IsInterface || !publisherType.IsAssignableFrom(type))
					{
						continue;
					}

					var attribute = type.GetCustomAttribute<ModPublisherAttribute>();
					if (attribute == null)
					{
						continue;
					}

					if (type.GetConstructor(new[] { typeof(ModPublisherContext) }) == null)
					{
						Debug.LogError($"{type.FullName} is marked with {nameof(ModPublisherAttribute)} but has no " +
						               $"public constructor taking a {nameof(ModPublisherContext)}; it will be ignored.");
						continue;
					}

					found.Add(new Entry(attribute.VendorId, attribute.DisplayName, attribute.Order, type));
				}
			}

			return found
				.OrderBy(entry => entry.Order)
				.ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}
	}
}
