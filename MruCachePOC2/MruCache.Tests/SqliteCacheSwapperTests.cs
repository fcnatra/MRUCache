using MruCache.CacheSwapper;

namespace MruCache.Tests
{
	public class SqliteCacheSwapperTests : CacheSwapperTests
	{
		public override ICacheSwapper<MruCacheEntry<object?>> CreateInstance()
		{
			var tempPath = Path.GetTempPath();
			return new SqLiteCacheSwapper<MruCacheEntry<object?>>(tempPath, new FileManager());

		}
	}
}
