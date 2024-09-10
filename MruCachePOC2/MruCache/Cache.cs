using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MruCache.CacheSwapper;
using MruCache.Comparers;

namespace MruCache;

#nullable enable

public class Cache<T> : IDisposable
{
	private object _lock;
	private Dictionary<object, MruCacheEntry<T?>> _entries;
	private bool IsFull => MaxActiveEntries > 0 && _entries.Count >= MaxActiveEntries;
	private const int ONE_MILLION = 1000000;
	/// <summary>
	/// Entries kept in memory. Once overpassed this number, oldest entries are cached.
	/// If no CacheSwapper is provided, entries overflow are deleted.
	/// If ZERO, unlimitied entries in memory are allowed.
	/// </summary>
	public uint MaxActiveEntries { get; set; } = ONE_MILLION;
	public ICacheSwapper<MruCacheEntry<T?>>? CacheSwapper { get; set; }

	public Cache()
	{
		this._lock = new object();
		this._entries = new Dictionary<object, MruCacheEntry<T?>>();
	}

	internal static Cache<T> CacheWithArrayAsAKey()
	{
		var cache = new Cache<T>();
		cache._entries = new Dictionary<object, MruCacheEntry<T?>>((IEqualityComparer<object>)ArrayEqualityComparerWithObjectCasting<object>.Default);
		return cache;
	}

	public T? this[object key]
	{
		get
		{
			this.TryGetValue(key, out T? value);
			return value;
		}
		set
		{
			this.AddOrUpdate(key, value);
		}
	}

	public bool TryGetValue(object key, out T? value)
	{
		if (_entries.TryGetValue(key, out MruCacheEntry<T?>? entry))
		{
			value = entry.Value;
			return true;
		}
		else
		{
			if (CacheSwapper?.Recover(_entries, key) ?? false)
			{
				//MakeRoomIfFull(); - is this producing continuous Dumping?
				return this.TryGetValue(key, out value);
			}
			else
			{
				value = default;
				return false;
			}
		}
	}

	public Dictionary<object, T?> TryGetValues(object[] keys)
	{
		Dictionary<object, T?> valuesRecovered = new Dictionary<object, T?>();
		Dictionary<object, int> keysNotFound = new Dictionary<object, int>();

		for (int i = 0; i < keys.Length; i++)
		{
			if (_entries.TryGetValue(keys[i], out MruCacheEntry<T?>? entry))
				valuesRecovered.Add(keys[i], entry.Value);
			else
				keysNotFound.Add(keys[i], i);
		}

		object[]? keysRecovered = CacheSwapper?.RecoverSeveral(_entries, keysNotFound.Keys.ToArray());

		if (keysRecovered != null) foreach (object key in keysRecovered)
				valuesRecovered.Add(key, _entries[key].Value);

		return valuesRecovered;
	}

	public void AddOrUpdate(object key, T? value)
	{
		lock (_lock)
		{
			if (_entries.ContainsKey(key))// || CacheSwapper?.Recover(_entries, key) == true)
			{
				_entries[key].Value = value;
			}
			else
			{
				MakeRoomIfFull();
				_entries.Add(key, new MruCacheEntry<T?>(value));
			}
		}
	}

	private void MakeRoomIfFull()
	{
		if (IsFull)
		{
			var keysToRemove = _entries.OrderBy(e => (e.Value).LastAccessTime)
				.Select(e => e.Key)
				.Take(TenPercent)
				.ToList();
			if (this.CacheSwapper is not null)
			{
				Trace.WriteLine($"Dumping keys from cache to {CacheSwapper.GetType()} ({TenPercent})");
				CacheSwapper.Dump(_entries, keysToRemove);
			}
			else
				this.RemoveKeys(keysToRemove);
		}
	}

	private void RemoveKeys(IEnumerable<object> keysToRemove)
	{
		foreach (var key in keysToRemove)
			_entries.Remove(key);
	}

	public void Dispose()
	{
		CacheSwapper?.Dispose();
	}

	private int OnePercent
	{
		get
		{
			int number = (int)(MaxActiveEntries / 100);
			return number < 1 ? 1 : number;
		}
	}

	private int TenPercent
	{
		get
		{
			int number = (int)(MaxActiveEntries / 10);
			return number < 1 ? 1 : number;
		}
	}

}
