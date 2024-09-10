using System.Collections;

namespace MruCache.Comparers
{
	internal class ArrayEqualityComparerWithObjectCasting<T> : IEqualityComparer<T>
	{
		public static readonly ArrayEqualityComparerWithObjectCasting<T> Default = new ArrayEqualityComparerWithObjectCasting<T>();

		public bool Equals(T? x, T? y)
        {
            if (x is null && y is null) return true;
			if (x is null || y is null) return false;

            return ((IEnumerable)x).Cast<object>().Select(ex => ex)
                .SequenceEqual(
                ((IEnumerable)y).Cast<object>().Select(ey => ey));
        }

        public int GetHashCode(T obj)
		{
			if (obj == null) return 0;
            object[] objArray = ((IEnumerable)obj)
				.Cast<object>()
				.Select(x => x)
				.ToArray();

			return objArray.Aggregate(string.Empty, (s, i) => s + i.GetHashCode(), s => s.GetHashCode());
		}
	}
}
