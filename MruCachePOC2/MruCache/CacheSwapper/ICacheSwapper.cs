using System;
using System.Collections.Generic;

namespace MruCache.CacheSwapper;

public interface ICacheSwapper<T> : IDisposable
{
    IEnumerable<object> Dump(Dictionary<object, T> entries, List<object> keysToDump);
    bool Recover(Dictionary<object, T> entries, object key);
	object[]? RecoverSeveral(Dictionary<object, T> entries, object[] keys);
}