
namespace CacheSwapper;

public interface ICacheSwapper
{
    IEnumerable<object> Dump(Dictionary<object, object> entries, IEnumerable<object> keysToDump);
    bool Recover(Dictionary<object, object> entries, object key);
}