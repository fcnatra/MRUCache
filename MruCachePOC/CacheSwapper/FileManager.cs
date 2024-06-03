namespace CacheSwapper;

public class FileManager : IFileManager
{
	public string CombinePath(string path1, string path2) => Path.Combine(path1, path2);

	public void CreateFileOrOverride(string filePath)
	{
		File.Create(filePath);
	}

	public void CreateFolder(string path)
	{
		Directory.CreateDirectory(path);
	}

	public void DeleteFile(string filePath)
	{
		File.Delete(filePath);
	}

	public bool FileExists(string filePath) => File.Exists(filePath);
}
