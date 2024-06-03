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

internal class CacheDeserializerJsonConverter<T> : JsonConverter<Dictionary<object, T>> where T : class
{
	/// <exception cref="JsonException"/>
	public override Dictionary<object, T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		ValidateThisIsStartObject(reader);

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
					propertyValue = JsonSerializer.Deserialize(ref reader, typeof(T), options);
					foreach (var property in typeof(T).GetProperties())
					{
						var objectValue = property.GetValue(propertyValue);
						if (objectValue is JsonElement)
						{
							var deserializedValue = GetTypedValueFromJsonElement((JsonElement)objectValue);//((JsonElement)objectValue).GetString();
							property.SetValue(propertyValue, deserializedValue);
						}
					}
					break;

				default:
					throw new JsonException($"Unexpected token type: {reader.TokenType}");
			}

			AddValueToTheDictionary(dictionary, propertyName, propertyValue);
		}

		return dictionary;
	}

	private object? GetTypedValueFromJsonElement(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				return element.GetString();

			case JsonValueKind.Number:
				if (element.TryGetByte(out byte byteValue)) return byteValue;
				else if (element.TryGetInt16(out short shortValue)) return shortValue;
				else if (element.TryGetInt32(out int intValue)) return intValue;
				else if (element.TryGetInt64(out long longValue)) return longValue;
				else if (element.TryGetDecimal(out decimal decimalValue)) return decimalValue;
				else if (element.TryGetDouble(out double doubleValue)) return doubleValue;
				break;

			case JsonValueKind.Array:
				var array = new List<object?>();
				foreach (JsonElement arrayElement in element.EnumerateArray())
				{
					var arrayElementValue = GetTypedValueFromJsonElement(arrayElement);
					array.Add(arrayElementValue);
				}
				return array.ToArray();

			default:
				if (element.TryGetDateTime(out DateTime dateTime)) return dateTime;
				else if (element.TryGetGuid(out Guid guidValue)) return guidValue;
				else if (element.TryGetBytesFromBase64(out byte[]? bytes)) return bytes;
				else if (element.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset)) return dateTimeOffset;
				break;
		}

		return element.GetString();
	}

	private static void ValidateThisIsStartObject(Utf8JsonReader reader)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException($"Unexpected token type: {reader.TokenType}");
	}

	private static void AddValueToTheDictionary(Dictionary<object, T> dictionary, string propertyName, object? propertyValue)
	{
#pragma warning disable 8604 //Possible null reference for parameter "value" in "void Dictionary<object, T>.Add(object key, T value)
		dictionary.Add(propertyName, (T?)propertyValue);
#pragma warning restore 8604
	}

	public override void Write(Utf8JsonWriter writer, Dictionary<object, T> value, JsonSerializerOptions options)
	{
		throw new NotImplementedException();
	}
}

internal class ObjectDeserializerJsonConverter : JsonConverter<object>
{
	public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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

	public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
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
