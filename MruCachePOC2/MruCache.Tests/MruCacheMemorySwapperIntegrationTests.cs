
using MruCache.CacheSwapper;

namespace MruCache.Tests
{
	public class MruCacheMemorySwapperIntegrationTests : MruCacheIntegrationTests
	{
		public override Cache<T> CreateCache<T>()
		{
			var cache = new Cache<T>();
			cache.CacheSwapper = new MemoryCacheSwapper<MruCacheEntry<T?>>();

			return cache;
		}
	}
}
