namespace CacheSwapper;

public class MemoryCacheSwapper : ICacheSwapper
{
    public void Dump(Dictionary<object, object> entries, IEnumerable<object> keysToDump)
    {}
    public bool Recover(Dictionary<object, object> entries, object key)
    {
        return true;
    }
}
