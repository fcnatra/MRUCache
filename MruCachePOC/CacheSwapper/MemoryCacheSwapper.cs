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
        
        foreach(var k in keysToDump)
        {
            if (TryAdd(k, entries[k]))
            {
                addedKeys.Add(k);
                entries.Remove(k);
            }
        }

        return addedKeys;
    }

    private bool TryAdd(object key, object value)
    {
        var entries = DecompressCache<Dictionary<object, object>>();

        if (entries is null)
            entries = new Dictionary<object, object>();

        var addedOk = entries.TryAdd(key, value);

        if (addedOk)
            CompressCache(entries);

        return addedOk;
    }

    private void CompressCache<T>(T cachedEntries)
    {
        JsonSerializerOptions serializationOptions = SetupSerializationOptions();
        var jsonString = JsonSerializer.Serialize(cachedEntries, serializationOptions);

        using (MemoryStream ms = new MemoryStream(jsonString)) 
        using (GZipStream zs = new GZipStream(ms, CompressionMode.Compress, true))
        {
        }
    }

    private T? DecompressCache<T>()
    {
        using (MemoryStream ms = new MemoryStream(_compressedCache)) 
        using (GZipStream zs = new GZipStream(ms, CompressionMode.Decompress, true))
        {
            StreamReader reader = new(zs);
            string jsonString = reader.ReadToEnd();
            
            if (string.IsNullOrEmpty(jsonString)) return default;

			JsonSerializerOptions deserializationOptions = SetupDeserializationOptions();
			T? cacheEntries = JsonSerializer.Deserialize<T>(jsonString, deserializationOptions);

            return cacheEntries;
        }
    }
    
    private static JsonSerializerOptions SetupDeserializationOptions()
    {
        // needed to avoid getting JsonElement.JsonValueKind values instead of actual values
        var deserializationOptions = new JsonSerializerOptions();
        deserializationOptions.Converters.Add(new ByteArrayJsonConverterForSerialization());
        return deserializationOptions;
    }
    
    private static JsonSerializerOptions SetupSerializationOptions()
    {
        // needed to avoid getting JsonElement.JsonValueKind values instead of actual values
        var deserializationOptions = new JsonSerializerOptions();
        deserializationOptions.Converters.Add(new ByteArrayJsonConverterForDeserialization());
        return deserializationOptions;
    }

    public bool Recover(Dictionary<object, object> entries, object key)
    {
        bool wasRecoveredFine = _cachedEntries.TryGetValue(key, out object? entry);

        if (wasRecoveredFine)
        {
            entries.Add(key,entry);
            _cachedEntries.Remove(key);
        }

        return wasRecoveredFine;
    }
}
