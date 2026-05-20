namespace Aion.GameServer.Dataholders.LoadingUtils;

public sealed class XmlMergeResult
{
	public XmlMergeResult(string cacheFilePath, bool fileWasModified, IReadOnlyList<string> importedFiles)
	{
		CacheFilePath = cacheFilePath;
		FileWasModified = fileWasModified;
		ImportedFiles = importedFiles;
	}

	public string CacheFilePath { get; }

	public bool FileWasModified { get; }

	public IReadOnlyList<string> ImportedFiles { get; }
}
