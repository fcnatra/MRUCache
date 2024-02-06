
namespace MruCache;

public interface ICacheSwapper
{
    void Dump<T>(Dictionary<object, MruCacheEntry<T>> entries, IEnumerable<object> keysToRemove);
    bool Recover<T>(Dictionary<object, MruCacheEntry<T>> entries, object key);
}