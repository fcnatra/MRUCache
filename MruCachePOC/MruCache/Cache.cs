using CacheSwapper;
using System.Diagnostics;

namespace MruCache;

public class Cache<T> : IDisposable
{
	private object _lock;
	private Dictionary<object, MruCacheEntry<T?>> _entries;
	private bool IsFull => _entries.Count >= MaxActiveEntries;
	public uint MaxActiveEntries { get; set; } = 1000000;
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

	public void AddOrUpdate(object key, T? value)
	{
		lock (_lock)
		{
			//if (_entries.ContainsKey(key) || CacheSwapper?.Recover(_entries, key) == true)
			//{
			//	_entries[key].Value = value;
			//}
			//else
			//{
			MakeRoomIfFull();
			_entries.Add(key, new MruCacheEntry<T?>(value));
			//}
		}
	}

	private void MakeRoomIfFull()
	{
		if (IsFull)
		{
			// get 1% of entries which were Least Recently Used
			var keysToRemove = _entries.OrderBy(e => (e.Value).LastAccessTime)
				.Select(e => e.Key)
				.Take(TenPercent)
				.ToList();
			if (this.CacheSwapper is not null)
			{
				CacheSwapper.Dump(_entries, keysToRemove);
				Trace.WriteLine($"Dumping keys from cache to {CacheSwapper.GetType()} ({TenPercent})");
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
