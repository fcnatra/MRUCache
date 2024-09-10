namespace MruCache.CacheSwapper
{
	public interface IFileManager
	{
		string CombinePath(string path1, string path2);
		void CreateFileOrOverride(string filePath);
		void CreateFolder(string path);
		void DeleteFile(string filePath);
		bool FileExists(string filePath);
	}
}