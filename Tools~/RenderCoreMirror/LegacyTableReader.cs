using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>
	/// Читает таблицу ModRenderCoreVariants из собранной CarX.Modding.Creator.dll по reflection (отдельный AssemblyLoadContext, UnityEngine из
	/// --unity-managed или из папки dll) и дампит её как MirrorTableDump. Понимает и старую форму (поле emission, без vertexColors и
	/// доступности): отсутствующее поле в дамп не попадает, и сравнение покажет его как новое.
	/// </summary>
	internal static class LegacyTableReader
	{
		private const string TableType = "Plugins.CarX.Modding.Creator.Runtime.ModRenderCoreVariants";

		public static SortedDictionary<string, string> Read(string dllPath, IReadOnlyList<string> probeDirectories)
		{
			string fullPath = Path.GetFullPath(dllPath);
			var directories = new List<string> { Path.GetDirectoryName(fullPath) };
			directories.AddRange(probeDirectories.Select(Path.GetFullPath));

			var context = new AssemblyLoadContext("legacy-table", isCollectible: true);
			context.Resolving += (loadContext, name) => Resolve(loadContext, name, directories);

			try
			{
				Assembly assembly = context.LoadFromAssemblyPath(fullPath);
				Type table = assembly.GetType(TableType, throwOnError: true);
				IEnumerable variants = (table.GetProperty("All", BindingFlags.Public | BindingFlags.Static)?.GetValue(obj: null) ??
					table.GetField("s_variants", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(obj: null)) as IEnumerable;

				if (variants == null)
				{
					throw new InvalidOperationException($"{TableType} has neither All nor s_variants");
				}

				return Dump(variants);
			}
			finally
			{
				context.Unload();
			}
		}

		private static SortedDictionary<string, string> Dump(IEnumerable variants)
		{
			var dump = new SortedDictionary<string, string>(StringComparer.Ordinal);
			var names = new List<string>();

			foreach (object variant in variants)
			{
				string name = (string)Get(variant, "name");
				names.Add(name);
				string prefix = name + ".";
				Put(dump, prefix + "id", variant, "id");
				Put(dump, prefix + "queue", variant, "queue");
				Put(dump, prefix + "renderType", variant, "renderType");
				Put(dump, prefix + "keywords", variant, "keywords");
				Put(dump, prefix + "vertexColors", variant, "vertexColors");
				Put(dump, prefix + "requiredStreams", variant, "requiredStreams");
				Put(dump, prefix + "unavailableReason", variant, "unavailableReason");
				Put(dump, prefix + "revision", variant, "revision");

				// Старая таблица: поле emission; новая — свойство Emission (vertexColors == EmissionMultiplier).
				object emission = Get(variant, "emission") ?? Get(variant, "Emission");

				if (emission != null)
				{
					dump[prefix + "emission"] = Format(emission);
				}

				object available = Get(variant, "Available");
				dump[prefix + "available"] = available == null ? MirrorTableDump.Bool(value: true) : Format(available);

				IEnumerable properties = (IEnumerable)Get(variant, "properties");
				var propertyNames = new List<string>();

				foreach (object property in properties)
				{
					string propertyName = (string)Get(property, "name");
					propertyNames.Add(propertyName);
					string key = prefix + propertyName + ".";

					foreach (string field in new[] { "label", "tooltip", "type", "source", "layer", "defaultValue", "vectorLabels", "channels" })
					{
						Put(dump, key + field, property, field);
					}
				}

				dump[prefix + "properties"] = MirrorTableDump.List(propertyNames);
			}

			dump["variants"] = MirrorTableDump.List(names);
			return dump;
		}

		private static void Put(SortedDictionary<string, string> dump, string key, object owner, string member)
		{
			if (HasMember(owner, member))
			{
				dump[key] = Format(Get(owner, member));
			}
		}

		private static bool HasMember(object owner, string member)
		{
			Type type = owner.GetType();
			return type.GetField(member, BindingFlags.Public | BindingFlags.Instance) != null ||
				type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) != null;
		}

		private static object Get(object owner, string member)
		{
			Type type = owner.GetType();
			FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance);

			if (field != null)
			{
				return field.GetValue(owner);
			}

			PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
			return property?.GetValue(owner);
		}

		/// <summary>Как MirrorTableDump: null → "null", enum → имя, bool → true/false, Vector4 → MirrorVector, списки → [a|b].</summary>
		private static string Format(object value)
		{
			switch (value)
			{
				case null:
					return MirrorTableDump.Null;
				case string text:
					return text;
				case bool flag:
					return MirrorTableDump.Bool(flag);
				case int number:
					return MirrorTableDump.Int(number);
				case Enum enumValue:
					return enumValue.ToString();
				case IEnumerable items:
					return MirrorTableDump.List(items.Cast<object>().Select(Format));
			}

			Type type = value.GetType();

			if (type.FullName == "UnityEngine.Vector4")
			{
				return new MirrorVector(Component(value, "x"), Component(value, "y"), Component(value, "z"), Component(value, "w")).ToString();
			}

			throw new InvalidOperationException($"unknown table field type {type.FullName}");
		}

		private static float Component(object vector, string name)
		{
			return (float)vector.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(vector);
		}

		private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name, IReadOnlyList<string> directories)
		{
			foreach (string directory in directories)
			{
				string candidate = Path.Combine(directory, name.Name + ".dll");

				if (File.Exists(candidate))
				{
					return context.LoadFromAssemblyPath(candidate);
				}
			}

			return null;
		}
	}
}
