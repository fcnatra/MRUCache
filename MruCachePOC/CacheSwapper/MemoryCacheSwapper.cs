using System.IO.Compression;
using System.Text.Json;

namespace CacheSwapper;

public class MemoryCacheSwapper<T> : ICacheSwapper<T> where T : class
{
	private byte[] _compressedCache = [];

	public IEnumerable<object> Dump(Dictionary<object, T> entries, IEnumerable<object> keysToDump)
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

	public bool Recover(Dictionary<object, T> entries, object key)
	{
		var cachedEntries = DecompressCache();
		bool wasRecoveredFine = cachedEntries.TryGetValue(key, out T? entry);

		if (wasRecoveredFine)
		{
			entries.Add(key, (T)entry);
			cachedEntries.Remove(key);
			CompressCache(cachedEntries);
		}

		return wasRecoveredFine;
	}

	private bool TryAdd(object key, T value)
	{
		var entries = DecompressCache();

		var addedOk = entries.TryAdd(key, value);

		if (addedOk)
			CompressCache(entries);

		return addedOk;
	}

	private void CompressCache(Dictionary<object, T> cachedEntries)
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

	private Dictionary<object, T> DecompressCache()
	{
		Dictionary<object, T> cachedEntries = [];

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
				var deserializedObject = JsonSerializer.Deserialize<object>(jsonString, deserializationOptions);
				cachedEntries = (Dictionary<object, T>?)deserializedObject ?? [];
			}
		}

		return cachedEntries;
	}

	private static JsonSerializerOptions SetupDeserializationOptions()
	{
		// needed to avoid getting JsonElement.JsonValueKind values instead of actual values
		var deserializationOptions = new JsonSerializerOptions();
		deserializationOptions.Converters.Add(new CacheDeserializerJsonConverter<T>());
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
