namespace CacheSwapper;

internal class SqLiteCacheSwapper<T> : ICacheSwapper<T> where T : class
{
    public IEnumerable<object> Dump(Dictionary<object, T> entries, IEnumerable<object> keysToDump)
    {
        throw new NotImplementedException();
    }

    public bool Recover(Dictionary<object, T?> entries, object key)
    {
        throw new NotImplementedException();
    }
}