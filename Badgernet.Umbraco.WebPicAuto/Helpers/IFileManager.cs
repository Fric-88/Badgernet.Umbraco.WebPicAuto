namespace Badgernet.WebPicAuto.Helpers;

public interface IFileManager
{
    MemoryStream? ReadFile(string relativePath);
    bool WriteFile(string relativePath, Stream fileStream);
    bool DeleteFile(string relativePath);
    bool FileExists(string relativePath);
    string GetFreePath(string relativePath, string targetExtension = "");
    long CompareFileSize(string referenceRelativePath, string relativePath);
}
