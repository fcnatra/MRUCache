using CacheSwapper;

namespace MruCache.Tests;

public class CacheSwapperTests
{
	ICacheSwapper<MruCacheEntry<object>> _memorySwapper;

	public CacheSwapperTests()
    {
		_memorySwapper = new MemoryCacheSwapper<MruCacheEntry<object>>();
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
		_memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		_memorySwapper.Recover(entries, nameOfKey1);
		bool recoveredFine = _memorySwapper.Recover(entries, nameOfKey1);

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

		// ACT
		_memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ASSERT
		Assert.Throws<KeyNotFoundException>(() => _memorySwapper.Dump(entries, new List<object> { nameOfKey1 }));
	}

	[Fact]
	public void Dumping_AlreadyDumpedEntry_DoesNotReturnItsKey()
	{
		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<object>>();

		entries.Add(nameOfKey1, new MruCacheEntry<object>(value1));
		_memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		entries.Add(nameOfKey1, new MruCacheEntry<object>(value1));

		// ACT
		var dumpedKeys = _memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ASSERT
		Assert.DoesNotContain(nameOfKey1, dumpedKeys);
	}

	[Fact]
	public void Recovering_NotDumpedValue_ReturnsFalse()
	{
		var entries = new Dictionary<object, MruCacheEntry<object>>();

		// ACT
		bool wasRecoveredFine = _memorySwapper.Recover(entries, "any keyname not previously dumped");

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
		_memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		_memorySwapper.Recover(entries, nameOfKey1);

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
		_memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		var wasRecovered = _memorySwapper.Recover(entries, nameOfKey1);

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
		_memorySwapper.Dump(entries, entries.Keys.Take(2));

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
		_memorySwapper.Dump(entries, new List<object> { nameOfKey2 });

		// ASSERT
		bool entryIsInTheList = entries.TryGetValue(nameOfKey2, out MruCacheEntry<object>? entry);

		Assert.Single(entries);
		Assert.False(entryIsInTheList);
	}
}