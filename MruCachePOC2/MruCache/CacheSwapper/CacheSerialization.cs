using System.Text.Json;
using System.Text.Json.Serialization;

namespace MruCache.CacheSwapper;
#nullable enable

internal static class CacheSerialization
{
	internal static JsonSerializerOptions GetOptionsWith(JsonConverter converter)
	{
		var deserializationOptions = new JsonSerializerOptions();
		deserializationOptions.Converters.Add(converter);
		return deserializationOptions;
	}
}
