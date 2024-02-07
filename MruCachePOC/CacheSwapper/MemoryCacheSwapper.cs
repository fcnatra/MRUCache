namespace CacheSwapper;

public class MemoryCacheSwapper : ICacheSwapper
{
    public void Dump(Dictionary<object, object> entries, IEnumerable<object> keysToDump)
    {
        foreach(var k in keysToDump)
            entries.Remove(k);
    }

    public bool Recover(Dictionary<object, object> entries, object key)
    {
        return true;
    }
}
