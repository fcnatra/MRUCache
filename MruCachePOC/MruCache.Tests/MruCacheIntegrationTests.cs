using CacheSwapper;

namespace MruCache.Tests;

public class MruCacheIntegrationTests
{
    [Fact]
    public void Given_InexistingKey_NullValueIsReturned()
    {
        // ARRANGE
        var cache = new Cache<string>();
        cache.CacheSwapper = new MemoryCacheSwapper<MruCacheEntry<string?>>();

        // ACT
        var valueRecovered = cache["anyKey"];

        // ASSERT
        Assert.Null(valueRecovered);
    }

    [Fact]
    public void Given_InexistingKey_TryGetReturnsFalse()
    {
		var cache = new Cache<object>();
		cache.CacheSwapper = new MemoryCacheSwapper<MruCacheEntry<object?>>();

		// ACT
		var keyExists = cache.TryGetValue("anyKey", out object? _);

        // ASSERT
        Assert.False(keyExists);
    }

    [Fact]
    public void Given_NullValue_KeyIsCached()
    {
        var keyName = "test";
		var cache = new Cache<object>();
		cache.CacheSwapper = new MemoryCacheSwapper<MruCacheEntry<object?>>();

		// ACT
		cache.AddOrUpdate(keyName, null);

        // ASSERT
        var valueRecovered = cache[keyName];
        Assert.Null(valueRecovered);
    }

	[Fact]
	public void GivenObject_CanBeRecovered_MoreThanOnce()
	{
		var keyName = "test";
        var keyValue = new byte[] { 1, 2, 3 };

		var cache = new Cache<object>();
		cache.CacheSwapper = new MemoryCacheSwapper<MruCacheEntry<object?>>();

		// ACT
		cache.AddOrUpdate(keyName, keyValue);

		// ASSERT
		var valueRecovered = cache[keyName];
		var valueRecoveredSecondTime = cache[keyName];
		Assert.Equivalent(keyValue, valueRecovered);
		Assert.Equivalent(keyValue, valueRecoveredSecondTime);
	}

	[Theory]
    [InlineData(new object[] { "prueba", "prueba1"} )]
    [InlineData(new object[] { new byte[]{1, 2, 3}, new byte[]{4, 5, 6} } )]
    [InlineData(new object[] { 7, 8 } )]
    [InlineData(new object[] { 12.3, 45.6 } )]
    [InlineData(new object?[] { "pruebaNull", null } )]
    public void Given_Object_CanBeRecovered(object keyName, object value)
    {
		var cache = new Cache<object>();
		cache.CacheSwapper = new MemoryCacheSwapper<MruCacheEntry<object?>>();

		// ACT
		cache[keyName] = value;

        // ASSERT
        var cachedValue = cache[keyName];
        Assert.Equal(value, cachedValue);
    }
}
