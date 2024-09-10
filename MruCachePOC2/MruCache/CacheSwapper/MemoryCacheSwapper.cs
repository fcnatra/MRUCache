using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using MruCache.Comparers;
using MruCache.CacheSwapper.JsonConverters;

namespace MruCache.CacheSwapper;

public class MemoryCacheSwapper<T> : ICacheSwapper<T> where T : class
{
	private byte[] _compressedCache = new byte[] { };

	public void Dispose()
	{
		System.Array.Clear(_compressedCache);
	}

	public IEnumerable<object> Dump(Dictionary<object, T> entries, List<object> keysToDump)
	{
		var cachedEntries = DecompressCache();

		List<object> keysToAdd = keysToDump.Where(k => cachedEntries.TryAdd(k, entries[k])).ToList();

		if (keysToAdd.Any())
			CompressCache(cachedEntries);

		foreach (var key in keysToAdd) entries.Remove(key);

		return keysToAdd;
	}

	#nullable enable
	public bool Recover(Dictionary<object, T> entries, object key)
	{
		var cachedEntries = DecompressCache();
		bool wasRecoveredFine = cachedEntries.TryGetValue(key, out T? entry);

		if (wasRecoveredFine && entry is not null)
		{
			entries.Add(key, entry);
			cachedEntries.Remove(key);
			CompressCache(cachedEntries);
		}

		return wasRecoveredFine;
	}

	public object[] RecoverSeveral(Dictionary<object, T> entries, object[] keys)
	{
		throw new System.NotImplementedException();
	}

	private void CompressCache(Dictionary<object, T> entries)
	{
		if (entries.Count == 0)
		{
			_compressedCache = new byte[] { };
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
		Dictionary<object, T> cachedEntries = new();

		if (_compressedCache == null || _compressedCache.Length == 0)
			return cachedEntries;

		JsonSerializerOptions deserializationOptions = CacheSerialization.GetOptionsWith(new DictionaryJsonConverter<T>());


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
				cachedEntries = deserializedObject ?? new();
			}
		}

		return cachedEntries;
	}
}
