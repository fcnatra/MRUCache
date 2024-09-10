using System.Text.Json;
using System.Text.Json.Serialization;

namespace MruCache.CacheSwapper.JsonConverters;

#nullable enable
internal class DictionaryJsonConverter<T> : JsonConverter<Dictionary<object, T>> where T: class
{
    /// <exception cref="JsonException"></exception>
	public override Dictionary<object, T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException($"Unexpected token type: {reader.TokenType}");

		var dictionary = new Dictionary<object, T>();

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            (string propertyName, object? propertyValue) = ReadProperty(ref reader, options);
            AddValueToTheDictionary(dictionary, propertyName, propertyValue);
        }

        return dictionary;
	}

    private object? ProcessValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var content = reader.GetString();
                if (DateTime.TryParse(content, out DateTime dateTime))
					return dateTime;
                return content;

            case JsonTokenType.Number:
                if (reader.TryGetByte(out byte b)) return b;

                if (reader.TryGetInt32(out int valInt)) return valInt;

                if (reader.TryGetInt64(out long valLong)) return valLong;

                if (reader.TryGetDouble(out double valDouble)) return valDouble;

                throw new JsonException($"Unexpected token type: {reader.TokenType}");

            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetBoolean();

            case JsonTokenType.StartArray: return ArraySerializer.ReadByteArray(ref reader);// ReadArray(ref reader, options);

            case JsonTokenType.StartObject: return ReadObject(ref reader, options);

            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    private object? ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
		var resultObject = typeof(T).GetConstructor(new Type[] { })?.Invoke(new object[] { });
		var objectProperties = typeof(T).GetProperties();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            (string propertyName, object? propertyValue) = ReadProperty(ref reader, options);
            objectProperties.First(p => p.Name == propertyName).SetValue(resultObject, propertyValue);
        }

        return resultObject;
    }

	private (string propertyName, object? propertyValue) ReadProperty(ref Utf8JsonReader reader, JsonSerializerOptions options)
	{
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Unexpected token type: {reader.TokenType}");

        string propertyName = reader.GetString() ?? throw new JsonException("Property name can't be NULL");

        reader.Read();
        object? propertyValue = ProcessValue(ref reader, options);

        return (propertyName, propertyValue);
    }

    private object ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        object? propertyValue;
        var list = new List<object>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(Read(ref reader, typeof(object), options));
        }
        propertyValue = list.ToArray();
        return propertyValue;
    }

    private static void AddValueToTheDictionary(Dictionary<object, T> dictionary, string propertyName, object? propertyValue)
    {
		#pragma warning disable 8604 //Possible null reference for parameter "value" in "void Dictionary<object, T>.Add(object key, T value)
        dictionary.Add(propertyName, (T?)propertyValue);
		#pragma warning restore 8604
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<object, T> dictionary, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		foreach (var element in dictionary)
		{
			Write(writer, element.Key, options);
			Write(writer, element.Value, options);
		}

		writer.WriteEndObject();
	}

    private static void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}

		if (value is byte)
		{
			writer.WriteNumberValue((byte)value);
			return;
		}
		
		if (value is int)
		{
			writer.WriteNumberValue((int)value);
			return;
		}

		if (value is long)
		{
			writer.WriteNumberValue((long)value);
			return;
		}
		
		if (value is double)
		{
			writer.WriteNumberValue((double)value);
			return;
		}
		
		if (value is string)
		{
			writer.WriteStringValue((string)value);
			return;
		}

		if (value is bool)
		{
			writer.WriteBooleanValue((bool)value);
			return;
		}

        if (value is Array)
		{
			ArraySerializer.WriteByteArray(writer, (byte[])value);
			return;
		}

		JsonSerializer.Serialize(writer, value, value.GetType(), options);
	}

}
