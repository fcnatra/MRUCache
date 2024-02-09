using CacheSwapper;

namespace MruCache;

public class Cache<T>
{
    private object _lock;
    private Dictionary<object, MruCacheEntry<T>> _entries;
    private bool IsFull => _entries.Count >= MaxActiveEntries;
    public uint MaxActiveEntries { get; set; } = 1000000;
    public ICacheSwapper<MruCacheEntry<T>>? CacheSwapper { get; set; }
    
    public Cache()
    {
        this._lock = new object();
        this._entries = new Dictionary<object, MruCacheEntry<T>>();
    }

    public T? this[object key]
    {
        get
        {
            this.TryGetValue(key, out T? value);
            return value;
        }
        set
        {
            this.AddOrUpdate(key, value);
        }
    }

    public bool TryGetValue(object key, out T? value)
    {
        if (_entries.TryGetValue(key, out MruCacheEntry<T>? entry))
        {
            value = ((MruCacheEntry<T>)entry).Value;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public void AddOrUpdate(object key, T? value)
    {
        lock (_lock)
        {
            if (_entries.ContainsKey(key) || CacheSwapper?.Recover(_entries, key) == true)
            {
                ((MruCacheEntry<T>)_entries[key]).Value = value;
            }
            else
            {
                MakeRoomIfFull();
                _entries.Add(key, new MruCacheEntry<T>(value));
            }
        }
    }

    private void MakeRoomIfFull()
    {
        if (IsFull)
        {
            // get 1% of entries which were Least Recently Used
            var keysToRemove = _entries.OrderBy(e => ((MruCacheEntry<T>)e.Value).LastAccessTime).Select(e => e.Key).Take(OnePercent);

            if (this.CacheSwapper is not null)
                CacheSwapper.Dump(_entries, keysToRemove);
            else
                this.RemoveKeys(keysToRemove);
        }
    }

    private void RemoveKeys(IEnumerable<object> keysToRemove)
    {
        foreach (var key in keysToRemove)
            _entries.Remove(key);
    }

    private int OnePercent
    {
        get
        {
            int number = (int)(MaxActiveEntries / 100);
            return number < 1 ? 1 : number;
        }
    }
}
