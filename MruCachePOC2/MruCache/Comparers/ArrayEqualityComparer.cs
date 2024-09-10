using System.Collections;

namespace MruCache.Comparers
{
	internal class ArrayEqualityComparer<T> : IEqualityComparer<T[]>
	{
		public static readonly ArrayEqualityComparer<T> Default = new ArrayEqualityComparer<T>();

		public bool Equals(T[]? x, T[]? y)
		{
			if (x == y) return true;
			if (x == null || y == null) return false;
			if (x.Length != y.Length) return false;

			return x.SequenceEqual(y);
		}

		public int GetHashCode(T[] obj)
		{
			return obj.Aggregate(string.Empty, (s, i) => s + i?.GetHashCode(), s => s.GetHashCode());
		}
	}
}
