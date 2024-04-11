
namespace CacheSwapper;

public interface ICacheSwapper<T>
{
    IEnumerable<object> Dump(Dictionary<object, T> entries, IEnumerable<object> keysToDump);
    bool Recover(Dictionary<object, T> entries, object key);
}