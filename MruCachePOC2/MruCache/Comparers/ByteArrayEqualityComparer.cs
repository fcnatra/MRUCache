using System.Collections;

namespace MruCache.Comparers
{
	internal class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
	{
		private const int BEGINNING_PRIME_NUMBER = 17;
		private const int MULTIPLIER_PRIME_NUMBER = 31;

		public static readonly ByteArrayEqualityComparer Default = new ByteArrayEqualityComparer();

		public bool Equals(byte[]? x, byte[]? y)
		{
			if (x == y) return true; // Reference equality check
			if (x == null || y == null) return false; // Null check
			if (x.Length != y.Length) return false; // Length check

			// Use SequenceEqual for comparison
			return x.SequenceEqual(y);
		}

		public int GetHashCode(byte[]? obj)
		{
			if (obj == null) return 0;

			// unchecked
			// Ensures that the multiplication and addition do not throw overflow exceptions,
			// as we are intentionally allowing integer overflow to occur in the hash code computation.
			unchecked
			{
				// Use a prime number to generate a hash code
				int hash = BEGINNING_PRIME_NUMBER;
				foreach (byte b in obj)
					hash = hash * MULTIPLIER_PRIME_NUMBER + b;

				return hash;
			}
		}
	}
}
