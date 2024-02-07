namespace CacheSwapper;

public class MemoryCacheSwapper : ICacheSwapper
{
    private Dictionary<object, object> _cachedEntries = new Dictionary<object, object>();

    public IEnumerable<object> Dump(Dictionary<object, object> entries, IEnumerable<object> keysToDump)
    {
        var addedKeys = new List<object>();
        
        foreach(var k in keysToDump)
        {
            if (_cachedEntries.TryAdd(k, entries[k]))
            {
                addedKeys.Add(k);
                entries.Remove(k);
            }
        }

        return addedKeys;
    }

    public bool Recover(Dictionary<object, object> entries, object key)
    {
        bool wasRecoveredFine = _cachedEntries.TryGetValue(key, out object entry);

        if (wasRecoveredFine)
        {
            entries.Add(key,entry);
            _cachedEntries.Remove(key);
        }

        return wasRecoveredFine;
    }
}
