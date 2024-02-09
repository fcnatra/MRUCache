using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CacheSwapper;

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

internal class CacheDeserializerJsonConverter<T> : JsonConverter<object> where T: class
{
	public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException($"Unexpected token type: {reader.TokenType}");

		var dictionary = new Dictionary<object, T>();

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			if (reader.TokenType != JsonTokenType.PropertyName)
				throw new JsonException($"Unexpected token type: {reader.TokenType}");

			var propertyName = reader.GetString();

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
					//propertyValue = Read(ref reader, typeof(object), options); // Recursive call for nested objects
					propertyValue = JsonSerializer.Deserialize(ref reader, typeof(T));
					break;

				default:
					throw new JsonException($"Unexpected token type: {reader.TokenType}");
			}

			// Add the property to the dictionary
			dictionary.Add(propertyName, (T?)propertyValue);
		}

		return dictionary;
	}

	public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
	{
		throw new NotImplementedException();
	}
}

internal class ObjectDeserializerJsonConverter : JsonConverter<object>
{
	public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
			//writer.WriteStartArray();

			//foreach (var item in (Array)value)
			//	Write(writer, item, options);

			//writer.WriteEndArray();
		}
		else
		{
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}
