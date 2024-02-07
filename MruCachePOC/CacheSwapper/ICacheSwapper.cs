
namespace CacheSwapper;

public interface ICacheSwapper
{
    void Dump(Dictionary<object, object> entries, IEnumerable<object> keysToDump);
    bool Recover(Dictionary<object, object> entries, object key);
}