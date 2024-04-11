using FakeItEasy;
using CacheSwapper;

namespace MruCache.Tests;

public class MruCacheTests
{
    [Fact]
    public void GivenCacheSwapper_AddingNewElement_SearchIntoDumpedElements()
    {
        var keyName = 1;
        var fakeCacheSwapper = A.Fake<ICacheSwapper<MruCacheEntry<string?>>>();

        var cache = new Cache<string>();
        cache.CacheSwapper = fakeCacheSwapper;

        // ACT
        cache.AddOrUpdate(keyName, "any value");

        // ASSERT
        A.CallTo(() => fakeCacheSwapper.Recover(A<Dictionary<object, MruCacheEntry<string?>>>._, A<int>.That.IsEqualTo<int>(keyName)))
        .MustHaveHappened(1, Times.Exactly);
    }

    [Fact]
    public void GivenCacheSwapper_ExceedentElements_AreDumpedIntoTheCache()
    {
        var firstKey = 1;
        var firstValueAdded = "one";

        var fakeCacheSwapper = A.Fake<ICacheSwapper<MruCacheEntry<string?>>>();
        A.CallTo(() => fakeCacheSwapper.Recover(A<Dictionary<object, MruCacheEntry<string?>>>._, A<object>._)).Returns(false);

        var cache = new Cache<string>();
        cache.CacheSwapper = fakeCacheSwapper;

        cache.MaxActiveEntries = 2;
        cache.AddOrUpdate(firstKey, firstValueAdded);
        cache.AddOrUpdate(2, "any value");

        // ACT
        cache.AddOrUpdate(3, "any other value");

        // ASSERT
        A.CallTo(() => fakeCacheSwapper.Dump(A<Dictionary<object, MruCacheEntry<string?>>>._, A<IEnumerable<object>>
        .That.IsSameSequenceAs<IEnumerable<object>>(new List<object>{firstKey})))
        .MustHaveHappened(1, Times.Exactly);
    }

    [Fact]
    public void Given_InexistingKey_NullValueIsReturned()
    {
        var cache = new Cache<object>();

        // ACT
        var valueRecovered = cache["anyKey"];

        // ASSERT
        Assert.Null(valueRecovered);
    }

    [Fact]
    public void Given_InexistingKey_TryGetReturnsFalse()
    {
        var cache = new Cache<object>();

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
        
        // ACT
        cache.AddOrUpdate(keyName, null);

        // ASSERT
        var valueRecovered = cache[keyName];
        Assert.Null(valueRecovered);
    }

    [Fact]
    public void Given_ElementsExceedingAllowedMaximum_OldestAccessedIsRemoved()
    {
        var firstKey = 1;
        var secondKey = 2;
        var thirdKey = 3;

        var secondValueAdded = "two";

        var cache = new Cache<string>();
        cache.MaxActiveEntries = 2;
        cache.AddOrUpdate(firstKey, "any value");
        cache.AddOrUpdate(secondKey, secondValueAdded);

        // ACT
        _ = cache[firstKey];
        cache.AddOrUpdate(thirdKey, "any other value");

        // ASSERT
        bool valueExist = cache.TryGetValue(secondKey, out string? valueRecovered);

        Assert.False(valueExist);
        Assert.NotEqual(secondValueAdded, valueRecovered);
        Assert.Null(valueRecovered);

        Assert.True(cache.TryGetValue(firstKey, out string? _));
        Assert.True(cache.TryGetValue(thirdKey, out string? _));
    }
 
    [Fact]
    public void Given_ElementsInsideAllowedMaximum_NoneIsRemoved()
    {
        var firstKeyAdded = 1;
        var firstValueAdded = "one";

        var cache = new Cache<string>();
        cache.MaxActiveEntries = 3;
        cache.AddOrUpdate(firstKeyAdded, firstValueAdded);
        cache.AddOrUpdate(2, "two");
        cache.AddOrUpdate(3, "three");

        // ACT
        bool valueExist = cache.TryGetValue(firstKeyAdded, out string? valueRecovered);

        // ASSERT
        Assert.True(valueExist);
        Assert.Equal(firstValueAdded, valueRecovered);
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

        // ACT
        cache[keyName] = value;

        // ASSERT
        var cachedValue = cache[keyName];
        Assert.Equal(value, cachedValue);
    }
}
