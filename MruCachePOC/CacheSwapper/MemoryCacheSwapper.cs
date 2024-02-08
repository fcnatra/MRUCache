using System.IO.Compression;
using System.Text.Json;

namespace CacheSwapper;

public class MemoryCacheSwapper : ICacheSwapper
{
	private Dictionary<object, object> _cachedEntries = new Dictionary<object, object>();
	private byte[] _compressedCache = [];

	public IEnumerable<object> Dump(Dictionary<object, object> entries, IEnumerable<object> keysToDump)
	{
		var addedKeys = new List<object>();

		foreach (var k in keysToDump)
		{
			if (TryAdd(k, entries[k]))
			{
				addedKeys.Add(k);
				entries.Remove(k);
			}
		}

		return addedKeys;
	}

	public bool Recover(Dictionary<object, object> entries, object key)
	{
		var cachedEntries = DecompressCache();
		bool wasRecoveredFine = cachedEntries.TryGetValue(key, out object? entry);

		if (wasRecoveredFine)
		{
			entries.Add(key, entry);
			cachedEntries.Remove(key);
			CompressCache(cachedEntries);
		}

		return wasRecoveredFine;
	}

	private bool TryAdd(object key, object value)
	{
		var entries = DecompressCache();

		var addedOk = entries.TryAdd(key, value);

		if (addedOk)
			CompressCache(entries);

		return addedOk;
	}

	private void CompressCache(Dictionary<object, object> cachedEntries)
	{
		if (cachedEntries.Count == 0)
			return;

		JsonSerializerOptions serializationOptions = SetupSerializationOptions();
		var jsonString = JsonSerializer.Serialize(cachedEntries, serializationOptions);

		using (MemoryStream memStream = new MemoryStream())
		{
			using (StreamWriter writer = new(memStream))
			{
				writer.Write(jsonString);
				writer.Flush();
				memStream.Seek(0, SeekOrigin.Begin);
				using (var compressedStream = new MemoryStream())
				using (var gZipStream = new GZipStream(compressedStream, CompressionMode.Compress))
				{
					memStream.CopyTo(gZipStream);
					gZipStream.Flush();
					_compressedCache = compressedStream.ToArray();
				}
			}
		}
	}

	private Dictionary<object, object> DecompressCache()
	{
		Dictionary<object, object> cachedEntries = new Dictionary<object, object>();

		if (_compressedCache == null || _compressedCache.Length == 0)
			return cachedEntries;

		using (var compressedStream = new MemoryStream(_compressedCache))
		using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
		using (var decompressedStream = new MemoryStream())
		{
			gzipStream.CopyTo(decompressedStream);
			decompressedStream.Seek(0, SeekOrigin.Begin);
			using (var reader = new StreamReader(decompressedStream))
			{
				string jsonString = reader.ReadToEnd();
				JsonSerializerOptions deserializationOptions = SetupDeserializationOptions();
				cachedEntries = (Dictionary<object, object>?)JsonSerializer.Deserialize<object>(jsonString, deserializationOptions) ?? [];
			}
		}

		return cachedEntries;
	}

	private static JsonSerializerOptions SetupDeserializationOptions()
	{
		// needed to avoid getting JsonElement.JsonValueKind values instead of actual values
		var deserializationOptions = new JsonSerializerOptions();
		deserializationOptions.Converters.Add(new CacheDeserializerJsonConverter());
		return deserializationOptions;
	}

	private static JsonSerializerOptions SetupSerializationOptions()
	{
		// needed to avoid getting JsonElement.JsonValueKind values instead of actual values
		var serializationOptions = new JsonSerializerOptions();
		serializationOptions.Converters.Add(new ByteArrayJsonConverter());
		return serializationOptions;
	}
}
