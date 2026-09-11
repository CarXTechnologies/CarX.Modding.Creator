using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    // Only the caller's known DTO schema is instantiated. No CLR type names or native layouts are loaded from a map.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class BinaryBlobAttribute : Attribute
    {
        public readonly string bytesField;
        public BinaryBlobAttribute(string bytesField) => this.bytesField = bytesField;
    }

    public static class BinaryModData
    {
        private const uint Magic = 0x44425843; // CXBD
        private const int MaxBytes = 256 * 1024 * 1024;
        private static readonly Dictionary<Type, FieldInfo[]> Schemas = new();
        private static bool IsLodDistance(Type type, FieldInfo field) => type == typeof(LodInstance) && (field.Name == nameof(LodInstance.LODDistances0) || field.Name == nameof(LodInstance.LODDistances1));
        public static bool IsBinary(byte[] bytes) => bytes != null && bytes.Length >= 4 && BitConverter.ToUInt32(bytes, 0) == Magic;
        private static FieldInfo[] Fields(Type type)
        {
            lock (Schemas)
            {
                if (Schemas.TryGetValue(type, out var fields)) return fields;
                if (type.Namespace != typeof(ModMeta).Namespace && type.Namespace != "UnityEngine")
                    throw new InvalidDataException("Unsupported binary data schema: " + type.Name);
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => !f.IsNotSerialized).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
                if (fields.Length == 0) throw new InvalidDataException("Empty binary schema: " + type.Name);
                return Schemas[type] = fields;
            }
        }
        public static byte[] Write(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Magic); writer.Write(1);
            var state = new Writer(writer);
            state.Text(value.GetType().Name);
            state.Value(value.GetType(), value, 0);
            if (stream.Length > MaxBytes) throw new InvalidDataException("Binary document is too large.");
            return stream.ToArray();
        }
        public static T Read<T>(byte[] bytes) => (T)Read(bytes, typeof(T));
        public static object Read(byte[] bytes, Type type)
        {
            if (bytes == null || bytes.Length > MaxBytes) throw new InvalidDataException("Invalid binary document size.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported binary document version.");
            var state = new Reader(reader);
            if (state.Text() != type.Name) throw new InvalidDataException("Binary document type does not match " + type.Name);
            object result = state.Value(type, 0);
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing binary document data.");
            return result;
        }
        private sealed class Writer
        {
            private readonly BinaryWriter w;
            private readonly Dictionary<string, int> strings = new(StringComparer.Ordinal);
            public Writer(BinaryWriter writer) => w = writer;
            public void Text(string text)
            {
                if (text == null) { w.Write(-1); return; }
                if (strings.TryGetValue(text, out int id)) { w.Write(id); return; }
                w.Write(-2); var bytes = Encoding.UTF8.GetBytes(text);
                if (bytes.Length > MaxBytes) throw new InvalidDataException("Binary string is too large.");
                w.Write(bytes.Length); w.Write(bytes); strings.Add(text, strings.Count);
            }
            private void Float(float value) { if (!float.IsFinite(value)) throw new InvalidDataException("Non-finite binary value."); w.Write(value); }
            public void Value(Type type, object value, int depth)
            {
                if (depth > 32) throw new InvalidDataException("Binary data nesting limit exceeded.");
                if (type == typeof(string)) { Text((string)value); return; }
                if (type == typeof(byte[])) { var bytes = (byte[])value; w.Write(bytes?.Length ?? -1); if (bytes != null) w.Write(bytes); return; }
                if (type == typeof(int) || type.IsEnum) { w.Write(Convert.ToInt32(value)); return; }
                if (type == typeof(float)) { if (!float.IsFinite((float)value)) throw new InvalidDataException("Non-finite binary value."); w.Write((float)value); return; }
                if (type == typeof(bool)) { w.Write((bool)value); return; }
                if (type == typeof(Vector2)) { var v = (Vector2)value; Float(v.x); Float(v.y); return; }
                if (type == typeof(Vector3)) { var v = (Vector3)value; Float(v.x); Float(v.y); Float(v.z); return; }
                if (type == typeof(Vector4)) { var v = (Vector4)value; Float(v.x); Float(v.y); Float(v.z); Float(v.w); return; }
                if (type == typeof(Quaternion)) { var v = (Quaternion)value; Float(v.x); Float(v.y); Float(v.z); Float(v.w); return; }
                if (type == typeof(Color)) { var v = (Color)value; Float(v.r); Float(v.g); Float(v.b); Float(v.a); return; }
                if (type == typeof(Bounds)) { var b = (Bounds)value; Value(typeof(Vector3), b.center, depth + 1); Value(typeof(Vector3), b.extents, depth + 1); return; }
                if (!type.IsValueType) { w.Write(value != null); if (value == null) return; }
                if (type.IsArray || typeof(IList).IsAssignableFrom(type))
                {
                    var list = (IList)value;
                    if (list.Count > 2000000) throw new InvalidDataException("Binary array is too large.");
                    w.Write(list.Count); Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    foreach (var item in list) Value(element, item, depth + 1);
                    return;
                }
                var fields = Fields(type); w.Write(fields.Length);
                foreach (var field in fields)
                {
                    Text(field.Name);
                    if (IsLodDistance(type, field))
                    {
                        var distances = (Vector4)field.GetValue(value);
                        for (int i = 0; i < 4; i++) { if (float.IsNaN(distances[i]) || distances[i] < 0) throw new InvalidDataException("Invalid LOD distance."); w.Write(distances[i]); }
                        continue;
                    }
                    var blob = field.GetCustomAttribute<BinaryBlobAttribute>();
                    if (blob != null)
                    {
                        var raw = (byte[])type.GetField(blob.bytesField).GetValue(value);
                        var encoded = (string)field.GetValue(value);
                        Value(typeof(byte[]), raw ?? (string.IsNullOrEmpty(encoded) ? null : Convert.FromBase64String(encoded)), depth + 1);
                    }
                    else Value(field.FieldType, field.GetValue(value), depth + 1);
                }
            }
        }
        private sealed class Reader
        {
            private readonly BinaryReader r;
            private readonly List<string> strings = new();
            private int remainingValues = 8000000;
            public Reader(BinaryReader reader) => r = reader;
            private int Count(int maximum)
            {
                int count = r.ReadInt32();
                if (count < 0 || count > maximum || count > r.BaseStream.Length - r.BaseStream.Position)
                    throw new InvalidDataException("Invalid binary data count.");
                return count;
            }
            private bool Bool()
            {
                byte value = r.ReadByte(); if (value > 1) throw new InvalidDataException("Invalid binary boolean."); return value != 0;
            }
            private float Float() { float f = r.ReadSingle(); if (!float.IsFinite(f)) throw new InvalidDataException("Non-finite binary value."); return f; }
            public string Text()
            {
                int id = r.ReadInt32(); if (id == -1) return null;
                if (id >= 0 && id < strings.Count) return strings[id];
                if (id != -2 || strings.Count >= 2000000) throw new InvalidDataException("Invalid binary string reference.");
                var value = new UTF8Encoding(false, true).GetString(r.ReadBytes(Count(MaxBytes)));
                strings.Add(value); return value;
            }
            public object Value(Type type, int depth)
            {
                if (--remainingValues < 0 || depth > 32) throw new InvalidDataException("Binary data complexity limit exceeded.");
                if (type == typeof(string)) return Text();
                if (type == typeof(byte[]))
                {
                    int count = r.ReadInt32(); if (count == -1) return null;
                    if (count < 0 || count > MaxBytes || count > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("Invalid binary blob size.");
                    return r.ReadBytes(count);
                }
                if (type == typeof(int)) return r.ReadInt32();
                if (type.IsEnum) return Enum.ToObject(type, r.ReadInt32());
                if (type == typeof(float)) return Float();
                if (type == typeof(bool)) return Bool();
                if (type == typeof(Vector2)) return new Vector2(Float(), Float());
                if (type == typeof(Vector3)) return new Vector3(Float(), Float(), Float());
                if (type == typeof(Vector4)) return new Vector4(Float(), Float(), Float(), Float());
                if (type == typeof(Quaternion)) return new Quaternion(Float(), Float(), Float(), Float());
                if (type == typeof(Color)) return new Color(Float(), Float(), Float(), Float());
                if (type == typeof(Bounds)) return new Bounds { center = (Vector3)Value(typeof(Vector3), depth + 1), extents = (Vector3)Value(typeof(Vector3), depth + 1) };
                if (!type.IsValueType && !Bool()) return null;
                if (type.IsArray || typeof(IList).IsAssignableFrom(type))
                {
                    int count = Count(2000000);
                    Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    var list = type.IsArray ? (IList)Array.CreateInstance(element, count) : (IList)Activator.CreateInstance(type);
                    for (int i = 0; i < count; i++) { object item = Value(element, depth + 1); if (type.IsArray) list[i] = item; else list.Add(item); }
                    return list;
                }
                var fields = Fields(type);
                int fieldCount = Count(fields.Length);
                if (fieldCount != fields.Length) throw new InvalidDataException("Binary schema changed: " + type.Name);
                object result = FormatterServices.GetUninitializedObject(type);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < fieldCount; i++)
                {
                    string name = Text(); var field = Array.Find(fields, f => f.Name == name);
                    if (field == null || !seen.Add(name)) throw new InvalidDataException("Invalid binary field: " + name);
                    if (IsLodDistance(type, field))
                    {
                        // +Infinity is the existing renderer's sentinel for an unused LOD slot.
                        var distances = new Vector4();
                        for (int j = 0; j < 4; j++) { float f = r.ReadSingle(); if (float.IsNaN(f) || f < 0) throw new InvalidDataException("Invalid LOD distance."); distances[j] = f; }
                        field.SetValue(result, distances); continue;
                    }
                    var blob = field.GetCustomAttribute<BinaryBlobAttribute>();
                    if (blob != null) type.GetField(blob.bytesField).SetValue(result, Value(typeof(byte[]), depth + 1));
                    else field.SetValue(result, Value(field.FieldType, depth + 1));
                }
                return result;
            }
        }
    }
}
