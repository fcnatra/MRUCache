using System.Collections;

namespace MruCache
{
	internal class ArrayEqualityComparer<T> : IEqualityComparer<T[]>
	{
		public static readonly ArrayEqualityComparer<T> Default = new ArrayEqualityComparer<T>();

		public bool Equals(T[] x, T[] y)
		{
			return x?.SequenceEqual(y) ?? y is null;
		}

		public int GetHashCode(T[] obj)
		{
			return obj.Aggregate(string.Empty, (s, i) => s + i.GetHashCode(), s => s.GetHashCode());
		}
	}

	internal class ArrayEqualityComparerWithObjectCasting<T> : IEqualityComparer<T>
	{
		public static readonly ArrayEqualityComparerWithObjectCasting<T> Default = new ArrayEqualityComparerWithObjectCasting<T>();

		public bool Equals(T x, T y)
		{
			return ((IEnumerable)x).Cast<object>().Select(ex => ex)
				.SequenceEqual(
				((IEnumerable)y).Cast<object>().Select(ey => ey));
		}

		public int GetHashCode(T obj)
		{
			var objArray = ((IEnumerable)obj)
				.Cast<object>()
				.Select(x => x)
				.ToArray();

			return objArray.Aggregate(string.Empty, (s, i) => s + i.GetHashCode(), s => s.GetHashCode());
		}
	}
}
