using CacheSwapper;

namespace MruCache.Tests
{
	public class MemoryCacheSwapperTests : CacheSwapperTests
	{
		public override ICacheSwapper<MruCacheEntry<object>> CreateInstance()
		{
			return new MemoryCacheSwapper<MruCacheEntry<object>>();
		}
	}
}
