using System.Collections.Generic;
using System.Text.Json;

namespace MruCache.CacheSwapper.JsonConverters;

#nullable enable
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
