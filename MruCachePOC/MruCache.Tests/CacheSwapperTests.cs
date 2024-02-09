using CacheSwapper;

namespace MruCache.Tests;

public class CacheSwapperTests
{
	[Fact]
	public void RecoveringEntry_AlreadyRecovered_ReturnsFalse()
	{
		ICacheSwapper<object> memorySwapper = new MemoryCacheSwapper<object>();

		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, object>
		{
			{ nameOfKey1, new MruCacheEntry<string>(value1) },
			{ "second key", new MruCacheEntry<string>("second valueS") },
		};
		memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		memorySwapper.Recover(entries, nameOfKey1);
		bool recoveredFine = memorySwapper.Recover(entries, nameOfKey1);

		// ASSERT
		Assert.False(recoveredFine);
	}

	[Fact]
	public void DumpingKey_ThatIsNotInTheEntryList_ThrowsException()
	{
		ICacheSwapper<MruCacheEntry<string>> memorySwapper = new MemoryCacheSwapper<MruCacheEntry<string>>();

		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<string>>
		{
			{ nameOfKey1, new MruCacheEntry<string>(value1) }
		};

		// ACT
		memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ASSERT
		Assert.Throws<KeyNotFoundException>(() => memorySwapper.Dump(entries, new List<object> { nameOfKey1 }));
	}

	[Fact]
	public void Dumping_AlreadyDumpedEntry_DoesNotReturnItsKey()
	{
		ICacheSwapper<MruCacheEntry<string>> memorySwapper = new MemoryCacheSwapper<MruCacheEntry<string>>();

		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<string>>();

		entries.Add(nameOfKey1, new MruCacheEntry<string>(value1));
		memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		entries.Add(nameOfKey1, new MruCacheEntry<string>(value1));

		// ACT
		var dumpedKeys = memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ASSERT
		Assert.DoesNotContain(nameOfKey1, dumpedKeys);
	}

	[Fact]
	public void Recovering_NotDumpedValue_ReturnsFalse()
	{
		ICacheSwapper<object> memorySwapper = new MemoryCacheSwapper<object>();

		var entries = new Dictionary<object, object>();

		// ACT
		bool wasRecoveredFine = memorySwapper.Recover(entries, "any keyname not previously dumped");

		// ASSERT
		Assert.False(wasRecoveredFine);
	}

	[Fact]
	public void GivenDumpedEntry_RecoveringIt_AddsTheDumpedValueToTheEntryList()
	{
		ICacheSwapper<MruCacheEntry<object>> memorySwapper = new MemoryCacheSwapper<MruCacheEntry<object>>();

		var nameOfKey1 = "key1";
		var value1 = "value1";

		var entries = new Dictionary<object, MruCacheEntry<object>>
		{
			{ nameOfKey1, new MruCacheEntry<object>(value1) }
		};
		memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		memorySwapper.Recover(entries, nameOfKey1);

		// ASSERT
		bool entryIsInTheList = entries.TryGetValue(nameOfKey1, out MruCacheEntry<object>? entry);
		Assert.Equal(value1, ((MruCacheEntry<object>)entry).Value);
	}

	[Fact]
	public void GivenDumpedEntry_RecoveringIt_ReturnsTrue()
	{
		ICacheSwapper<MruCacheEntry<object>> memorySwapper = new MemoryCacheSwapper<MruCacheEntry<object>>();

		var nameOfKey1 = "key1";

		var entries = new Dictionary<object, MruCacheEntry<object>>
		{
			{ nameOfKey1, new MruCacheEntry<object>("value1") }
		};
		memorySwapper.Dump(entries, new List<object> { nameOfKey1 });

		// ACT
		var wasRecovered = memorySwapper.Recover(entries, nameOfKey1);

		// ASSERT
		Assert.True(wasRecovered);
	}

	[Fact]
	public void GivenThreeEntries_DumpingFirstTwo_LeavesThirdAloneInTheList()
	{
		ICacheSwapper<MruCacheEntry<string>> memorySwapper = new MemoryCacheSwapper<MruCacheEntry<string>>();

		var nameOfKey3 = "key3";

		var entries = new Dictionary<object, MruCacheEntry<string>>()
		{
			{"key1", new MruCacheEntry<string>("value1")},
			{"key2", new MruCacheEntry<string>("value2")},
			{nameOfKey3, new MruCacheEntry<string>("value3")}
		};

		// ACT
		memorySwapper.Dump(entries, entries.Keys.Take(2));

		// ASSERT
		bool entryIsInTheList = entries.TryGetValue(nameOfKey3, out MruCacheEntry<string>? entry);

		Assert.Single(entries);
		Assert.True(entryIsInTheList);
	}

	[Fact]
	public void GivenTwoEntries_DumpingOne_RemovesEntryFromList()
	{
		ICacheSwapper<MruCacheEntry<string>> memorySwapper = new MemoryCacheSwapper<MruCacheEntry<string>>();

		var nameOfKey2 = "key2";

		var entries = new Dictionary<object, MruCacheEntry<string>>();
		entries.Add("key1", new MruCacheEntry<string>("value1"));
		entries.Add(nameOfKey2, new MruCacheEntry<string>("value2"));

		// ACT
		memorySwapper.Dump(entries, new List<object> { nameOfKey2 });

		// ASSERT
		bool entryIsInTheList = entries.TryGetValue(nameOfKey2, out MruCacheEntry<string>? entry);

		Assert.Single(entries);
		Assert.False(entryIsInTheList);
	}
}