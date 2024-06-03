
namespace CacheSwapper;

public interface ICacheSwapper<T> : IDisposable
{
	IEnumerable<object> Dump(Dictionary<object, T> entries, List<object> keysToDump);
	bool Recover(Dictionary<object, T> entries, object key);
}