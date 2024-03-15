namespace CacheSwapper
{
	public class FileManager : IFileManager
	{
		public string CombinePath(string path1, string path2) => Path.Combine(path1, path2);

		public void CreateFileOrOverride(string dbFilePath)
		{
			File.Create(dbFilePath);
		}

		public void Delete(string dbFilePath)
		{
			File.Delete(dbFilePath);
		}

		public bool FileExists(string dbFilePath) => File.Exists(dbFilePath);
	}
}