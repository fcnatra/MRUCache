using System;

namespace MruCache;

#nullable enable

public class MruCacheEntry<T>
{
    private T? _value;

    public MruCacheEntry(T? value)
    {
        Value = value;
    }

    public T? Value 
    {
        get 
        {
            Touch();
            return _value;
        }

        set
        {
            Touch();
            _value = value;
        }
    }

    public DateTime LastAccessTime { get; set; }

    private void Touch()
    {
        LastAccessTime = DateTime.Now;
    }
}