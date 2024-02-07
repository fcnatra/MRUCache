using System.Diagnostics;
using FakeItEasy;
using CacheSwapper;

namespace MruCache.Tests;

public class CacheSwapperTests
{
    [Fact]
    public void GivenTwoEntries_DumpingOne_RemovesEntryFromList()
    {
        ICacheSwapper cache = new MemoryCacheSwapper();

        var entries = new Dictionary<object, object>();
        entries.Add("key1", new MruCacheEntry<string>("value1"));
        entries.Add("key2", new MruCacheEntry<string>("value2"));

        // ACT
        cache.Dump(entries, new List<object> {"value2"});

        // ASSERT
        Assert.Equal(1, entries.Count());
    }
}