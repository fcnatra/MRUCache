namespace CacheSwapper
{
	public interface IFileManager
	{
		string CombinePath(string databasePath, string dATABASENAME);
		void CreateFileOrOverride(string dbFilePath);
		void Delete(string dbFilePath);
		bool FileExists(string dbFilePath);
	}
}