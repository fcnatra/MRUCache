
using CacheSwapper;

namespace MruCache.Tests
{
	public class MruCacheSqliteSwapperIntegrationTests : MruCacheIntegrationTests
	{
		public override Cache<T> CreateCache<T>()
		{
			var tempPath = Path.GetTempPath();
			var cache = new Cache<T>();
			cache.CacheSwapper = new SqLiteCacheSwapper<MruCacheEntry<T?>>(tempPath, new FileManager());

			return cache;
		}
	}
}
