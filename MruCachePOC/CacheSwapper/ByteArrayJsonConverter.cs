using System.Text.Json;
using System.Text.Json.Serialization;

namespace CacheSwapper;
internal class ByteArrayJsonConverterForSerialization : JsonConverter<object>
{
    /// <summary>
    /// ORIGINAL CODE FROM: https://github.com/dotnet/runtime/issues/31408#issuecomment-550347895
    /// </summary>
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
        {
            return reader.GetString();
        }

        if (type == JsonTokenType.True || type == JsonTokenType.False)
            return reader.GetBoolean();

        if (type == JsonTokenType.StartArray)
        {
            var byteArray = new List<byte>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                byteArray.Add(reader.GetByte());
            return byteArray.ToArray();
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void WriteByteArray(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (byte b in value)
            writer.WriteNumberValue(b);

        writer.WriteEndArray();
    }
}

internal class ByteArrayJsonConverterForDeserialization : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (byte b in value)
            writer.WriteNumberValue(b);

        writer.WriteEndArray();
    }
}
