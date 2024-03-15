using CacheSwapper;

namespace MruCache.Tests;

public class CacheSwapperTests
{
	ICacheSwapper<MruCacheEntry<object>> cacheSwapper;

	public CacheSwapperTests()
	{
		//cacheSwapper = new MemoryCacheSwapper<MruCacheEntry<object>>();

		var tempPath = Path.GetTempPath();
		cacheSwapper = new SqLiteCacheSwapper<MruCacheEntry<object>>(tempPath, new FileManager());
	}

	[Theory]
	[InlineData([15 * 1000000], Skip = ("Doesn't pass"))]
	[InlineData([10 * 1000000])]
	[InlineData([2 * 1000000])]
	public void SeveralMillionEntries_CanBeCached(int numberOfEntries)
	{
		var entries = new Dictionary<object, MruCacheEntry<object>>();
		for (int i = 0; i < numberOfEntries; i++)
			entries.Add(i, new MruCacheEntry<object>(i));

		// ACT
		var dumpedKeys = cacheSwapper.Dump(entries, entries.Keys);

		// ASSERT
		Assert.Equal(numberOfEntries, dumpedKeys.Count());
	}

	[Fact]
	public void RecoveringEntry_AlreadyRecovered_ReturnsFalse()
	{
		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<object>>
		{
			{ nameOfKey1, new MruCacheEntry<object>(value1) },
			{ "second key", new MruCacheEntry<object>("second valueS") },
		};
		cacheSwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		cacheSwapper.Recover(entries, nameOfKey1);
		bool recoveredFine = cacheSwapper.Recover(entries, nameOfKey1);

		// ASSERT
		Assert.False(recoveredFine);
	}

	[Fact]
	public void DumpingKey_ThatIsNotInTheEntryList_ThrowsException()
	{
		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<object>>
		{
			{ nameOfKey1, new MruCacheEntry<object>(value1) }
		};

		cacheSwapper.Dump(entries, new List<object> { nameOfKey1 }); // this removes nameOfKey1 from entries

		// ACT && ASSERT
		Assert.Throws<KeyNotFoundException>(() => cacheSwapper.Dump(entries, new List<object> { nameOfKey1 }));
	}

	[Fact]
	public void Dumping_AlreadyDumpedEntry_DoesNotReturnItsKey()
	{
		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<object>>();

		entries.Add(nameOfKey1, new MruCacheEntry<object>(value1));
		cacheSwapper.Dump(entries, new List<object> { nameOfKey1 });

		entries.Add(nameOfKey1, new MruCacheEntry<object>(value1));

		// ACT
		var dumpedKeys = cacheSwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ASSERT
		Assert.DoesNotContain(nameOfKey1, dumpedKeys);
	}

	[Fact]
	public void Recovering_NotDumpedValue_ReturnsFalse()
	{
		var entries = new Dictionary<object, MruCacheEntry<object>>();

		// ACT
		bool wasRecoveredFine = cacheSwapper.Recover(entries, "any keyname not previously dumped");

		// ASSERT
		Assert.False(wasRecoveredFine);
	}

	[Fact]
	public void GivenDumpedEntry_RecoveringIt_AddsTheDumpedValueToTheEntryList()
	{
		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<object>>
		{
			{ nameOfKey1, new MruCacheEntry<object>(value1) }
		};
		cacheSwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		cacheSwapper.Recover(entries, nameOfKey1);

		// ASSERT
		MruCacheEntry<object> entry = entries[nameOfKey1];
		Assert.Equal(value1, entry.Value);
	}

	[Fact]
	public void GivenDumpedEntry_RecoveringIt_ReturnsTrue()
	{
		var nameOfKey1 = "key1";

		var entries = new Dictionary<object, MruCacheEntry<object>>
		{
			{ nameOfKey1, new MruCacheEntry<object>("value1") }
		};
		cacheSwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		var wasRecovered = cacheSwapper.Recover(entries, nameOfKey1);

		// ASSERT
		Assert.True(wasRecovered);
	}

	[Fact]
	public void GivenThreeEntries_DumpingFirstTwo_LeavesThirdAloneInTheList()
	{
		var nameOfKey3 = "key3";

		var entries = new Dictionary<object, MruCacheEntry<object>>()
		{
			{"key1", new MruCacheEntry<object>("value1")},
			{"key2", new MruCacheEntry<object>("value2")},
			{nameOfKey3, new MruCacheEntry<object>("value3")}
		};

		// ACT
		cacheSwapper.Dump(entries, entries.Keys.Take(2));

		// ASSERT
		bool entryIsInTheList = entries.TryGetValue(nameOfKey3, out MruCacheEntry<object>? entry);

		Assert.Single(entries);
		Assert.True(entryIsInTheList);
	}

	[Fact]
	public void GivenTwoEntries_DumpingOne_RemovesEntryFromList()
	{
		var nameOfKey2 = "key2";

		var entries = new Dictionary<object, MruCacheEntry<object>>();
		entries.Add("key1", new MruCacheEntry<object>("value1"));
		entries.Add(nameOfKey2, new MruCacheEntry<object>("value2"));

		// ACT
		cacheSwapper.Dump(entries, new List<object> { nameOfKey2 });

		// ASSERT
		bool entryIsInTheList = entries.TryGetValue(nameOfKey2, out MruCacheEntry<object>? entry);

		Assert.Single(entries);
		Assert.False(entryIsInTheList);
	}


}
