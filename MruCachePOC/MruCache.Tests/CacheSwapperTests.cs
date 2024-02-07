using System.Diagnostics;
using FakeItEasy;
using CacheSwapper;

namespace MruCache.Tests;

public class CacheSwapperTests
{
    [Fact]
    public void GivenDumpedEntry_RecoverIt_ReturnsTrue()
    {
        ICacheSwapper cache = new MemoryCacheSwapper();

        var nameOfKey1 = "key1";

        var entries = new Dictionary<object, object>
        {
            { nameOfKey1, new MruCacheEntry<string>("value1") }
        };
        cache.Dump(entries, new List<object> {nameOfKey1});

        // ACT
        var wasRecovered = cache.Recover(entries, nameOfKey1);

        // ASSERT
        Assert.True(wasRecovered);
    }

    [Fact]
    public void GivenThreeEntries_DumpingFirstTwo_LeavesThirdAloneInTheList()
    {
        ICacheSwapper cache = new MemoryCacheSwapper();

        var nameOfKey3 = "key3";

        var entries = new Dictionary<object, object>()
        {
            {"key1", new MruCacheEntry<string>("value1")},
            {"key2", new MruCacheEntry<string>("value2")},
            {nameOfKey3, new MruCacheEntry<string>("value3")}
        };

        // ACT
        cache.Dump(entries, entries.Keys.Take(2));

        // ASSERT
        bool entryIsInTheList = entries.TryGetValue(nameOfKey3, out object? entry);

        Assert.Equal(1, entries.Count());
        Assert.True(entryIsInTheList);
    }

    [Fact]
    public void GivenTwoEntries_DumpingOne_RemovesEntryFromList()
    {
        ICacheSwapper cache = new MemoryCacheSwapper();

        var nameOfKey2 = "key2";

        var entries = new Dictionary<object, object>();
        entries.Add("key1", new MruCacheEntry<string>("value1"));
        entries.Add(nameOfKey2, new MruCacheEntry<string>("value2"));

        // ACT
        cache.Dump(entries, new List<object> {nameOfKey2});

        // ASSERT
        bool entryIsInTheList = entries.TryGetValue(nameOfKey2, out object? entry);

        Assert.Equal(1, entries.Count());
        Assert.False(entryIsInTheList);
    }
}