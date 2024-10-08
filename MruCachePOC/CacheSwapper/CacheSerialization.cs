using System.Text.Json;
using System.Text.Json.Serialization;

namespace CacheSwapper.Serializers;

internal static class CacheSerialization
{
	internal static JsonSerializerOptions GetOptionsWith(JsonConverter converter)
	{
		var deserializationOptions = new JsonSerializerOptions();
		deserializationOptions.Converters.Add(converter);
		return deserializationOptions;
	}
}

internal static class ArraySerializer
{
	public static byte[]? ReadByteArray(ref Utf8JsonReader reader)
	{
		byte[]? result;
		var array = new List<byte>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			array.Add(reader.GetByte());
		result = array.ToArray();
		return result;
	}

	public static void WriteByteArray(Utf8JsonWriter writer, byte[] value)
	{
		writer.WriteStartArray();

		foreach (byte b in value)
			writer.WriteNumberValue(b);

		writer.WriteEndArray();
	}
}

internal class ByteArrayJsonConverter : JsonConverter<byte[]>
{
	/// <summary>
	/// ORIGINAL CODE FROM: https://github.com/dotnet/runtime/issues/31408#issuecomment-550347895
	/// </summary>
	public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		byte[]? result = null;
		var type = reader.TokenType;

		switch (type)
		{
			case JsonTokenType.StartArray:
				result = ArraySerializer.ReadByteArray(ref reader);
				break;

			default:
				break;
		}

		return result;
	}

	public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
	{
		ArraySerializer.WriteByteArray(writer, value);
	}
}

internal class DictionaryJsonConverter<T> : JsonConverter<Dictionary<object, T>> where T : class
{
	public override Dictionary<object, T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException($"Unexpected token type: {reader.TokenType}");

		var dictionary = new Dictionary<object, T>();

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			if (reader.TokenType != JsonTokenType.PropertyName)
				throw new JsonException($"Unexpected token type: {reader.TokenType}");

			string? propertyName = reader.GetString() ?? throw new JsonException("Property name can't be NULL");
			reader.Read();

			object? propertyValue = null;
			switch (reader.TokenType)
			{
				case JsonTokenType.String:
					propertyValue = reader.GetString();
					break;

				case JsonTokenType.Number:
					if (reader.TryGetByte(out byte b))
						propertyValue = b;
					else if (reader.TryGetInt32(out var valInt))
						propertyValue = valInt;
					else if (reader.TryGetInt64(out var valLong))
						propertyValue = valLong;
					else if (reader.TryGetDouble(out var valDouble))
						propertyValue = valDouble;
					break;

				case JsonTokenType.True:
					propertyValue = true;
					break;

				case JsonTokenType.False:
					propertyValue = false;
					break;

				case JsonTokenType.StartArray:
					var list = new List<object>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						list.Add(Read(ref reader, typeof(object), options));
					}
					propertyValue = list.ToArray();
					break;

				case JsonTokenType.StartObject:
					propertyValue = JsonSerializer.Deserialize(ref reader, typeof(T));
					foreach (var property in typeof(T).GetProperties())
					{
						var objectValue = property.GetValue(propertyValue);
						if (objectValue is JsonElement)
							property.SetValue(propertyValue, ((JsonElement)objectValue).GetString());
					}
					break;

				default:
					throw new JsonException($"Unexpected token type: {reader.TokenType}");
			}

			AddValueToTheDictionary(dictionary, propertyName, propertyValue);
		}

		return dictionary;
	}

	private static void AddValueToTheDictionary(Dictionary<object, T> dictionary, string propertyName, object? propertyValue)
	{
#pragma warning disable 8604 //Possible null reference for parameter "value" in "void Dictionary<object, T>.Add(object key, T value)
		dictionary.Add(propertyName, (T?)propertyValue);
#pragma warning restore 8604
	}

	public override void Write(Utf8JsonWriter writer, Dictionary<object, T> value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		foreach (var kvp in value)
		{
			//writer.WritePropertyName(kvp.Key.ToString());
			//JsonSerializer.Serialize(writer, kvp.Value, options);

			ObjectSerializer.Write(writer, kvp.Key, options);
			ObjectSerializer.Write(writer, kvp.Value, options);
		}

		writer.WriteEndObject();
	}
}

internal static class ObjectSerializer
{
	public static object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var type = reader.TokenType;

		if (type == JsonTokenType.Number)
		{
			if (reader.TryGetInt32(out var valInt))
				return valInt;

			if (reader.TryGetInt64(out var valLong))
				return valLong;

			if (reader.TryGetDouble(out var valDouble))
				return valDouble;
		}

		if (type == JsonTokenType.String)
			return reader.GetString();

		if (type == JsonTokenType.True || type == JsonTokenType.False)
			return reader.GetBoolean();

		if (type == JsonTokenType.StartArray)
			return ArraySerializer.ReadByteArray(ref reader);

		using var document = JsonDocument.ParseValue(ref reader);
		return document.RootElement.Clone();
	}

	public static void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}

		Type type = value.GetType();

		if (type == typeof(byte))
		{
			writer.WriteNumberValue((byte)value);
		}
		else if (type == typeof(int))
		{
			writer.WriteNumberValue((int)value);
		}
		else if (type == typeof(long))
		{
			writer.WriteNumberValue((long)value);
		}
		else if (type == typeof(double))
		{
			writer.WriteNumberValue((double)value);
		}
		else if (type == typeof(string))
		{
			writer.WriteStringValue((string)value);
		}
		else if (type == typeof(bool))
		{
			writer.WriteBooleanValue((bool)value);
		}
		else if (type.IsArray)
		{
			ArraySerializer.WriteByteArray(writer, (byte[])value);
		}
		else
		{
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}

internal class ObjectJsonConverter : JsonConverter<object>
{
	public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return ObjectSerializer.Read(ref reader, typeToConvert, options);
	}

	public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
	{
		ObjectSerializer.Write(writer, value, options);
	}
}
