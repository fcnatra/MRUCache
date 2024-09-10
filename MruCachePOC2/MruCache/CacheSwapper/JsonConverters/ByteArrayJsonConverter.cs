using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MruCache.CacheSwapper.JsonConverters;

#nullable enable
internal class ByteArrayJsonConverter : JsonConverter<byte[]>
{
    /// <summary>
    /// ORIGINAL CODE FROM: https://github.com/dotnet/runtime/issues/31408#issuecomment-550347895
    /// </summary>
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.StartArray)
            return ArraySerializer.ReadByteArray(ref reader);

        return null;
    }

	public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
	{
		ArraySerializer.WriteByteArray(writer, value);
	}
}
