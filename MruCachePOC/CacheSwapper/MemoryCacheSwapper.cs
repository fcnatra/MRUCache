using CacheSwapper.Serializers;
using System.IO.Compression;
using System.Text.Json;

namespace CacheSwapper;

public class MemoryCacheSwapper<T> : ICacheSwapper<T> where T : class
{
	private byte[] _compressedCache = [];

	public IEnumerable<object> Dump(Dictionary<object, T> entries, IEnumerable<object> keysToDump)
	{
		var cachedEntries = DecompressCache();

		List<object> addedKeys = keysToDump.Where(k => cachedEntries.TryAdd(k, entries[k])).ToList();

		foreach (var key in addedKeys) entries.Remove(key);

		if (addedKeys.Any())
			CompressCache(cachedEntries);

		return addedKeys;
	}

	public bool Recover(Dictionary<object, T?> entries, object key)
	{
		var cachedEntries = DecompressCache();
		bool wasRecoveredFine = cachedEntries.TryGetValue(key, out T? entry);

		if (wasRecoveredFine)
		{
			entries.Add(key, (T?)entry);
			cachedEntries.Remove(key);
			CompressCache(cachedEntries);
		}

		return wasRecoveredFine;
	}

	private void CompressCache(Dictionary<object, T> entries)
	{
		if (entries.Count == 0)
		{
			_compressedCache = [];
			return;
		}

		JsonSerializerOptions serializationOptions = CacheSerialization.GetOptionsWith(new ByteArrayJsonConverter());
		var jsonString = JsonSerializer.Serialize(entries, serializationOptions);

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

		JsonSerializerOptions deserializationOptions = CacheSerialization.GetOptionsWith(new CacheDeserializerJsonConverter<T>());


		using (var compressedStream = new MemoryStream(_compressedCache))
		using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
		using (var decompressedStream = new MemoryStream())
		{
			gzipStream.CopyTo(decompressedStream);
			decompressedStream.Seek(0, SeekOrigin.Begin);
			using (var reader = new StreamReader(decompressedStream))
			{
				string jsonString = reader.ReadToEnd();
				var deserializedObject = JsonSerializer.Deserialize<Dictionary<object, T>>(jsonString, deserializationOptions);
				cachedEntries = deserializedObject ?? [];
			}
		}

		return cachedEntries;
	}
}
